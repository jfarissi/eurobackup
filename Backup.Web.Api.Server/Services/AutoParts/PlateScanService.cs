using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Models;
using Backup.Web.Api.Server.Models.Catalog;
using Backup.Web.Api.Server.Services.Sales;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Backup.Web.Api.Server.Services.AutoParts
{
    public class PlateScanService : IPlateScanService
    {
        private readonly IStorageBroker storage;
        private readonly IHttpClientFactory httpFactory;
        private readonly IVinLookupService vinLookup;
        private readonly IOptions<PlateScanOptions> options;
        private readonly ILogger<PlateScanService> logger;
        private readonly IHttpContextAccessor httpContextAccessor;

        /// <summary>Véhicules fréquents sur le marché marocain (stub démo sans API).</summary>
        private static readonly VehicleInfo[] MoroccoDemoFleet =
        {
            new("VF1BZ090X12345678", "Dacia", "Logan", 2019, "K7M", "Essence", 90),
            new("UU1HSDAGB51234567", "Dacia", "Sandero", 2021, "H4M", "Essence", 90),
            new("VF1RJA00X51234567", "Renault", "Clio", 2018, "H5F", "Essence", 90),
            new("VF3M45GSYJS123456", "Peugeot", "208", 2020, "EB2", "Essence", 75),
            new("MALA351CANM123456", "Hyundai", "i10", 2017, "G3LA", "Essence", 67),
            new("JTDBR32E500123456", "Toyota", "Corolla", 2016, "1ZR", "Essence", 132),
            new("WVWZZZ1KZAW123456", "Volkswagen", "Polo", 2015, "CFW", "Diesel", 90),
            new("VF7SXHMRB81234567", "Citroen", "C3", 2019, "EB2DT", "Essence", 110),
        };

        public PlateScanService(
            IStorageBroker storage,
            IHttpClientFactory httpFactory,
            IVinLookupService vinLookup,
            IOptions<PlateScanOptions> options,
            ILogger<PlateScanService> logger,
            IHttpContextAccessor httpContextAccessor)
        {
            this.storage = storage;
            this.httpFactory = httpFactory;
            this.vinLookup = vinLookup;
            this.options = options;
            this.logger = logger;
            this.httpContextAccessor = httpContextAccessor;
        }

        public async Task<PlateScanResultDto> ScanPlateAsync(
            string companyId, IFormFile image, string? userId, CancellationToken ct = default)
        {
            if (image == null || image.Length == 0)
                throw new InvalidOperationException("Image de plaque requise.");
            if (image.Length > 5 * 1024 * 1024)
                throw new InvalidOperationException("Image trop volumineuse (max 5 Mo).");

            var plateNumber = await ExtractPlateFromImageAsync(image, ct);
            if (string.IsNullOrWhiteSpace(plateNumber))
                throw new InvalidOperationException("Impossible de lire la plaque sur l'image.");

            return await SearchByPlateAsync(companyId, plateNumber, options.Value.DefaultCountry, userId, ct);
        }

        public async Task<PlateScanResultDto> SearchByPlateAsync(
            string companyId, string plateNumber, string? country, string? userId, CancellationToken ct = default)
        {
            var countryCode = NormalizeCountry(country);
            var normalized = NormalizePlate(plateNumber, countryCode);
            if (string.IsNullOrWhiteSpace(normalized))
                throw new InvalidOperationException("Numéro de plaque invalide.");

            var (vehicle, isDemo) = await DecodePlateAsync(normalized, countryCode, ct);
            var products = await FindCompatibleProductsAsync(vehicle, ct);
            await SaveHistoryAsync(companyId, normalized, countryCode, vehicle, products.Count, userId, ct);

            return MapToResult(normalized, countryCode, vehicle, products, isDemo,
                isDemo
                    ? "Données véhicule de démonstration (API plaque non configurée). Branchez PlateScan:ApiKey pour la prod Maroc."
                    : null);
        }

        public async Task<PlateScanResultDto> SearchByVinAsync(
            string companyId, string vin, string? userId, CancellationToken ct = default)
        {
            var cleanVin = (vin ?? string.Empty).Trim().ToUpperInvariant();
            if (cleanVin.Length != 17)
                throw new InvalidOperationException("Le VIN doit contenir exactement 17 caractères.");

            var lookup = await vinLookup.ResolveAsync(cleanVin, companyId, ct);
            var vehicle = ToVehicleInfo(lookup.Vehicle);
            var products = await FindCompatibleProductsAsync(vehicle, ct);
            await SaveHistoryAsync(companyId, cleanVin, null, vehicle, products.Count, userId, ct);

            var message = lookup.Message
                ?? (lookup.FromCache
                    ? "VIN résolu depuis le cache local."
                    : lookup.IsDemo
                        ? "Décodage VIN en mode démo (NHTSA indisponible)."
                        : null);

            return MapToResult(cleanVin, null, vehicle, products, lookup.IsDemo, message);
        }

        public async Task<List<PlateHistoryDto>> GetHistoryAsync(
            string companyId, int limit = 20, CancellationToken ct = default)
        {
            var take = Math.Clamp(limit, 1, 100);
            return await storage.SelectAllErpPlateHistories()
                .AsNoTracking()
                .Where(h => h.CompanyId == companyId)
                .OrderByDescending(h => h.SearchedAt)
                .Take(take)
                .Select(h => new PlateHistoryDto(
                    h.Id.ToString(),
                    h.PlateNumber,
                    h.Country,
                    h.Vin,
                    h.Make,
                    h.Model,
                    h.Year,
                    h.ProductsFound,
                    h.SearchedAt))
                .ToListAsync(ct);
        }

        /// <summary>Normalise une plaque MA (ex. 12345-أ-6 → 12345-A-6) ou FR/BE.</summary>
        public static string NormalizePlate(string? raw, string country)
        {
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
            var s = raw.Trim().ToUpperInvariant()
                .Replace('|', '-')
                .Replace('/', '-')
                .Replace(' ', '-');
            s = Regex.Replace(s, @"\s+", "");

            // Lettres arabes courantes sur plaques marocaines → latin (approximation affichage)
            s = s
                .Replace('أ', 'A').Replace('ا', 'A').Replace('ب', 'B').Replace('د', 'D')
                .Replace('ه', 'H').Replace('و', 'W').Replace('ط', 'T').Replace('ج', 'J');

            s = Regex.Replace(s, @"[^A-Z0-9\-]", "");
            s = Regex.Replace(s, @"\-+", "-").Trim('-');

            if (string.Equals(country, "MA", StringComparison.OrdinalIgnoreCase))
            {
                // WW temporaire / format numérique-lettre-région
                if (Regex.IsMatch(s, @"^WW\d{3,6}$")) return s;
                var m = Regex.Match(s, @"^(\d{1,5})-?([A-Z]{1,2})-?(\d{1,2})$");
                if (m.Success)
                    return $"{m.Groups[1].Value}-{m.Groups[2].Value}-{m.Groups[3].Value}";
            }

            return s;
        }

        private string NormalizeCountry(string? country)
        {
            var c = (country ?? options.Value.DefaultCountry ?? "MA").Trim().ToUpperInvariant();
            return string.IsNullOrWhiteSpace(c) ? "MA" : c;
        }

        private async Task<string?> ExtractPlateFromImageAsync(IFormFile image, CancellationToken ct)
        {
            // Prod : brancher OCR (OpenALPR / Azure / fournisseur MA) via ProviderBaseUrl + ApiKey.
            var cfg = options.Value;
            if (!string.IsNullOrWhiteSpace(cfg.ApiKey) && !string.IsNullOrWhiteSpace(cfg.ProviderBaseUrl))
            {
                try
                {
                    var client = httpFactory.CreateClient("PlateScan");
                    using var content = new MultipartFormDataContent();
                    await using var stream = image.OpenReadStream();
                    content.Add(new StreamContent(stream), "image", image.FileName ?? "plate.jpg");
                    if (!string.IsNullOrWhiteSpace(cfg.ApiKey))
                        content.Headers.TryAddWithoutValidation("X-Api-Key", cfg.ApiKey);

                    var response = await client.PostAsync(
                        $"{cfg.ProviderBaseUrl.TrimEnd('/')}/plate/ocr", content, ct);
                    if (response.IsSuccessStatusCode)
                    {
                        var payload = await response.Content.ReadFromJsonAsync<ProviderPlateOcrResponse>(cancellationToken: ct);
                        if (!string.IsNullOrWhiteSpace(payload?.PlateNumber))
                            return payload.PlateNumber;
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "OCR plaque fournisseur indisponible — fallback démo MA");
                }
            }

            // Démo Maroc : plaque synthétique stable à partir du nom de fichier / taille
            await Task.Delay(200, ct);
            var seed = Math.Abs((image.FileName ?? "plate").GetHashCode(StringComparison.Ordinal) + (int)image.Length);
            var region = (seed % 80) + 1;
            var serial = (seed % 90000) + 10000;
            var letter = (char)('A' + (seed % 26));
            return $"{serial}-{letter}-{region}";
        }

        private async Task<(VehicleInfo Vehicle, bool IsDemo)> DecodePlateAsync(
            string plateNumber, string country, CancellationToken ct)
        {
            var cfg = options.Value;
            if (!string.IsNullOrWhiteSpace(cfg.ApiKey) && !string.IsNullOrWhiteSpace(cfg.ProviderBaseUrl))
            {
                try
                {
                    var client = httpFactory.CreateClient("PlateScan");
                    var url =
                        $"{cfg.ProviderBaseUrl.TrimEnd('/')}/plate/{Uri.EscapeDataString(plateNumber)}?country={Uri.EscapeDataString(country)}";
                    using var request = new HttpRequestMessage(HttpMethod.Get, url);
                    request.Headers.TryAddWithoutValidation("X-Api-Key", cfg.ApiKey);
                    var response = await client.SendAsync(request, ct);
                    if (response.IsSuccessStatusCode)
                    {
                        var data = await response.Content.ReadFromJsonAsync<ProviderVehicleResponse>(cancellationToken: ct);
                        if (data != null && !string.IsNullOrWhiteSpace(data.Make))
                        {
                            var vehicle = new VehicleInfo(
                                data.Vin, data.Make, data.Model, data.Year,
                                data.EngineCode, data.FuelType, data.PowerHP);
                            // Si la plaque renvoie un VIN, alimenter le cache pour les prochaines recherches.
                            if (!string.IsNullOrWhiteSpace(data.Vin) && data.Vin.Trim().Length == 17)
                            {
                                try
                                {
                                    await storage.UpsertErpVinVehicleAsync(new ErpVinVehicle
                                    {
                                        Vin = data.Vin.Trim().ToUpperInvariant(),
                                        Make = data.Make,
                                        Model = data.Model,
                                        Year = data.Year,
                                        EngineCode = data.EngineCode,
                                        FuelType = data.FuelType,
                                        PowerHP = data.PowerHP,
                                        Source = "PlateProvider",
                                        HitCount = 1
                                    });
                                }
                                catch (Exception cacheEx)
                                {
                                    logger.LogDebug(cacheEx, "Cache VIN depuis plaque non enregistré");
                                }
                            }
                            return (vehicle, false);
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Décodage plaque fournisseur indisponible — stub marché MA");
                }
            }

            // Stub marché marocain : véhicule déterministe par plaque
            var idx = Math.Abs(plateNumber.GetHashCode(StringComparison.Ordinal)) % MoroccoDemoFleet.Length;
            return (MoroccoDemoFleet[idx], true);
        }

        private static VehicleInfo ToVehicleInfo(VinVehicleDto dto) =>
            new(dto.Vin, dto.Make, dto.Model, dto.Year, dto.EngineCode, dto.FuelType, dto.PowerHP);

        private async Task<List<PlateCompatibleProductDto>> FindCompatibleProductsAsync(
            VehicleInfo vehicle, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(vehicle.Make) || string.IsNullOrWhiteSpace(vehicle.Model))
                return new List<PlateCompatibleProductDto>();

            var make = vehicle.Make.Trim();
            var model = vehicle.Model.Trim();
            var makeLower = make.ToLowerInvariant();
            var modelLower = model.ToLowerInvariant();
            var year = vehicle.Year;
            var engine = vehicle.EngineCode?.Trim();
            var max = Math.Clamp(options.Value.MaxProducts, 1, 200);

            var vehicles = storage.SelectAllErpProductVehicles().AsNoTracking()
                .Where(v =>
                    v.Make.ToLower() == makeLower
                    && (v.Model.ToLower() == modelLower || v.Model.ToLower().StartsWith(modelLower)));

            if (year.HasValue)
            {
                vehicles = vehicles.Where(v =>
                    (!v.YearFrom.HasValue || v.YearFrom <= year) &&
                    (!v.YearTo.HasValue || v.YearTo >= year));
            }

            if (!string.IsNullOrWhiteSpace(engine))
            {
                // Soft filter : si aucune pièce avec engine, on retombe sur marque/modèle/année
                var withEngine = await vehicles
                    .Where(v => v.EngineCode == engine)
                    .Select(v => v.ProductId)
                    .Distinct()
                    .Take(max)
                    .ToListAsync(ct);
                if (withEngine.Count > 0)
                    return await MapProductsAsync(withEngine, ct);
            }

            var productIds = await vehicles
                .Select(v => v.ProductId)
                .Distinct()
                .Take(max)
                .ToListAsync(ct);

            return await MapProductsAsync(productIds, ct);
        }

        private async Task<List<PlateCompatibleProductDto>> MapProductsAsync(List<int> productIds, CancellationToken ct)
        {
            if (productIds.Count == 0) return new List<PlateCompatibleProductDto>();

            var products = await storage.SelectAllErpProducts()
                .AsNoTracking()
                .Where(p => productIds.Contains(p.Id) && (p.Archived == null || p.Archived == false))
                .Take(productIds.Count)
                .ToListAsync(ct);

            var images = await storage.SelectAllErpProductImages()
                .AsNoTracking()
                .Where(i => productIds.Contains(i.ProductId) && i.IsMain)
                .Select(i => new { i.ProductId, i.Url })
                .ToListAsync(ct);
            var imageMap = images
                .GroupBy(i => i.ProductId)
                .ToDictionary(g => g.Key, g => g.First().Url);

            return products.Select(p => new PlateCompatibleProductDto(
                p.Id,
                p.ErpProductId,
                p.Name,
                p.Reference,
                p.Brand,
                p.PriceHT,
                p.StockQuantity,
                imageMap.TryGetValue(p.Id, out var url) ? url : p.PicName,
                p.TypeName ?? p.MainTypeName
            )).ToList();
        }

        private async Task SaveHistoryAsync(
            string companyId,
            string plateNumber,
            string? country,
            VehicleInfo vehicle,
            int productsFound,
            string? userId,
            CancellationToken ct)
        {
            var actor = userId
                ?? SalesDocumentAudit.ActorFrom(httpContextAccessor.HttpContext?.User);

            await storage.InsertErpPlateHistoryAsync(new ErpPlateHistory
            {
                CompanyId = companyId,
                PlateNumber = plateNumber,
                Country = country,
                Vin = vehicle.Vin,
                Make = vehicle.Make,
                Model = vehicle.Model,
                Year = vehicle.Year,
                EngineCode = vehicle.EngineCode,
                FuelType = vehicle.FuelType,
                PowerHP = vehicle.PowerHP,
                ProductsFound = productsFound,
                SearchedBy = actor,
                SearchedAt = DateTime.UtcNow
            });
            _ = ct;
        }

        private static PlateScanResultDto MapToResult(
            string plateNumber,
            string? country,
            VehicleInfo vehicle,
            List<PlateCompatibleProductDto> products,
            bool isDemo,
            string? message) =>
            new(
                plateNumber,
                country,
                vehicle.Vin,
                vehicle.Make,
                vehicle.Model,
                vehicle.Year,
                vehicle.EngineCode,
                vehicle.FuelType,
                vehicle.PowerHP,
                isDemo,
                message,
                products);

        private sealed record VehicleInfo(
            string? Vin,
            string? Make,
            string? Model,
            int? Year,
            string? EngineCode,
            string? FuelType,
            int? PowerHP);

        private sealed class ProviderPlateOcrResponse
        {
            public string? PlateNumber { get; set; }
        }

        private sealed class ProviderVehicleResponse
        {
            public string? Vin { get; set; }
            public string? Make { get; set; }
            public string? Model { get; set; }
            public int? Year { get; set; }
            public string? EngineCode { get; set; }
            public string? FuelType { get; set; }
            public int? PowerHP { get; set; }
        }

    }
}
