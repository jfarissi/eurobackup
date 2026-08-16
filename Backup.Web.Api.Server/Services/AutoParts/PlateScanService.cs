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
        private readonly IPlateOcrService plateOcr;
        private readonly IVehicleKTypeResolver kTypeResolver;
        private readonly IKTypeEnrichmentService kTypeEnrichment;
        private readonly IOptions<PlateScanOptions> options;
        private readonly ILogger<PlateScanService> logger;
        private readonly IHttpContextAccessor httpContextAccessor;

        /// <summary>Véhicules fréquents sur le marché marocain (stub démo VIN uniquement).</summary>
        private static readonly VehicleInfo[] MoroccoDemoFleet =
        {
            new("VF1BZ090X12345678", "Dacia", "Logan", 2019, "K7M", "Essence", 90, null),
            new("UU1HSDAGB51234567", "Dacia", "Sandero", 2021, "H4M", "Essence", 90, null),
            new("VF1RJA00X51234567", "Renault", "Clio", 2018, "H5F", "Essence", 90, null),
            new("VF3M45GSYJS123456", "Peugeot", "208", 2020, "EB2", "Essence", 75, null),
            new("MALA351CANM123456", "Hyundai", "i10", 2017, "G3LA", "Essence", 67, null),
            new("JTDBR32E500123456", "Toyota", "Corolla", 2016, "1ZR", "Essence", 132, null),
            new("WVWZZZ1KZAW123456", "Volkswagen", "Polo", 2015, "CFW", "Diesel", 90, null),
            new("VF7SXHMRB81234567", "Citroen", "C3", 2019, "EB2DT", "Essence", 110, null),
        };

        public PlateScanService(
            IStorageBroker storage,
            IHttpClientFactory httpFactory,
            IVinLookupService vinLookup,
            IPlateOcrService plateOcr,
            IVehicleKTypeResolver kTypeResolver,
            IKTypeEnrichmentService kTypeEnrichment,
            IOptions<PlateScanOptions> options,
            ILogger<PlateScanService> logger,
            IHttpContextAccessor httpContextAccessor)
        {
            this.storage = storage;
            this.httpFactory = httpFactory;
            this.vinLookup = vinLookup;
            this.plateOcr = plateOcr;
            this.kTypeResolver = kTypeResolver;
            this.kTypeEnrichment = kTypeEnrichment;
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

            var country = options.Value.DefaultCountry;
            var ocr = await plateOcr.RecognizeAsync(image, country, ct);
            if (string.IsNullOrWhiteSpace(ocr.PlateNumber))
                throw new InvalidOperationException("Impossible de lire la plaque sur l'image.");

            var result = await SearchByPlateAsync(companyId, ocr.PlateNumber, country, userId, ct);
            var message = result.Message;
            if (ocr.IsDemo)
                message = ocr.RawMessage ?? "OCR démo — configurez PlateScan:OcrProvider=PlateRecognizer.";
            else if (!string.IsNullOrWhiteSpace(ocr.RawMessage) && string.IsNullOrWhiteSpace(message))
                message = ocr.RawMessage;

            return result with
            {
                IsDemoData = result.IsDemoData || ocr.IsDemo,
                OcrProvider = ocr.Provider,
                OcrScore = ocr.Score,
                Message = message
            };
        }

        public async Task<PlateScanResultDto> SearchByPlateAsync(
            string companyId, string plateNumber, string? country, string? userId, CancellationToken ct = default)
        {
            var countryCode = NormalizeCountry(country);
            var normalized = NormalizePlate(plateNumber, countryCode);
            if (string.IsNullOrWhiteSpace(normalized))
                throw new InvalidOperationException("Numéro de plaque invalide.");

            // Scénario A : plaque déjà enregistrée localement
            var registered = await storage.SelectErpPlateVehicleAsync(companyId, normalized, countryCode);
            if (registered != null && (!string.IsNullOrWhiteSpace(registered.Make) || !string.IsNullOrWhiteSpace(registered.KType)))
            {
                await storage.TouchErpPlateVehicleHitAsync(registered);
                var vehicle = await EnrichVehicleWithKTypeAsync(FromRegistry(registered), ct);
                if (string.IsNullOrWhiteSpace(registered.KType) && !string.IsNullOrWhiteSpace(vehicle.KType))
                    await UpsertRegistryAsync(companyId, normalized, countryCode, vehicle, registered.Source ?? "VinLink", userId);

                var match = await FindCompatibleProductsAsync(
                    companyId, vehicle, "PlateScan", ct);
                await SaveHistoryAsync(companyId, normalized, countryCode, vehicle, match.Products.Count, userId, ct);
                return MapToResult(
                    normalized, countryCode, vehicle, match,
                    isDemo: false,
                    fromRegistry: true,
                    needsVehicleLink: false,
                    message: BuildVehicleMessage(
                        string.IsNullOrWhiteSpace(vehicle.KType)
                            ? "Véhicule connu (registre plaque local)."
                            : null,
                        match,
                        vehicle));
            }

            // Fournisseur plaque externe (prod)
            var (decoded, isDemo) = await DecodePlateAsync(normalized, countryCode, ct);
            if (!isDemo && !string.IsNullOrWhiteSpace(decoded.Make))
            {
                decoded = await EnrichVehicleWithKTypeAsync(decoded, ct);
                await UpsertRegistryAsync(companyId, normalized, countryCode, decoded, "PlateProvider", userId);
                var match = await FindCompatibleProductsAsync(
                    companyId, decoded, "PlateProvider", ct);
                await SaveHistoryAsync(companyId, normalized, countryCode, decoded, match.Products.Count, userId, ct);
                return MapToResult(
                    normalized, countryCode, decoded, match,
                    isDemo: false,
                    fromRegistry: true,
                    needsVehicleLink: false,
                    message: BuildVehicleMessage(null, match, decoded));
            }

            // Scénario B : plaque inconnue → demander VIN une fois
            var empty = new VehicleInfo(null, null, null, null, null, null, null, null);
            var emptyMatch = new ProductMatchResult(new List<PlateCompatibleProductDto>(), "None", false, false);
            await SaveHistoryAsync(companyId, normalized, countryCode, empty, 0, userId, ct);
            return MapToResult(
                normalized, countryCode, empty, emptyMatch,
                isDemo: false,
                fromRegistry: false,
                needsVehicleLink: true,
                message: "Plaque inconnue. Saisissez le VIN une fois pour l’enregistrer (lien plaque → véhicule).");
        }

        public async Task<PlateScanResultDto> SearchByVinAsync(
            string companyId, string vin, string? userId, CancellationToken ct = default)
        {
            var cleanVin = (vin ?? string.Empty).Trim().ToUpperInvariant();
            if (cleanVin.Length != 17)
                throw new InvalidOperationException("Le VIN doit contenir exactement 17 caractères.");

            var lookup = await vinLookup.ResolveAsync(cleanVin, companyId, ct);
            var vehicle = await EnrichVehicleWithKTypeAsync(ToVehicleInfo(lookup.Vehicle), ct);
            var match = await FindCompatibleProductsAsync(companyId, vehicle, "VinLookup", ct);
            await SaveHistoryAsync(companyId, cleanVin, null, vehicle, match.Products.Count, userId, ct);

            var message = lookup.Message
                ?? (lookup.FromCache
                    ? "VIN résolu depuis le cache local."
                    : lookup.IsDemo
                        ? "Décodage VIN en mode démo (NHTSA indisponible)."
                        : null);
            message = BuildVehicleMessage(message, match, vehicle);

            return MapToResult(cleanVin, null, vehicle, match, lookup.IsDemo, false, false, message);
        }

        public async Task<PlateScanResultDto> LinkPlateToVinAsync(
            string companyId, LinkPlateVinRequest request, string? userId, CancellationToken ct = default)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Plate))
                throw new InvalidOperationException("Plaque requise.");
            if (string.IsNullOrWhiteSpace(request.Vin) || request.Vin.Trim().Length != 17)
                throw new InvalidOperationException("Le VIN doit contenir exactement 17 caractères.");

            var countryCode = NormalizeCountry(request.Country);
            var normalized = NormalizePlate(request.Plate, countryCode);
            if (string.IsNullOrWhiteSpace(normalized))
                throw new InvalidOperationException("Numéro de plaque invalide.");

            var lookup = await vinLookup.ResolveAsync(request.Vin.Trim(), companyId, ct);
            var vehicle = await EnrichVehicleWithKTypeAsync(ToVehicleInfo(lookup.Vehicle), ct);
            if (string.IsNullOrWhiteSpace(vehicle.Make) && string.IsNullOrWhiteSpace(vehicle.KType))
                throw new InvalidOperationException("Impossible de résoudre le véhicule pour ce VIN.");

            await UpsertRegistryAsync(companyId, normalized, countryCode, vehicle, "VinLink", userId);
            var match = await FindCompatibleProductsAsync(companyId, vehicle, "VinLink", ct);
            await SaveHistoryAsync(companyId, normalized, countryCode, vehicle, match.Products.Count, userId, ct);

            var msg = lookup.IsDemo
                ? "Plaque associée (VIN démo). Le lien est enregistré pour les prochaines recherches."
                : "Plaque associée au véhicule. Prochaine recherche instantanée via le registre local.";
            msg = BuildVehicleMessage(msg, match, vehicle);

            return MapToResult(
                normalized, countryCode, vehicle, match,
                isDemo: lookup.IsDemo,
                fromRegistry: true,
                needsVehicleLink: false,
                message: msg);
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
                                data.EngineCode, data.FuelType, data.PowerHP, data.KType);
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

            // Stub marché marocain : uniquement pour tests VIN, pas pour figer une plaque
            var idx = Math.Abs(plateNumber.GetHashCode(StringComparison.Ordinal)) % MoroccoDemoFleet.Length;
            return (MoroccoDemoFleet[idx], true);
        }

        private static VehicleInfo ToVehicleInfo(VinVehicleDto dto) =>
            new(dto.Vin, dto.Make, dto.Model, dto.Year, dto.EngineCode, dto.FuelType, dto.PowerHP, dto.ExternalVehicleId);

        private static VehicleInfo FromRegistry(ErpPlateVehicle row) =>
            new(row.Vin, row.Make, row.Model, row.Year, row.EngineCode, row.FuelType, row.PowerHP, row.KType);

        private async Task<VehicleInfo> EnrichVehicleWithKTypeAsync(VehicleInfo vehicle, CancellationToken ct)
        {
            if (!string.IsNullOrWhiteSpace(vehicle.KType))
                return vehicle;

            var resolved = await kTypeResolver.ResolveAsync(
                vehicle.Make, vehicle.Model, vehicle.Year, vehicle.EngineCode, ct);
            return string.IsNullOrWhiteSpace(resolved)
                ? vehicle
                : vehicle with { KType = resolved };
        }

        private async Task UpsertRegistryAsync(
            string companyId,
            string plateNumber,
            string country,
            VehicleInfo vehicle,
            string source,
            string? userId)
        {
            var actor = userId
                ?? SalesDocumentAudit.ActorFrom(httpContextAccessor.HttpContext?.User);
            await storage.UpsertErpPlateVehicleAsync(new ErpPlateVehicle
            {
                CompanyId = companyId,
                PlateNumber = plateNumber,
                Country = country,
                Vin = vehicle.Vin,
                KType = vehicle.KType,
                Make = vehicle.Make,
                Model = vehicle.Model,
                Year = vehicle.Year,
                EngineCode = vehicle.EngineCode,
                FuelType = vehicle.FuelType,
                PowerHP = vehicle.PowerHP,
                Source = source,
                CreatedBy = actor,
                UpdatedBy = actor
            });
        }

        private async Task<ProductMatchResult> FindCompatibleProductsAsync(
            string companyId,
            VehicleInfo vehicle,
            string enrichmentSource,
            CancellationToken ct)
        {
            var max = Math.Clamp(options.Value.MaxProducts, 1, 200);
            vehicle = await EnrichVehicleWithKTypeAsync(vehicle, ct);

            var kTypeInCatalog = false;
            var enrichmentQueued = false;
            var kTypeSyncInProgress = false;
            var needsCategorySelection = false;
            string? enrichmentMessage = null;

            // Priorité K-Type (passerelle TecDoc) — match insensible à la casse
            if (!string.IsNullOrWhiteSpace(vehicle.KType))
            {
                var k = vehicle.KType.Trim();
                kTypeInCatalog = await kTypeEnrichment.ExistsInCatalogAsync(k, ct);
                if (kTypeInCatalog)
                {
                    var kLower = k.ToLowerInvariant();
                    var byKType = await storage.SelectAllErpProductVehicles().AsNoTracking()
                        .Where(v => v.KType != null && v.KType.ToLower() == kLower)
                        .Select(v => v.ProductId)
                        .Distinct()
                        .Take(max)
                        .ToListAsync(ct);
                    var products = await MapProductsAsync(byKType, ct, vehicle);
                    return new ProductMatchResult(products, "KType", true, false);
                }

                var enrichResult = await kTypeEnrichment.EnrichIfMissingAsync(
                    k,
                    new KTypeEnrichmentContext(
                        companyId,
                        vehicle.Vin,
                        vehicle.Make,
                        vehicle.Model,
                        vehicle.Year,
                        vehicle.EngineCode,
                        enrichmentSource),
                    ct);
                enrichmentQueued = enrichResult.Queued;
                needsCategorySelection = enrichResult.NeedsCategorySelection;

                if (enrichResult.SyncInProgress)
                {
                    // Continuer en fallback marque/modèle pendant l'import K-Type
                    // (évite 0 pièce alors que le catalogue a déjà des Compass/JEEP proches).
                    enrichmentMessage = enrichResult.Message;
                    enrichmentQueued = true;
                    kTypeSyncInProgress = true;
                }
                else if (enrichResult.CatalogSynced && enrichResult.ProductsImported > 0)
                {
                    kTypeInCatalog = await kTypeEnrichment.ExistsInCatalogAsync(k, ct);
                    if (kTypeInCatalog)
                    {
                        var kLower = k.ToLowerInvariant();
                        var byKType = await storage.SelectAllErpProductVehicles().AsNoTracking()
                            .Where(v => v.KType != null && v.KType.ToLower() == kLower)
                            .Select(v => v.ProductId)
                            .Distinct()
                            .Take(max)
                            .ToListAsync(ct);
                        var syncedProducts = await MapProductsAsync(byKType, ct, vehicle);
                        return new ProductMatchResult(
                            syncedProducts,
                            "KType",
                            true,
                            enrichmentQueued,
                            enrichResult.Message);
                    }
                }
                else if (!string.IsNullOrWhiteSpace(enrichResult.Message))
                {
                    enrichmentMessage = enrichResult.Message;
                }
            }

            if (string.IsNullOrWhiteSpace(vehicle.Make) || string.IsNullOrWhiteSpace(vehicle.Model))
                return new ProductMatchResult(
                    new List<PlateCompatibleProductDto>(),
                    "None",
                    kTypeInCatalog,
                    enrichmentQueued,
                    enrichmentMessage,
                    kTypeSyncInProgress,
                    needsCategorySelection);

            var make = vehicle.Make.Trim();
            var model = vehicle.Model.Trim();
            var makeAliases = VehicleMakeAliases.Expand(make);
            var modelLower = model.ToLowerInvariant();
            var year = vehicle.Year;
            var engine = vehicle.EngineCode?.Trim();

            var vehicles = storage.SelectAllErpProductVehicles().AsNoTracking()
                .Where(v =>
                    makeAliases.Contains(v.Make.ToLower())
                    && (v.Model.ToLower() == modelLower || v.Model.ToLower().StartsWith(modelLower)));

            if (year.HasValue)
            {
                if (year.Value < 1950 || year.Value > 2035)
                {
                    return new ProductMatchResult(
                        new List<PlateCompatibleProductDto>(),
                        "None",
                        kTypeInCatalog,
                        enrichmentQueued,
                        enrichmentMessage,
                        kTypeSyncInProgress,
                        needsCategorySelection);
                }

                var maxOpenYear = DateTime.UtcNow.Year + 1;
                vehicles = vehicles.Where(v =>
                    (v.YearFrom.HasValue || v.YearTo.HasValue) &&
                    (!v.YearFrom.HasValue || v.YearFrom <= year) &&
                    (v.YearTo.HasValue
                        ? v.YearTo >= year
                        : year <= maxOpenYear));
            }

            if (!string.IsNullOrWhiteSpace(engine))
            {
                var engineLower = engine.ToLowerInvariant();
                var withEngine = await vehicles
                    .Where(v => v.EngineCode != null && v.EngineCode.ToLower() == engineLower)
                    .Select(v => v.ProductId)
                    .Distinct()
                    .Take(max)
                    .ToListAsync(ct);
                if (withEngine.Count > 0)
                {
                    var engineProducts = await MapProductsAsync(withEngine, ct, vehicle);
                    return new ProductMatchResult(
                        engineProducts, "MakeModel", kTypeInCatalog, enrichmentQueued, enrichmentMessage, kTypeSyncInProgress, needsCategorySelection);
                }
            }

            var productIds = await vehicles
                .Select(v => v.ProductId)
                .Distinct()
                .Take(max)
                .ToListAsync(ct);

            var fallbackProducts = await MapProductsAsync(productIds, ct, vehicle);
            return new ProductMatchResult(
                fallbackProducts,
                fallbackProducts.Count > 0 ? "MakeModel" : "None",
                kTypeInCatalog,
                enrichmentQueued,
                enrichmentMessage,
                kTypeSyncInProgress,
                needsCategorySelection);
        }

        private async Task<List<PlateCompatibleProductDto>> MapProductsAsync(
            List<int> productIds, CancellationToken ct, VehicleInfo? vehicle = null)
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

            var fitmentQuery = storage.SelectAllErpProductVehicles().AsNoTracking()
                .Where(v => productIds.Contains(v.ProductId));
            if (!string.IsNullOrWhiteSpace(vehicle?.KType))
            {
                var k = vehicle.KType.Trim().ToLowerInvariant();
                fitmentQuery = fitmentQuery.Where(v => v.KType != null && v.KType.ToLower() == k);
            }
            else if (!string.IsNullOrWhiteSpace(vehicle?.Make))
            {
                var makeAliases = VehicleMakeAliases.Expand(vehicle.Make);
                fitmentQuery = fitmentQuery.Where(v => makeAliases.Contains(v.Make.ToLower()));
            }

            var fitments = await fitmentQuery.ToListAsync(ct);
            var fitmentMap = fitments
                .GroupBy(v => v.ProductId)
                .ToDictionary(g => g.Key, g => g.First());

            var oemCounts = await storage.SelectAllErpOemCrossReferences().AsNoTracking()
                .Where(o => productIds.Contains(o.ProductId))
                .GroupBy(o => o.ProductId)
                .Select(g => new { ProductId = g.Key, Count = g.Count() })
                .ToListAsync(ct);
            var oemMap = oemCounts.ToDictionary(x => x.ProductId, x => x.Count);

            return products.Select(p =>
            {
                fitmentMap.TryGetValue(p.Id, out var fit);
                oemMap.TryGetValue(p.Id, out var oemCount);
                return new PlateCompatibleProductDto(
                    p.Id,
                    p.ErpProductId,
                    p.Name,
                    p.Reference,
                    p.Brand,
                    p.PriceHT,
                    p.StockQuantity,
                    imageMap.TryGetValue(p.Id, out var url) ? url : p.PicName,
                    p.TypeName ?? p.MainTypeName,
                    fit?.Make ?? vehicle?.Make,
                    fit?.Model ?? vehicle?.Model,
                    fit?.TypeName,
                    fit?.YearFrom ?? vehicle?.Year,
                    fit?.YearTo ?? vehicle?.Year,
                    fit?.EngineCode ?? vehicle?.EngineCode,
                    fit?.KType ?? vehicle?.KType,
                    fit?.FuelType ?? vehicle?.FuelType,
                    oemCount > 0 ? oemCount : null);
            }).ToList();
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

        private static string? BuildVehicleMessage(
            string? baseMessage, ProductMatchResult match, VehicleInfo vehicle)
        {
            if (!string.IsNullOrWhiteSpace(match.EnrichmentMessage))
            {
                baseMessage = string.IsNullOrWhiteSpace(baseMessage)
                    ? match.EnrichmentMessage
                    : $"{baseMessage.TrimEnd('.')} {match.EnrichmentMessage}";
            }

            if (string.IsNullOrWhiteSpace(vehicle.KType))
                return baseMessage;

            var k = vehicle.KType.Trim();
            if (string.Equals(match.MatchMode, "KType", StringComparison.OrdinalIgnoreCase))
            {
                var exact = $"K-Type {k} — correspondance catalogue exacte.";
                return string.IsNullOrWhiteSpace(baseMessage) ? exact : $"{baseMessage.TrimEnd('.')} {exact}";
            }

            if (!match.KTypeInCatalog)
            {
                var missing = match.NeedsCategorySelection
                    ? $"K-Type {k} identifié — choisissez les catégories RapidAPI à importer."
                    : match.KTypeEnrichmentQueued
                        ? $"K-Type {k} identifié — absent du catalogue (enrichissement planifié)."
                        : $"K-Type {k} identifié — absent du catalogue.";
                if (string.Equals(match.MatchMode, "MakeModel", StringComparison.OrdinalIgnoreCase))
                    missing += " Résultats approximatifs par marque/modèle.";
                return string.IsNullOrWhiteSpace(baseMessage) ? missing : $"{baseMessage.TrimEnd('.')} {missing}";
            }

            return baseMessage;
        }

        private static PlateScanResultDto MapToResult(
            string plateNumber,
            string? country,
            VehicleInfo vehicle,
            ProductMatchResult match,
            bool isDemo,
            bool fromRegistry,
            bool needsVehicleLink,
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
                vehicle.KType,
                isDemo,
                fromRegistry,
                needsVehicleLink,
                OcrProvider: null,
                OcrScore: null,
                message,
                match.MatchMode,
                match.KTypeInCatalog,
                match.KTypeEnrichmentQueued,
                match.Products,
                match.KTypeSyncInProgress,
                match.NeedsCategorySelection);

        private sealed record ProductMatchResult(
            List<PlateCompatibleProductDto> Products,
            string MatchMode,
            bool KTypeInCatalog,
            bool KTypeEnrichmentQueued,
            string? EnrichmentMessage = null,
            bool KTypeSyncInProgress = false,
            bool NeedsCategorySelection = false);

        private sealed record VehicleInfo(
            string? Vin,
            string? Make,
            string? Model,
            int? Year,
            string? EngineCode,
            string? FuelType,
            int? PowerHP,
            string? KType);

        private sealed class ProviderVehicleResponse
        {
            public string? Vin { get; set; }
            public string? Make { get; set; }
            public string? Model { get; set; }
            public int? Year { get; set; }
            public string? EngineCode { get; set; }
            public string? FuelType { get; set; }
            public int? PowerHP { get; set; }
            public string? KType { get; set; }
        }

    }
}
