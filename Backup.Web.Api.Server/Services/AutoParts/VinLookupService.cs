using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Models.Catalog;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Backup.Web.Api.Server.Services.AutoParts
{
    public class VinLookupService : IVinLookupService
    {
        private readonly IStorageBroker storage;
        private readonly IHttpClientFactory httpFactory;
        private readonly IOptions<RapidApiOptions> rapidOptions;
        private readonly IOptions<PlateScanOptions> plateOptions;
        private readonly ILogger<VinLookupService> logger;

        public VinLookupService(
            IStorageBroker storage,
            IHttpClientFactory httpFactory,
            IOptions<RapidApiOptions> rapidOptions,
            IOptions<PlateScanOptions> plateOptions,
            ILogger<VinLookupService> logger)
        {
            this.storage = storage;
            this.httpFactory = httpFactory;
            this.rapidOptions = rapidOptions;
            this.plateOptions = plateOptions;
            this.logger = logger;
        }

        public async Task<VinLookupResult> ResolveAsync(
            string vin, string? companyId = null, CancellationToken ct = default)
        {
            var clean = NormalizeVin(vin);
            if (clean.Length != 17)
                throw new InvalidOperationException("Le VIN doit contenir exactement 17 caractères.");

            var cached = await storage.SelectErpVinVehicleByVinAsync(clean);
            if (cached != null && !string.IsNullOrWhiteSpace(cached.Make))
            {
                await storage.TouchErpVinVehicleHitAsync(cached);
                return new VinLookupResult(
                    ToDto(cached),
                    cached.Source,
                    FromCache: true,
                    IsDemo: string.Equals(cached.Source, "Demo", StringComparison.OrdinalIgnoreCase),
                    Message: "VIN résolu depuis le cache local.");
            }

            // NHTSA d'abord (gratuit, sans RapidAPI). RapidAPI = opt-in uniquement.
            var nhtsa = await TryNhtsaAsync(clean, ct);
            if (nhtsa != null)
            {
                await PersistAsync(nhtsa, companyId, "Nhtsa", ct);
                return new VinLookupResult(nhtsa, "Nhtsa", false, false);
            }

            var rapid = await TryRapidApiAsync(clean, ct);
            if (rapid != null)
            {
                await PersistAsync(rapid, companyId, "RapidApi", ct);
                return new VinLookupResult(rapid, "RapidApi", false, false);
            }

            var demo = DemoFromVin(clean);
            return new VinLookupResult(
                demo,
                "Demo",
                false,
                true,
                "Décodage VIN en mode démo (NHTSA indisponible ; RapidAPI VIN désactivé).");
        }

        private async Task PersistAsync(
            VinVehicleDto vehicle, string? companyId, string source, CancellationToken ct)
        {
            _ = ct;
            try
            {
                await storage.UpsertErpVinVehicleAsync(new ErpVinVehicle
                {
                    CompanyId = string.IsNullOrWhiteSpace(companyId) ? null : companyId,
                    Vin = vehicle.Vin,
                    Make = vehicle.Make,
                    Model = vehicle.Model,
                    Year = vehicle.Year,
                    EngineCode = vehicle.EngineCode,
                    FuelType = vehicle.FuelType,
                    PowerHP = vehicle.PowerHP,
                    ExternalVehicleId = vehicle.ExternalVehicleId,
                    ExternalModelId = vehicle.ExternalModelId,
                    ExternalManufacturerId = vehicle.ExternalManufacturerId,
                    Source = source,
                    HitCount = 1
                });
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Impossible d'enregistrer le cache VIN pour {Vin}", vehicle.Vin);
            }
        }

        private async Task<VinVehicleDto?> TryRapidApiAsync(string vin, CancellationToken ct)
        {
            var cfg = rapidOptions.Value;
            if (!cfg.Enabled || !cfg.EnableVinLookup || string.IsNullOrWhiteSpace(cfg.ApiKey))
                return null;

            var paths = cfg.VinCheckPaths?
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .ToList()
                ?? new List<string>();
            if (paths.Count == 0) return null;

            var client = httpFactory.CreateClient("RapidApi");
            foreach (var template in paths)
            {
                try
                {
                    var relative = template
                        .Replace("{vin}", Uri.EscapeDataString(vin), StringComparison.OrdinalIgnoreCase)
                        .Replace("{vinNo}", Uri.EscapeDataString(vin), StringComparison.OrdinalIgnoreCase)
                        .TrimStart('/');
                    var url = $"{cfg.BaseUrl.TrimEnd('/')}/{relative}";

                    using var request = new HttpRequestMessage(HttpMethod.Get, url);
                    request.Headers.TryAddWithoutValidation("X-RapidAPI-Key", cfg.ApiKey);
                    request.Headers.TryAddWithoutValidation("X-RapidAPI-Host", cfg.Host);
                    request.Headers.TryAddWithoutValidation("Accept", "application/json");

                    using var response = await client.SendAsync(request, ct);
                    if (!response.IsSuccessStatusCode)
                    {
                        logger.LogDebug(
                            "RapidAPI VIN {Path} → {Status}",
                            relative,
                            (int)response.StatusCode);
                        continue;
                    }

                    await using var stream = await response.Content.ReadAsStreamAsync(ct);
                    using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
                    var parsed = ParseRapidApiVehicle(doc.RootElement, vin);
                    if (parsed != null && !string.IsNullOrWhiteSpace(parsed.Make))
                    {
                        logger.LogInformation("VIN {Vin} résolu via RapidAPI ({Path})", vin, relative);
                        return parsed;
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "RapidAPI VIN path failed for {Vin}", vin);
                }
            }

            return null;
        }

        private async Task<VinVehicleDto?> TryNhtsaAsync(string vin, CancellationToken ct)
        {
            if (!plateOptions.Value.EnableNhtsaVin) return null;

            try
            {
                var client = httpFactory.CreateClient("PlateScan");
                var url = $"{plateOptions.Value.NhtsaVinUrl.TrimEnd('/')}/{Uri.EscapeDataString(vin)}?format=json";
                var data = await client.GetFromJsonAsync<NhtsaVinResponse>(url, ct);
                var result = data?.Results?.FirstOrDefault();
                if (result == null || string.IsNullOrWhiteSpace(result.Make))
                    return null;

                int? year = int.TryParse(result.ModelYear, NumberStyles.Integer, CultureInfo.InvariantCulture, out var y)
                    ? y : null;

                return new VinVehicleDto(
                    vin,
                    result.Make,
                    result.Model,
                    year,
                    result.EngineCode,
                    result.FuelTypePrimary,
                    null,
                    null,
                    null,
                    null);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "NHTSA VIN decode failed for {Vin}", vin);
                return null;
            }
        }

        internal static VinVehicleDto? ParseRapidApiVehicle(JsonElement root, string vin)
        {
            // Certains payloads wrappent sous data / result / vehicle / vehicles[0]
            var candidates = new List<JsonElement> { root };
            foreach (var wrap in new[] { "data", "result", "vehicle", "Vehicle", "response" })
            {
                if (root.ValueKind == JsonValueKind.Object &&
                    root.TryGetProperty(wrap, out var nested) &&
                    nested.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
                {
                    candidates.Add(nested);
                }
            }

            foreach (var c in candidates)
            {
                if (c.ValueKind == JsonValueKind.Array && c.GetArrayLength() > 0)
                {
                    var fromItem = ExtractVehicleFields(c[0], vin);
                    if (fromItem != null) return fromItem;
                }

                if (c.ValueKind == JsonValueKind.Object)
                {
                    // matchingVehicles / vehicles / list
                    foreach (var arrName in new[] { "matchingVehicles", "vehicles", "list", "data" })
                    {
                        if (c.TryGetProperty(arrName, out var arr) &&
                            arr.ValueKind == JsonValueKind.Array &&
                            arr.GetArrayLength() > 0)
                        {
                            var fromArr = ExtractVehicleFields(arr[0], vin);
                            if (fromArr != null) return fromArr;
                        }
                    }

                    var direct = ExtractVehicleFields(c, vin);
                    if (direct != null) return direct;
                }
            }

            return null;
        }

        private static VinVehicleDto? ExtractVehicleFields(JsonElement el, string vin)
        {
            if (el.ValueKind != JsonValueKind.Object) return null;

            var make = FirstString(el,
                "manuName", "manufacturerName", "ManufacturerName", "make", "Make", "brand", "Brand");
            var model = FirstString(el,
                "modelName", "ModelName", "model", "MakeModelName", "vehicleModel");
            if (string.IsNullOrWhiteSpace(make) && string.IsNullOrWhiteSpace(model))
                return null;

            var year = FirstInt(el,
                "year", "Year", "modelYear", "ModelYear", "yearOfConstrFrom", "constructionYear");
            var engine = FirstString(el,
                "engineCode", "EngineCode", "engine", "motorCode", "MotorCode");
            var fuel = FirstString(el,
                "fuelType", "FuelType", "fuel", "Fuel");
            var power = FirstInt(el,
                "powerHP", "PowerHP", "powerHp", "power", "Power", "powerKwToHp");
            var vehicleId = FirstString(el,
                "vehicleId", "VehicleId", "carId", "ktype", "KType", "typeId");
            var modelId = FirstString(el, "modelId", "ModelId", "modId");
            var manuId = FirstString(el, "manuId", "manufacturerId", "ManufacturerId", "makeId");

            return new VinVehicleDto(
                vin,
                make,
                model,
                year,
                engine,
                fuel,
                power,
                vehicleId,
                modelId,
                manuId);
        }

        private static string? FirstString(JsonElement el, params string[] names)
        {
            foreach (var name in names)
            {
                if (!el.TryGetProperty(name, out var p)) continue;
                if (p.ValueKind == JsonValueKind.String)
                {
                    var s = p.GetString();
                    if (!string.IsNullOrWhiteSpace(s)) return s.Trim();
                }
                if (p.ValueKind is JsonValueKind.Number)
                    return p.ToString();
            }
            return null;
        }

        private static int? FirstInt(JsonElement el, params string[] names)
        {
            foreach (var name in names)
            {
                if (!el.TryGetProperty(name, out var p)) continue;
                if (p.ValueKind == JsonValueKind.Number && p.TryGetInt32(out var n))
                    return n;
                if (p.ValueKind == JsonValueKind.String &&
                    int.TryParse(p.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
                    return parsed;
            }
            return null;
        }

        private static VinVehicleDto ToDto(ErpVinVehicle row) =>
            new(
                row.Vin,
                row.Make,
                row.Model,
                row.Year,
                row.EngineCode,
                row.FuelType,
                row.PowerHP,
                row.ExternalVehicleId,
                row.ExternalModelId,
                row.ExternalManufacturerId);

        private static string NormalizeVin(string? vin) =>
            (vin ?? string.Empty).Trim().ToUpperInvariant();

        private static VinVehicleDto DemoFromVin(string vin)
        {
            var fleet = new[]
            {
                ("Dacia", "Logan", 2019, "K7M", "Essence", 90),
                ("Dacia", "Sandero", 2021, "H4M", "Essence", 90),
                ("Renault", "Clio", 2018, "H5F", "Essence", 90),
                ("Peugeot", "208", 2020, "EB2", "Essence", 75),
                ("Hyundai", "i10", 2017, "G3LA", "Essence", 67),
                ("Toyota", "Corolla", 2016, "1ZR", "Essence", 132),
                ("Volkswagen", "Polo", 2015, "CFW", "Diesel", 90),
                ("Citroen", "C3", 2019, "EB2DT", "Essence", 110),
            };
            var idx = Math.Abs(vin.GetHashCode(StringComparison.Ordinal)) % fleet.Length;
            var d = fleet[idx];
            return new VinVehicleDto(vin, d.Item1, d.Item2, d.Item3, d.Item4, d.Item5, d.Item6, null, null, null);
        }

        private sealed class NhtsaVinResponse
        {
            public List<NhtsaResult>? Results { get; set; }
        }

        private sealed class NhtsaResult
        {
            [JsonPropertyName("Make")] public string? Make { get; set; }
            [JsonPropertyName("Model")] public string? Model { get; set; }
            [JsonPropertyName("ModelYear")] public string? ModelYear { get; set; }
            [JsonPropertyName("EngineCode")] public string? EngineCode { get; set; }
            [JsonPropertyName("FuelTypePrimary")] public string? FuelTypePrimary { get; set; }
        }
    }
}
