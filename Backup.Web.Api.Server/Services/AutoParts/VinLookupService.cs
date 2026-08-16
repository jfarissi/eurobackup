using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Models.Catalog;
using Microsoft.EntityFrameworkCore;
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
                var dto = ToDto(cached);
                dto = await TryEnrichWithRapidApiAsync(clean, dto, companyId, ct);
                dto = await EnrichMissingFuelAsync(dto, companyId, ct);
                return new VinLookupResult(
                    dto,
                    dto.ExternalVehicleId != cached.ExternalVehicleId && !string.IsNullOrWhiteSpace(dto.ExternalVehicleId)
                        ? "RapidApi"
                        : cached.Source,
                    FromCache: true,
                    IsDemo: string.Equals(cached.Source, "Demo", StringComparison.OrdinalIgnoreCase),
                    Message: "VIN résolu depuis le cache local.");
            }

            // NHTSA d'abord (gratuit). RapidAPI si activé et K-Type encore manquant.
            var nhtsa = await TryNhtsaAsync(clean, ct);
            if (nhtsa != null)
            {
                nhtsa = await TryEnrichWithRapidApiAsync(clean, nhtsa, companyId, ct);
                nhtsa = await EnrichMissingFuelAsync(nhtsa, companyId, ct);
                var source = !string.IsNullOrWhiteSpace(nhtsa.ExternalVehicleId) ? "RapidApi" : "Nhtsa";
                await PersistAsync(nhtsa, companyId, source, ct);
                return new VinLookupResult(nhtsa, source, false, false);
            }

            var rapid = await TryRapidApiAsync(clean, ct);
            if (rapid != null)
            {
                rapid = await EnrichMissingFuelAsync(rapid, companyId, ct);
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
                    FuelType = FuelTypeNormalizer.Normalize(vehicle.FuelType) ?? vehicle.FuelType,
                    PowerHP = vehicle.PowerHP,
                    ExternalVehicleId = vehicle.ExternalVehicleId,
                    ExternalModelId = vehicle.ExternalModelId,
                    ExternalManufacturerId = vehicle.ExternalManufacturerId,
                    Source = source,
                    HitCount = 1
                });
                var fuel = FuelTypeNormalizer.Normalize(vehicle.FuelType) ?? vehicle.FuelType;
                if (!string.IsNullOrWhiteSpace(vehicle.ExternalVehicleId) &&
                    !string.IsNullOrWhiteSpace(fuel))
                {
                    var n = await storage.FillMissingErpProductVehicleFuelAsync(
                        vehicle.ExternalVehicleId, fuel, ct);
                    if (n > 0)
                    {
                        logger.LogInformation(
                            "Fuel {Fuel} reporté sur {Count} ligne(s) ErpProductVehicles K-Type {KType}",
                            fuel, n, vehicle.ExternalVehicleId);
                    }
                }
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
            VinVehicleDto? merged = null;
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
                    if (parsed == null ||
                        (string.IsNullOrWhiteSpace(parsed.Make) &&
                         string.IsNullOrWhiteSpace(parsed.ExternalVehicleId) &&
                         string.IsNullOrWhiteSpace(parsed.FuelType)))
                        continue;

                    merged = merged == null ? parsed : MergeVinVehicle(merged, parsed);
                    logger.LogInformation("VIN {Vin} enrichi via RapidAPI ({Path})", vin, relative);
                    if (!string.IsNullOrWhiteSpace(merged.ExternalVehicleId) &&
                        !string.IsNullOrWhiteSpace(merged.FuelType))
                        return merged;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "RapidAPI VIN path failed for {Vin}", vin);
                }
            }

            return merged;
        }

        /// <summary>
        /// Complète un décodage cache/NHTSA avec RapidAPI si le K-Type ou le carburant manque.
        /// </summary>
        private async Task<VinVehicleDto> TryEnrichWithRapidApiAsync(
            string vin, VinVehicleDto current, string? companyId, CancellationToken ct)
        {
            var needsKType = string.IsNullOrWhiteSpace(current.ExternalVehicleId);
            var needsFuel = string.IsNullOrWhiteSpace(current.FuelType);
            if (!needsKType && !needsFuel)
                return current;

            var rapid = await TryRapidApiAsync(vin, ct);
            if (rapid == null)
                return current;

            var merged = MergeVinVehicle(current, rapid);
            if (!string.IsNullOrWhiteSpace(merged.ExternalVehicleId) ||
                !string.Equals(merged.FuelType, current.FuelType, StringComparison.OrdinalIgnoreCase))
            {
                await PersistAsync(merged, companyId, "RapidApi", ct);
                if (!string.IsNullOrWhiteSpace(merged.ExternalVehicleId) && needsKType)
                {
                    logger.LogInformation(
                        "VIN {Vin} enrichi avec K-Type {KType} via RapidAPI",
                        vin,
                        merged.ExternalVehicleId);
                }
            }

            return merged;
        }

        private async Task<VinVehicleDto> EnrichMissingFuelAsync(
            VinVehicleDto dto, string? companyId, CancellationToken ct)
        {
            var fuel = FuelTypeNormalizer.Normalize(dto.FuelType)
                       ?? FuelTypeNormalizer.FromEngineCode(dto.EngineCode)
                       ?? FuelTypeNormalizer.FromText(dto.Model);

            var modelId = dto.ExternalModelId;
            if (!string.IsNullOrWhiteSpace(dto.ExternalVehicleId))
            {
                var k = dto.ExternalVehicleId.Trim();
                try
                {
                    var rows = await storage.SelectAllErpProductVehicles()
                        .AsNoTracking()
                        .Where(v => v.KType == k)
                        .Select(v => new { v.FuelType, v.TypeName, v.EngineCode, v.ExternalModelId, v.RawJson })
                        .Take(25)
                        .ToListAsync(ct);

                    foreach (var row in rows)
                    {
                        if (string.IsNullOrWhiteSpace(modelId) && !string.IsNullOrWhiteSpace(row.ExternalModelId))
                            modelId = row.ExternalModelId;
                        fuel ??= FuelTypeNormalizer.Normalize(row.FuelType)
                                 ?? FuelTypeNormalizer.FromText(row.TypeName)
                                 ?? FuelTypeNormalizer.FromEngineCode(row.EngineCode);
                        if (string.IsNullOrWhiteSpace(fuel) && !string.IsNullOrWhiteSpace(row.RawJson))
                            fuel = TryFuelFromRawJson(row.RawJson);
                        if (!string.IsNullOrWhiteSpace(fuel) && !string.IsNullOrWhiteSpace(modelId))
                            break;
                    }
                }
                catch (Exception ex)
                {
                    logger.LogDebug(ex, "Enrichissement carburant catalogue ignoré pour K-Type {KType}", k);
                }
            }

            if (string.IsNullOrWhiteSpace(modelId))
                modelId = await TryModelIdFromManufacturerModelsAsync(dto, ct);

            if (string.IsNullOrWhiteSpace(fuel))
            {
                var forTypes = string.Equals(modelId, dto.ExternalModelId, StringComparison.Ordinal)
                    ? dto
                    : dto with { ExternalModelId = modelId };
                fuel = await TryFuelFromVehicleTypesAsync(forTypes, ct);
            }

            if (string.IsNullOrWhiteSpace(fuel)
                && string.Equals(modelId, dto.ExternalModelId, StringComparison.Ordinal))
                return dto;

            var updated = dto with
            {
                FuelType = fuel ?? dto.FuelType,
                ExternalModelId = modelId ?? dto.ExternalModelId
            };
            if (string.Equals(updated.FuelType, dto.FuelType, StringComparison.Ordinal)
                && string.Equals(updated.ExternalModelId, dto.ExternalModelId, StringComparison.Ordinal))
                return dto;

            await PersistAsync(updated, companyId, string.IsNullOrWhiteSpace(dto.ExternalVehicleId) ? "Nhtsa" : "RapidApi", ct);
            if (!string.IsNullOrWhiteSpace(fuel))
            {
                logger.LogInformation(
                    "VIN {Vin} carburant {Fuel} (K-Type {KType}, modelId {ModelId})",
                    dto.Vin, fuel, dto.ExternalVehicleId, modelId);
            }
            return updated;
        }

        private static VinVehicleDto MergeVinVehicle(VinVehicleDto primary, VinVehicleDto rapid) =>
            new(
                primary.Vin,
                Coalesce(primary.Make, rapid.Make),
                Coalesce(primary.Model, rapid.Model),
                primary.Year ?? rapid.Year,
                Coalesce(primary.EngineCode, rapid.EngineCode),
                FuelTypeNormalizer.Normalize(Coalesce(primary.FuelType, rapid.FuelType)),
                primary.PowerHP ?? rapid.PowerHP,
                Coalesce(rapid.ExternalVehicleId, primary.ExternalVehicleId),
                Coalesce(rapid.ExternalModelId, primary.ExternalModelId),
                Coalesce(rapid.ExternalManufacturerId, primary.ExternalManufacturerId));

        private static string? Coalesce(string? a, string? b) =>
            string.IsNullOrWhiteSpace(a) ? (string.IsNullOrWhiteSpace(b) ? null : b.Trim()) : a.Trim();

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
                    FuelTypeNormalizer.Normalize(result.FuelTypePrimary),
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
            var decoderV5 = ParseDecoderV5Payload(root, vin);
            if (decoderV5 != null &&
                (!string.IsNullOrWhiteSpace(decoderV5.FuelType) ||
                 !string.IsNullOrWhiteSpace(decoderV5.Make)))
                return decoderV5;

            // tecdoc-vin-check : data.matchingVehicles.array[] + matchingModels / matchingManufacturers
            if (root.ValueKind == JsonValueKind.Object)
            {
                foreach (var wrap in new[] { "data", "result", "response" })
                {
                    if (root.TryGetProperty(wrap, out var wrapped) && wrapped.ValueKind == JsonValueKind.Object)
                    {
                        var tecDoc = ParseTecDocVinCheckPayload(wrapped, vin);
                        if (tecDoc != null) return tecDoc;
                    }
                }

                var rootTecDoc = ParseTecDocVinCheckPayload(root, vin);
                if (rootTecDoc != null) return rootTecDoc;
            }

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
                    foreach (var arrName in new[] { "matchingVehicles", "vehicles", "list", "data" })
                    {
                        var arr = TryGetArrayProperty(c, arrName);
                        if (arr is { } array && array.GetArrayLength() > 0)
                        {
                            var fromArr = ExtractVehicleFields(array[0], vin);
                            if (fromArr != null) return fromArr;
                        }
                    }

                    var direct = ExtractVehicleFields(c, vin);
                    if (direct != null) return direct;
                }
            }

            return null;
        }

        /// <summary>
        /// Format RapidAPI tecdoc-vin-check : tableaux sous matchingVehicles.array, etc.
        /// </summary>
        private static VinVehicleDto? ParseTecDocVinCheckPayload(JsonElement data, string vin)
        {
            if (data.ValueKind != JsonValueKind.Object) return null;

            var manufacturers = TryGetArrayProperty(data, "matchingManufacturers");
            var models = TryGetArrayProperty(data, "matchingModels");
            var vehicles = TryGetArrayProperty(data, "matchingVehicles");

            string? make = null;
            string? manuId = null;
            if (manufacturers is { } manuArr && manuArr.GetArrayLength() > 0)
            {
                var manu = manuArr[0];
                make = FirstString(manu,
                    "manuName", "manufacturerName", "ManufacturerName", "make", "Make");
                manuId = FirstString(manu, "manuId", "manufacturerId", "ManufacturerId", "makeId");
            }

            string? model = null;
            string? modelId = null;
            if (models is { } modelArr && modelArr.GetArrayLength() > 0)
            {
                var mod = modelArr[0];
                model = FirstString(mod, "modelName", "ModelName", "model", "MakeModelName");
                modelId = FirstString(mod, "modelId", "ModelId", "modId");
            }

            if (vehicles is not { } vehArr || vehArr.GetArrayLength() == 0)
            {
                return !string.IsNullOrWhiteSpace(make)
                    ? new VinVehicleDto(vin, make, model, null, null, null, null, null, modelId, manuId)
                    : null;
            }

            var vehicle = vehArr[0];
            var vehicleId = FirstString(vehicle,
                "vehicleId", "VehicleId", "carId", "ktype", "KType", "typeId");
            modelId ??= FirstString(vehicle, "modelId", "ModelId", "modId");
            manuId ??= FirstString(vehicle, "manuId", "manufacturerId", "ManufacturerId", "makeId");

            var carName = FirstString(vehicle, "carName", "CarName");
            var typeDesc = FirstString(vehicle, "vehicleTypeDescription", "VehicleTypeDescription");
            if (string.IsNullOrWhiteSpace(model))
                model = typeDesc ?? carName;

            if (string.IsNullOrWhiteSpace(make) && !string.IsNullOrWhiteSpace(carName))
                make = ExtractMakeFromCarName(carName, model);

            var year = FirstInt(vehicle,
                "year", "Year", "modelYear", "ModelYear", "yearOfConstrFrom", "constructionYear");
            var engine = FirstString(vehicle,
                "engineCode", "EngineCode", "engine", "motorCode", "MotorCode", "typeEngineName");
            var fuel = ExtractFuelFromVehicle(vehicle)
                       ?? FuelTypeNormalizer.FromText(string.Join(" ",
                           new[] { carName, typeDesc, engine }.Where(x => !string.IsNullOrWhiteSpace(x))!));
            var power = FirstInt(vehicle,
                "powerHP", "PowerHP", "powerHp", "power", "Power", "powerKwToHp");

            if (string.IsNullOrWhiteSpace(make) &&
                string.IsNullOrWhiteSpace(model) &&
                string.IsNullOrWhiteSpace(vehicleId))
                return null;

            return new VinVehicleDto(
                vin, make, model, year, engine, fuel, power, vehicleId, modelId, manuId);
        }

        private async Task<string?> TryFuelFromVehicleTypesAsync(VinVehicleDto dto, CancellationToken ct)
        {
            var cfg = rapidOptions.Value;
            if (!cfg.Enabled || string.IsNullOrWhiteSpace(cfg.ApiKey) ||
                string.IsNullOrWhiteSpace(dto.ExternalModelId))
                return null;

            try
            {
                var modelId = Uri.EscapeDataString(dto.ExternalModelId.Trim());
                var path =
                    $"types/type-id/{cfg.TypeId}/list-vehicles-types/{modelId}/" +
                    $"lang-id/{cfg.LangId}/country-filter-id/{cfg.CountryFilterId}";
                using var doc = await GetRapidJsonAsync(path, ct);
                if (doc is null) return null;

                var arr = FindModelTypesArray(doc.RootElement);
                if (arr is not { } types || types.GetArrayLength() == 0)
                {
                    logger.LogInformation(
                        "list-vehicles-types sans modelTypes pour modelId {ModelId}",
                        dto.ExternalModelId);
                    return null;
                }

                JsonElement? match = null;
                foreach (var item in types.EnumerateArray())
                {
                    var id = FirstString(item, "vehicleId", "VehicleId", "carId", "ktype", "KType");
                    if (!string.IsNullOrWhiteSpace(dto.ExternalVehicleId) &&
                        string.Equals(id, dto.ExternalVehicleId.Trim(), StringComparison.OrdinalIgnoreCase))
                    {
                        match = item;
                        break;
                    }
                }

                if (match is not { } el)
                {
                    if (!string.IsNullOrWhiteSpace(dto.ExternalVehicleId))
                    {
                        logger.LogInformation(
                            "K-Type {KType} absent de list-vehicles-types (modelId {ModelId}, {Count} types)",
                            dto.ExternalVehicleId, dto.ExternalModelId, types.GetArrayLength());
                        return null;
                    }
                    el = types[0];
                }

                if (el.ValueKind != JsonValueKind.Object) return null;
                return ExtractFuelFromVehicle(el)
                       ?? FuelTypeNormalizer.FromText(FirstString(el,
                           "typeName", "typeEngineName", "carName", "fullName", "description", "vehicleTypeDescription"));
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Fuel depuis list-vehicles-types ignoré pour modèle {ModelId}", dto.ExternalModelId);
                return null;
            }
        }

        private async Task<string?> TryModelIdFromManufacturerModelsAsync(VinVehicleDto dto, CancellationToken ct)
        {
            var cfg = rapidOptions.Value;
            if (!cfg.Enabled || string.IsNullOrWhiteSpace(cfg.ApiKey) ||
                string.IsNullOrWhiteSpace(dto.ExternalManufacturerId) ||
                string.IsNullOrWhiteSpace(dto.Model))
                return null;

            try
            {
                var manuId = Uri.EscapeDataString(dto.ExternalManufacturerId.Trim());
                var path =
                    $"models/list/type-id/{cfg.TypeId}/manufacturer-id/{manuId}/" +
                    $"lang-id/{cfg.LangId}/country-filter-id/{cfg.CountryFilterId}";
                using var doc = await GetRapidJsonAsync(path, ct);
                if (doc is null) return null;

                var models = TryGetArrayProperty(doc.RootElement, "models")
                             ?? (doc.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object
                                 ? TryGetArrayProperty(data, "models")
                                 : null);
                if (models is not { } arr) return null;

                var want = FoldKey(dto.Model);
                string? best = null;
                foreach (var item in arr.EnumerateArray())
                {
                    var name = FirstString(item, "modelName", "ModelName", "name", "Name");
                    var id = FirstString(item, "modelId", "ModelId", "modId");
                    if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name)) continue;
                    var have = FoldKey(name);
                    if (have.Length == 0) continue;
                    if (want.Contains(have, StringComparison.Ordinal) || have.Contains(want, StringComparison.Ordinal))
                    {
                        best = id;
                        if (have == want) return id;
                    }
                }

                return best;
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "modelId RapidAPI ignoré pour manu {ManuId}", dto.ExternalManufacturerId);
                return null;
            }
        }

        private async Task<JsonDocument?> GetRapidJsonAsync(string relativePath, CancellationToken ct)
        {
            var cfg = rapidOptions.Value;
            var client = httpFactory.CreateClient("RapidApi");
            var baseUrl = cfg.BaseUrl.TrimEnd('/');

            async Task<HttpResponseMessage> SendAsync(string path)
            {
                var request = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/{path.TrimStart('/')}");
                request.Headers.TryAddWithoutValidation("X-RapidAPI-Key", cfg.ApiKey);
                request.Headers.TryAddWithoutValidation("X-RapidAPI-Host", cfg.Host);
                request.Headers.TryAddWithoutValidation("Accept", "application/json");
                return await client.SendAsync(request, ct);
            }

            var response = await SendAsync(relativePath);
            if (!response.IsSuccessStatusCode && cfg.LangId != 4)
            {
                var alt = relativePath
                    .Replace($"/lang-id/{cfg.LangId}", "/lang-id/4", StringComparison.Ordinal)
                    .Replace($"/country-filter-id/{cfg.CountryFilterId}", "/country-filter-id/62", StringComparison.Ordinal);
                response.Dispose();
                response = await SendAsync(alt);
            }

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("RapidAPI {Path} → {Status}", relativePath, (int)response.StatusCode);
                response.Dispose();
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            response.Dispose();
            return doc;
        }

        private static JsonElement? FindModelTypesArray(JsonElement root)
        {
            if (root.ValueKind == JsonValueKind.Array) return root;
            var types = TryGetArrayProperty(root, "modelTypes")
                        ?? TryGetArrayProperty(root, "vehicles")
                        ?? TryGetArrayProperty(root, "matchingVehicles");
            if (types != null) return types;
            if (root.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty("data", out var data))
            {
                if (data.ValueKind == JsonValueKind.Array) return data;
                if (data.ValueKind == JsonValueKind.Object)
                    return TryGetArrayProperty(data, "modelTypes")
                           ?? TryGetArrayProperty(data, "vehicles");
            }
            return null;
        }

        private static string? TryFuelFromRawJson(string raw)
        {
            try
            {
                using var doc = JsonDocument.Parse(raw);
                return ExtractFuelFromVehicle(doc.RootElement)
                       ?? FuelTypeNormalizer.FromText(FirstString(doc.RootElement,
                           "typeName", "typeEngineName", "carName", "fullName"));
            }
            catch (JsonException)
            {
                return FuelTypeNormalizer.FromText(raw);
            }
        }

        private static string FoldKey(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "";
            var norm = s.Trim().Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(norm.Length);
            foreach (var c in norm)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                    sb.Append(char.ToLowerInvariant(c));
            }
            return sb.ToString();
        }

        private static string? ExtractFuelFromVehicle(JsonElement vehicle)
        {
            var raw = FirstString(vehicle,
                "fuel_type_-_primary", "FuelTypePrimary", "fuelTypePrimary",
                "fuelTypeName", "FuelTypeName", "fuelTypeProcess", "FuelTypeProcess",
                "motorType", "MotorType", "motorTypeName", "fuelDesc",
                "fuelType", "FuelType", "fuel", "Fuel", "fuelTypeId");
            return FuelTypeNormalizer.Normalize(raw)
                   ?? FuelTypeNormalizer.FromText(FirstString(vehicle,
                       "typeName", "typeEngineName", "carName", "fullName",
                       "description", "vehicleTypeDescription", "VehicleTypeDescription"));
        }

        private static JsonElement? TryGetArrayProperty(JsonElement container, string propertyName)
        {
            if (!container.TryGetProperty(propertyName, out var prop)) return null;
            if (prop.ValueKind == JsonValueKind.Array) return prop;
            if (prop.ValueKind == JsonValueKind.Object &&
                prop.TryGetProperty("array", out var wrapped) &&
                wrapped.ValueKind == JsonValueKind.Array)
                return wrapped;
            return null;
        }

        private static string? ExtractMakeFromCarName(string carName, string? model)
        {
            if (string.IsNullOrWhiteSpace(carName)) return null;
            var trimmed = carName.Trim();
            if (!string.IsNullOrWhiteSpace(model))
            {
                var idx = trimmed.IndexOf(model, StringComparison.OrdinalIgnoreCase);
                if (idx > 0)
                    return trimmed[..idx].Trim();
            }

            var parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length > 0 ? parts[0] : null;
        }

        private static VinVehicleDto? ExtractVehicleFields(JsonElement el, string vin)
        {
            if (el.ValueKind != JsonValueKind.Object) return null;

            var make = FirstString(el,
                "manuName", "manufacturerName", "ManufacturerName", "make", "Make", "brand", "Brand",
                "manufacturer", "Manufacturer");
            var model = FirstString(el,
                "modelName", "ModelName", "model", "MakeModelName", "vehicleModel",
                "carName", "CarName", "vehicleTypeDescription", "VehicleTypeDescription");
            var vehicleId = FirstString(el,
                "vehicleId", "VehicleId", "carId", "ktype", "KType", "typeId");

            if (string.IsNullOrWhiteSpace(make) &&
                string.IsNullOrWhiteSpace(model) &&
                string.IsNullOrWhiteSpace(vehicleId))
                return null;

            if (string.IsNullOrWhiteSpace(make) && !string.IsNullOrWhiteSpace(model))
                make = ExtractMakeFromCarName(model, null);

            var year = FirstInt(el,
                "year", "Year", "model_year", "modelYear", "ModelYear", "yearOfConstrFrom", "constructionYear");
            var engine = FirstString(el,
                "engineCode", "EngineCode", "engine", "motorCode", "MotorCode", "typeEngineName");
            var fuel = ExtractFuelFromVehicle(el)
                       ?? FuelTypeNormalizer.FromText(string.Join(" ",
                           new[] { model, engine }.Where(x => !string.IsNullOrWhiteSpace(x))!));
            var power = FirstInt(el,
                "powerHP", "PowerHP", "powerHp", "power", "Power", "powerKwToHp",
                "engine_brake_(hp)_from", "EngineBrakeHP");
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

        /// <summary>
        /// RapidAPI decoder-v5 : vin-data-1/2/3 avec content = JSON string (NHTSA-like, fuel_type_-_primary).
        /// </summary>
        private static VinVehicleDto? ParseDecoderV5Payload(JsonElement root, string vin)
        {
            if (root.ValueKind != JsonValueKind.Object) return null;

            VinVehicleDto? merged = null;
            foreach (var prop in root.EnumerateObject())
            {
                if (!prop.Name.StartsWith("vin-data", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (prop.Value.ValueKind != JsonValueKind.Object ||
                    !prop.Value.TryGetProperty("content", out var content))
                    continue;

                JsonDocument? owned = null;
                JsonElement inner;
                try
                {
                    if (content.ValueKind == JsonValueKind.String)
                    {
                        var raw = content.GetString();
                        if (string.IsNullOrWhiteSpace(raw) || raw[0] is not ('{' or '['))
                            continue;
                        owned = JsonDocument.Parse(raw);
                        inner = owned.RootElement;
                    }
                    else if (content.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
                    {
                        inner = content;
                    }
                    else continue;
                }
                catch (JsonException)
                {
                    continue;
                }

                try
                {
                    VinVehicleDto? part = null;
                    if (inner.ValueKind == JsonValueKind.Array && inner.GetArrayLength() > 0)
                        part = ExtractVehicleFields(inner[0], vin);
                    else if (inner.ValueKind == JsonValueKind.Object)
                        part = ExtractVehicleFields(inner, vin);

                    if (part != null)
                        merged = merged == null ? part : MergeVinVehicle(merged, part);
                }
                finally
                {
                    owned?.Dispose();
                }
            }

            return merged;
        }

        private static string? FirstString(JsonElement el, params string[] names)
        {
            foreach (var name in names)
            {
                if (!TryGetPropertyLoose(el, name, out var p)) continue;
                if (p.ValueKind == JsonValueKind.String)
                {
                    var s = p.GetString();
                    if (!string.IsNullOrWhiteSpace(s)) return s.Trim();
                }
                if (p.ValueKind is JsonValueKind.Number)
                    return p.ToString();
                if (p.ValueKind == JsonValueKind.Object)
                {
                    var nested = FirstString(p,
                        "name", "Name", "description", "Description", "label", "value", "text",
                        "fuelTypeName", "FuelTypeName", "fuelTypeId", "fuelTypeProcess", "typeEngineName");
                    if (!string.IsNullOrWhiteSpace(nested)) return nested;
                }
            }
            return null;
        }

        private static int? FirstInt(JsonElement el, params string[] names)
        {
            foreach (var name in names)
            {
                if (!TryGetPropertyLoose(el, name, out var p)) continue;
                if (p.ValueKind == JsonValueKind.Number && p.TryGetInt32(out var n))
                    return n;
                if (p.ValueKind == JsonValueKind.String &&
                    int.TryParse(p.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
                    return parsed;
                if (p.ValueKind == JsonValueKind.Array && p.GetArrayLength() > 0)
                {
                    var first = p[0];
                    if (first.ValueKind == JsonValueKind.Number && first.TryGetInt32(out var an))
                        return an;
                    if (first.ValueKind == JsonValueKind.String &&
                        int.TryParse(first.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var ap))
                        return ap;
                }
            }
            return null;
        }

        private static bool TryGetPropertyLoose(JsonElement el, string name, out JsonElement prop)
        {
            if (el.ValueKind != JsonValueKind.Object)
            {
                prop = default;
                return false;
            }
            if (el.TryGetProperty(name, out prop)) return true;
            var want = NormalizePropName(name);
            if (want.Length == 0) return false;
            foreach (var p in el.EnumerateObject())
            {
                if (NormalizePropName(p.Name) != want) continue;
                prop = p.Value;
                return true;
            }
            prop = default;
            return false;
        }

        private static string NormalizePropName(string name)
        {
            var sb = new StringBuilder(name.Length);
            foreach (var c in name)
            {
                if (char.IsAsciiLetterOrDigit(c))
                    sb.Append(char.ToLowerInvariant(c));
            }
            return sb.ToString();
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
