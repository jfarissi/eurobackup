using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Models;
using Backup.Web.Api.Server.Models.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Backup.Web.Api.Server.Services.ErpSync
{
    public class CarApiImportService : ICarApiImportService
    {
        private const string MainTypeExternalId = "CARAPI-MAIN";
        private const string MainTypeName = "Pièces auto";
        private const int SaveBatchSize = 100;

        private static readonly (string[] Keywords, string CategoryName, string CategoryId)[] PartCategoryRules =
        {
            (new[] { "brake", "abs", "disc", "pad", "caliper", "master-cylinder" }, "Freinage", "CARAPI-FREIN"),
            (new[] { "shock", "spring", "suspension", "strut", "absorber", "stabilizer", "ball-joint" }, "Suspension", "CARAPI-SUSP"),
            (new[] { "engine", "turbo", "piston", "cylinder", "camshaft", "crankshaft", "gasket", "timing", "oil-pump", "water-pump", "radiator", "thermostat", "alternator", "starter" }, "Moteur", "CARAPI-MOTEUR"),
            (new[] { "clutch", "gearbox", "transmission", "driveshaft", "cv-joint", "differential" }, "Transmission", "CARAPI-TRANS"),
            (new[] { "bumper", "fender", "hood", "door", "mirror", "grille", "spoiler", "body" }, "Carrosserie", "CARAPI-CARRO"),
            (new[] { "sensor", "relay", "fuse", "wiring", "battery", "bulb", "lamp", "switch", "ecu", "actuator" }, "Électricité", "CARAPI-ELEC"),
            (new[] { "ac-", "a-c-", "compressor", "condenser", "evaporator", "heater", "ventilation" }, "Climatisation", "CARAPI-CLIM"),
            (new[] { "steering", "rack", "tie-rod", "power-steering" }, "Direction", "CARAPI-DIR"),
            (new[] { "exhaust", "catalyst", "muffler", "lambda" }, "Échappement", "CARAPI-ECHAP"),
            (new[] { "filter", "wiper", "belt", "hose", "gasket", "bearing", "bushing", "seal" }, "Entretien", "CARAPI-ENTRET"),
        };

        private readonly IStorageBroker _storage;
        private readonly ErpSyncOptions _options;
        private readonly IHostEnvironment _environment;
        private readonly ICarApiCatalogService _catalogService;
        private readonly ILogger<CarApiImportService> _logger;

        public CarApiImportService(
            IStorageBroker storage,
            IOptions<ErpSyncOptions> options,
            IHostEnvironment environment,
            ICarApiCatalogService catalogService,
            ILogger<CarApiImportService> logger)
        {
            _storage = storage;
            _options = options.Value ?? new ErpSyncOptions();
            _environment = environment;
            _catalogService = catalogService;
            _logger = logger;
        }

        public async Task<CarApiImportResult> ImportAsync(
            string? dataPath = null,
            bool importParts = true,
            bool importVehicleBrands = false,
            bool applyFrenchNames = true,
            bool ensureVehicleAttribute = true,
            string? companyId = null,
            string? userName = null,
            CancellationToken ct = default)
        {
            var result = new CarApiImportResult();
            var basePath = ResolveDataPath(dataPath);

            var partsFile = Path.Combine(basePath, "car-parts.json");
            var brandsFile = Path.Combine(basePath, "car-brands.json");

            if (importParts && !File.Exists(partsFile))
            {
                result.Errors.Add($"Fichier introuvable: {partsFile}");
                return result;
            }

            if (importVehicleBrands && !File.Exists(brandsFile))
            {
                result.Errors.Add($"Fichier introuvable: {brandsFile}");
                return result;
            }

            if (ensureVehicleAttribute && !string.IsNullOrWhiteSpace(companyId))
            {
                await _catalogService.EnsureVehicleCompatAttributeAsync(companyId, userName, ct);
                result.VehicleAttributeEnsured = true;
            }

            ErpCategory? rootCategory = null;
            var typeCategories = new Dictionary<string, ErpCategory>(StringComparer.OrdinalIgnoreCase);

            if (importParts)
            {
                (rootCategory, typeCategories) = await EnsurePartCategoriesAsync(result, ct);
                await ImportPartsAsync(partsFile, rootCategory!, typeCategories, applyFrenchNames, result, ct);
            }

            if (importVehicleBrands)
                await ImportVehicleBrandsAsync(brandsFile, result, ct);

            if (applyFrenchNames && !importParts)
                result.FrenchNamesUpdated = await ApplyFrenchNamesToExistingAsync(result, ct);

            _logger.LogInformation(
                "Car-api import terminé: parts created={Created} updated={Updated} french={French} vehicleBrands={Brands}",
                result.PartsCreated, result.PartsUpdated, result.FrenchNamesUpdated, result.VehicleBrandsCreated);

            return result;
        }

        private string ResolveDataPath(string? dataPath)
        {
            if (!string.IsNullOrWhiteSpace(dataPath))
                return dataPath.Trim();

            if (!string.IsNullOrWhiteSpace(_options.CarApiDataPath))
                return _options.CarApiDataPath.Trim();

            return Path.Combine(_environment.ContentRootPath, "Data", "CarApi");
        }

        private async Task<(ErpCategory Root, Dictionary<string, ErpCategory> Types)> EnsurePartCategoriesAsync(
            CarApiImportResult result,
            CancellationToken ct)
        {
            var categories = await _storage.SelectAllErpCategories().ToListAsync(ct);
            var byKey = categories.ToDictionary(c => $"{c.Level}:{c.ErpExternalId}", StringComparer.OrdinalIgnoreCase);

            var root = await UpsertCategoryAsync(
                byKey, "MainType", MainTypeExternalId, MainTypeName, null, result, ct);

            var types = new Dictionary<string, ErpCategory>(StringComparer.OrdinalIgnoreCase);
            foreach (var rule in PartCategoryRules)
            {
                var cat = await UpsertCategoryAsync(
                    byKey, "Type", rule.CategoryId, rule.CategoryName, root.Id, result, ct);
                types[rule.CategoryId] = cat;
            }

            var autres = await UpsertCategoryAsync(
                byKey, "Type", "CARAPI-AUTRES", "Pièces diverses", root.Id, result, ct);
            types["CARAPI-AUTRES"] = autres;

            return (root, types);
        }

        private async Task<ErpCategory> UpsertCategoryAsync(
            Dictionary<string, ErpCategory> byKey,
            string level,
            string externalId,
            string name,
            int? parentId,
            CarApiImportResult result,
            CancellationToken ct)
        {
            var key = $"{level}:{externalId}";
            if (byKey.TryGetValue(key, out var existing))
                return existing;

            var slug = Slugify(name);
            var created = new ErpCategory
            {
                ErpExternalId = externalId,
                Level = level,
                NameNl = name,
                NameFr = name,
                NameEn = name,
                SlugNl = slug,
                SlugFr = slug,
                SlugEn = slug,
                ParentId = parentId,
                SortOrder = 0,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _storage.StageInsertErpCategoryAsync(created);
            await _storage.FlushChangesAsync(ct);
            byKey[key] = created;
            result.CategoriesCreated++;
            return created;
        }

        private async Task ImportPartsAsync(
            string partsFile,
            ErpCategory rootCategory,
            IReadOnlyDictionary<string, ErpCategory> typeCategories,
            bool useFrenchNames,
            CarApiImportResult result,
            CancellationToken ct)
        {
            await using var stream = File.OpenRead(partsFile);
            var parts = await JsonSerializer.DeserializeAsync<List<CarApiPartDto>>(stream, JsonOptions, ct)
                        ?? new List<CarApiPartDto>();

            result.PartsTotal = parts.Count;

            var existingRows = await _storage.SelectAllErpProducts()
                .AsNoTracking()
                .Where(p => p.ErpProductId.StartsWith("CARAPI-"))
                .Select(p => new ExistingPartRow(p.Id, p.ErpProductId, p.Reference))
                .ToListAsync(ct);

            var byErpId = existingRows.ToDictionary(p => p.ErpProductId, StringComparer.OrdinalIgnoreCase);
            var byReference = existingRows
                .Where(p => !string.IsNullOrWhiteSpace(p.Reference))
                .ToDictionary(p => p.Reference!, p => p.Id, StringComparer.OrdinalIgnoreCase);

            var pendingReferences = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var productsWithVariant = (await _storage.SelectAllErpProductVariants()
                .AsNoTracking()
                .Select(v => v.ProductId)
                .ToListAsync(ct)).ToHashSet();

            var pendingInserts = 0;
            var pendingUpdates = 0;

            foreach (var part in parts)
            {
                ct.ThrowIfCancellationRequested();

                if (string.IsNullOrWhiteSpace(part.Slug))
                {
                    result.PartsSkipped++;
                    continue;
                }

                var slug = part.Slug.Trim();
                var erpId = $"CARAPI-{slug}";
                var displayName = ChooseDisplayName(slug, part.Name, useFrenchNames);
                var category = ResolvePartCategory(slug, typeCategories);
                var categoryId = category?.Id ?? rootCategory.Id;

                if (byErpId.TryGetValue(erpId, out var existing))
                {
                    var tracked = await _storage.SelectAllErpProducts()
                        .FirstOrDefaultAsync(p => p.Id == existing.Id, ct);
                    if (tracked == null)
                    {
                        result.PartsSkipped++;
                        continue;
                    }

                    var changed = false;
                    if (!string.Equals(tracked.Name, displayName, StringComparison.Ordinal))
                    {
                        tracked.Name = displayName;
                        changed = true;
                    }

                    if (tracked.CategoryId != categoryId)
                    {
                        ApplyCategoryFields(tracked, rootCategory, category);
                        changed = true;
                    }

                    if (!string.IsNullOrWhiteSpace(part.Name)
                        && !string.Equals(tracked.Comment, part.Name.Trim(), StringComparison.Ordinal))
                    {
                        tracked.Comment = part.Name.Trim();
                        changed = true;
                    }

                    if (changed)
                    {
                        tracked.UpdatedAt = DateTime.UtcNow;
                        _storage.StageUpdateErpProduct(tracked);
                        result.PartsUpdated++;
                        pendingUpdates++;
                    }
                    else
                    {
                        result.PartsSkipped++;
                    }

                    await EnsureVariantAsync(tracked.Id, slug, productsWithVariant, result);
                }
                else if (byReference.ContainsKey(slug) || pendingReferences.Contains(slug))
                {
                    result.PartsSkipped++;
                }
                else
                {
                    var product = new ErpProduct
                    {
                        ErpProductId = erpId,
                        Name = displayName,
                        Reference = slug,
                        Comment = string.IsNullOrWhiteSpace(part.Name) ? null : part.Name.Trim(),
                        DataSource = "CarApi",
                        FromExcel = false,
                        TypeVatPerc = 21m,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    ApplyCategoryFields(product, rootCategory, category);

                    await _storage.StageInsertErpProductAsync(product);
                    pendingInserts++;
                    result.PartsCreated++;
                    byErpId[erpId] = new ExistingPartRow(0, erpId, slug);
                    pendingReferences.Add(slug);
                }

                if (pendingInserts >= SaveBatchSize)
                {
                    await _storage.FlushChangesAsync(ct);
                    pendingInserts = 0;
                }

                if (pendingUpdates >= SaveBatchSize)
                {
                    await _storage.FlushChangesAsync(ct);
                    pendingUpdates = 0;
                }
            }

            if (pendingInserts > 0 || pendingUpdates > 0)
                await _storage.FlushChangesAsync(ct);

            var carApiProducts = await _storage.SelectAllErpProducts()
                .AsNoTracking()
                .Where(p => p.ErpProductId.StartsWith("CARAPI-"))
                .Select(p => new { p.Id, p.Reference })
                .ToListAsync(ct);

            foreach (var p in carApiProducts)
            {
                var sku = p.Reference ?? "default";
                await EnsureVariantAsync(p.Id, sku, productsWithVariant, result);
            }
        }

        private async Task EnsureVariantAsync(
            int productId,
            string sku,
            HashSet<int> productsWithVariant,
            CarApiImportResult result)
        {
            if (productsWithVariant.Contains(productId))
                return;

            await _storage.InsertErpProductVariantAsync(new ErpProductVariant
            {
                Id = Guid.NewGuid(),
                ProductId = productId,
                Sku = sku,
                AttributesJson = "{}",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
            productsWithVariant.Add(productId);
            result.VariantsCreated++;
        }

        private sealed record ExistingPartRow(int Id, string ErpProductId, string? Reference);

        private async Task ImportVehicleBrandsAsync(
            string brandsFile,
            CarApiImportResult result,
            CancellationToken ct)
        {
            await using var stream = File.OpenRead(brandsFile);
            var brands = await JsonSerializer.DeserializeAsync<List<CarApiBrandDto>>(stream, JsonOptions, ct)
                         ?? new List<CarApiBrandDto>();

            result.VehicleBrandsTotal = brands.Count;

            var existing = await _storage.SelectAllErpBrands().ToListAsync(ct);
            var byName = existing.ToDictionary(b => b.Name, StringComparer.OrdinalIgnoreCase);
            var pending = 0;

            foreach (var item in brands)
            {
                ct.ThrowIfCancellationRequested();
                var name = item.Brand?.Trim();
                if (string.IsNullOrWhiteSpace(name))
                {
                    result.VehicleBrandsSkipped++;
                    continue;
                }

                if (byName.ContainsKey(name))
                {
                    result.VehicleBrandsSkipped++;
                    continue;
                }

                var brand = new ErpBrand
                {
                    Name = name,
                    Slug = EnsureUniqueBrandSlug(Slugify(name), existing),
                    Description = "Marque véhicule (catalogue car-api)",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                await _storage.StageInsertErpBrandAsync(brand);
                existing.Add(brand);
                byName[name] = brand;
                result.VehicleBrandsCreated++;
                pending++;

                if (pending >= SaveBatchSize)
                {
                    await _storage.FlushChangesAsync(ct);
                    pending = 0;
                }
            }

            if (pending > 0)
                await _storage.FlushChangesAsync(ct);
        }

        private async Task<int> ApplyFrenchNamesToExistingAsync(CarApiImportResult result, CancellationToken ct)
        {
            var products = await _storage.SelectAllErpProducts()
                .Where(p =>
                    p.ErpProductId.StartsWith("CARAPI-")
                    || p.ErpProductId.StartsWith("RAPID-")
                    || p.DataSource == "RapidApi"
                    || p.DataSource == "CarApi")
                .ToListAsync(ct);

            var updated = 0;
            var pending = 0;

            foreach (var product in products)
            {
                ct.ThrowIfCancellationRequested();
                string frenchName;
                if (product.ErpProductId.StartsWith("CARAPI-", StringComparison.Ordinal)
                    || string.Equals(product.DataSource, "CarApi", StringComparison.OrdinalIgnoreCase))
                {
                    var slug = product.Reference
                               ?? (product.ErpProductId.StartsWith("CARAPI-", StringComparison.Ordinal)
                                   ? product.ErpProductId["CARAPI-".Length..]
                                   : product.Name);
                    frenchName = ChooseDisplayName(slug, product.Comment, useFrench: true);
                }
                else
                {
                    frenchName = CarApiPartNameTranslator.TranslateEnglishName(product.Name);
                    if (string.IsNullOrWhiteSpace(frenchName))
                        continue;
                }

                if (string.Equals(product.Name, frenchName, StringComparison.Ordinal))
                    continue;

                product.Name = frenchName;
                product.UpdatedAt = DateTime.UtcNow;
                _storage.StageUpdateErpProduct(product);
                updated++;
                pending++;

                if (pending >= SaveBatchSize)
                {
                    await _storage.FlushChangesAsync(ct);
                    pending = 0;
                }
            }

            if (pending > 0)
                await _storage.FlushChangesAsync(ct);

            result.PartsUpdated += updated;
            return updated;
        }

        private static void ApplyCategoryFields(ErpProduct product, ErpCategory root, ErpCategory? type)
        {
            product.MainTypeID = root.ErpExternalId;
            product.MainTypeName = root.NameFr;
            product.TypeID = type?.ErpExternalId;
            product.TypeName = type?.NameFr;
            product.CategoryId = type?.Id ?? root.Id;
        }

        private static ErpCategory? ResolvePartCategory(string slug, IReadOnlyDictionary<string, ErpCategory> typeCategories)
        {
            foreach (var rule in PartCategoryRules)
            {
                if (rule.Keywords.Any(kw => slug.Contains(kw, StringComparison.OrdinalIgnoreCase)))
                    return typeCategories.TryGetValue(rule.CategoryId, out var cat) ? cat : null;
            }

            return typeCategories.TryGetValue("CARAPI-AUTRES", out var autres) ? autres : null;
        }

        private static string ChooseDisplayName(string slug, string? rawName, bool useFrench = true)
        {
            if (useFrench)
                return CarApiPartNameTranslator.TranslateSlug(slug);

            if (!string.IsNullOrWhiteSpace(rawName) && LooksReadable(rawName))
                return rawName.Trim();

            return HumanizeSlug(slug);
        }

        private static bool LooksReadable(string value)
        {
            var letters = value.Count(char.IsLetter);
            if (letters == 0)
                return false;

            var latin = value.Count(ch => ch is >= 'A' and <= 'Z' or >= 'a' and <= 'z'
                or >= 'À' and <= 'ÿ');
            return latin >= letters * 0.5;
        }

        private static string HumanizeSlug(string slug) =>
            CultureInfo.CurrentCulture.TextInfo.ToTitleCase(slug.Replace('-', ' '));

        private static string Slugify(string value)
        {
            var normalized = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(normalized.Length);
            foreach (var ch in normalized)
            {
                var uc = CharUnicodeInfo.GetUnicodeCategory(ch);
                if (uc == UnicodeCategory.NonSpacingMark)
                    continue;
                if (char.IsLetterOrDigit(ch))
                    sb.Append(ch);
                else if (char.IsWhiteSpace(ch) || ch is '-' or '_' or '/')
                    sb.Append('-');
            }

            var slug = Regex.Replace(sb.ToString().Normalize(NormalizationForm.FormC), "-{2,}", "-").Trim('-');
            return string.IsNullOrWhiteSpace(slug) ? "item" : slug;
        }

        private static string EnsureUniqueBrandSlug(string baseSlug, IEnumerable<ErpBrand> existing)
        {
            var slug = baseSlug;
            var i = 2;
            while (existing.Any(b => string.Equals(b.Slug, slug, StringComparison.OrdinalIgnoreCase)))
                slug = $"{baseSlug}-{i++}";
            return slug;
        }

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private sealed class CarApiPartDto
        {
            public string Slug { get; set; } = string.Empty;
            public string? Name { get; set; }
        }

        private sealed class CarApiBrandDto
        {
            public string? Brand { get; set; }
        }
    }
}
