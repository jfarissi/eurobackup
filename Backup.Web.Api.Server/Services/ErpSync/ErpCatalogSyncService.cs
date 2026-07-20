using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Backup.Web.Api.Server.Services.ErpSync
{
    public class ErpCatalogRebuildResult
    {
        public int BrandsCreated { get; set; }
        public int BrandsUpdated { get; set; }
        public int CategoriesCreated { get; set; }
        public int CategoriesUpdated { get; set; }
        public int ProductsLinked { get; set; }
    }

    public interface IErpCatalogSyncService
    {
        /// <summary>
        /// Reconstruit ErpBrands / ErpCategories depuis ErpProducts
        /// et rattache BrandId / CategoryId sur chaque produit.
        /// </summary>
        Task<ErpCatalogRebuildResult> RebuildFromProductsAsync(CancellationToken ct = default);
    }

    public class ErpCatalogSyncService : IErpCatalogSyncService
    {
        private readonly IStorageBroker _storage;
        private readonly ILogger<ErpCatalogSyncService> _logger;

        public ErpCatalogSyncService(IStorageBroker storage, ILogger<ErpCatalogSyncService> logger)
        {
            _storage = storage;
            _logger = logger;
        }

        public async Task<ErpCatalogRebuildResult> RebuildFromProductsAsync(CancellationToken ct = default)
        {
            var result = new ErpCatalogRebuildResult();
            var products = await _storage.SelectAllErpProducts().ToListAsync(ct);
            var brands = await _storage.SelectAllErpBrands().ToListAsync(ct);
            var categories = await _storage.SelectAllErpCategories().ToListAsync(ct);

            var brandsByName = brands.ToDictionary(b => b.Name, StringComparer.OrdinalIgnoreCase);
            var catsByKey = categories.ToDictionary(
                c => CatKey(c.Level, c.ErpExternalId),
                StringComparer.OrdinalIgnoreCase);

            // 1) Brands
            foreach (var brandName in products
                         .Select(p => p.Brand?.Trim())
                         .Where(b => !string.IsNullOrWhiteSpace(b))
                         .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (brandsByName.TryGetValue(brandName!, out var existing))
                {
                    var slug = EnsureUniqueSlug(Slugify(brandName!), brandsByName.Values, existing.Id);
                    if (!string.Equals(existing.Slug, slug, StringComparison.OrdinalIgnoreCase))
                    {
                        existing.Slug = slug;
                        existing.UpdatedAt = DateTime.UtcNow;
                        await _storage.UpdateErpBrandAsync(existing);
                        result.BrandsUpdated++;
                    }
                    continue;
                }

                var created = new ErpBrand
                {
                    Name = brandName!,
                    Slug = EnsureUniqueSlug(Slugify(brandName!), brandsByName.Values, null),
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };
                await _storage.InsertErpBrandAsync(created);
                brandsByName[created.Name] = created;
                result.BrandsCreated++;
            }

            // 2) Categories — collect unique nodes then insert MainType → Type → SubType
            var pending = new Dictionary<string, PendingCategory>(StringComparer.OrdinalIgnoreCase);

            void Touch(string level, string? externalId, string? name, string? parentLevel, string? parentExternalId)
            {
                if (IsEmptyErpId(externalId) || string.IsNullOrWhiteSpace(name))
                    return;

                var id = externalId!.Trim();
                var key = CatKey(level, id);
                if (!pending.TryGetValue(key, out var node))
                {
                    node = new PendingCategory
                    {
                        Level = level,
                        ExternalId = id,
                        Name = name.Trim(),
                        ParentLevel = parentLevel,
                        ParentExternalId = parentExternalId
                    };
                    pending[key] = node;
                }
                else if (!string.Equals(node.Name, name.Trim(), StringComparison.Ordinal))
                {
                    node.Name = name.Trim();
                }
            }

            foreach (var p in products)
            {
                Touch("MainType", p.MainTypeID, p.MainTypeName, null, null);

                string? typeParentLevel = "MainType";
                string? typeParentId = p.MainTypeID;
                if (!IsEmptyErpId(p.MainSubTypeID) && !string.IsNullOrWhiteSpace(p.MainSubTypeName))
                {
                    Touch("MainSubType", p.MainSubTypeID, p.MainSubTypeName, "MainType", p.MainTypeID);
                    typeParentLevel = "MainSubType";
                    typeParentId = p.MainSubTypeID;
                }

                Touch("Type", p.TypeID, p.TypeName, typeParentLevel, typeParentId);
                Touch("SubType", p.SubTypeID, p.SubTypeName, "Type", p.TypeID);
            }

            foreach (var level in new[] { "MainType", "MainSubType", "Type", "SubType" })
            {
                foreach (var node in pending.Values.Where(n => n.Level == level))
                {
                    await UpsertCategoryAsync(node, catsByKey, result, ct);
                }
            }

            // 3) Link products
            foreach (var p in products)
            {
                var changed = false;

                if (!string.IsNullOrWhiteSpace(p.Brand)
                    && brandsByName.TryGetValue(p.Brand.Trim(), out var brand)
                    && p.BrandId != brand.Id)
                {
                    p.BrandId = brand.Id;
                    changed = true;
                }

                var leaf = FindCat(catsByKey, "SubType", p.SubTypeID)
                           ?? FindCat(catsByKey, "Type", p.TypeID)
                           ?? FindCat(catsByKey, "MainType", p.MainTypeID);

                if (leaf != null && p.CategoryId != leaf.Id)
                {
                    p.CategoryId = leaf.Id;
                    changed = true;
                }

                if (!changed)
                    continue;

                p.UpdatedAt = DateTime.UtcNow;
                await _storage.UpdateErpProductAsync(p);
                result.ProductsLinked++;
            }

            _logger.LogInformation(
                "ERP catalog rebuild: brands +{CreatedB}/~{UpdatedB}, categories +{CreatedC}/~{UpdatedC}, products linked={Linked}",
                result.BrandsCreated,
                result.BrandsUpdated,
                result.CategoriesCreated,
                result.CategoriesUpdated,
                result.ProductsLinked);

            return result;
        }

        private async Task UpsertCategoryAsync(
            PendingCategory node,
            Dictionary<string, ErpCategory> catsByKey,
            ErpCatalogRebuildResult result,
            CancellationToken ct)
        {
            int? parentId = null;
            if (!string.IsNullOrWhiteSpace(node.ParentLevel) && !IsEmptyErpId(node.ParentExternalId))
            {
                var parent = FindCat(catsByKey, node.ParentLevel!, node.ParentExternalId);
                parentId = parent?.Id;
            }

            var key = CatKey(node.Level, node.ExternalId);
            var slug = EnsureUniqueCategorySlug(Slugify(node.Name), catsByKey.Values, null);

            if (catsByKey.TryGetValue(key, out var existing))
            {
                var updated = false;
                if (!string.Equals(existing.NameNl, node.Name, StringComparison.Ordinal))
                {
                    existing.NameNl = node.Name;
                    existing.NameFr = node.Name;
                    existing.NameEn = node.Name;
                    updated = true;
                }

                var newSlug = EnsureUniqueCategorySlug(Slugify(node.Name), catsByKey.Values, existing.Id);
                if (!string.Equals(existing.SlugNl, newSlug, StringComparison.OrdinalIgnoreCase))
                {
                    existing.SlugNl = newSlug;
                    existing.SlugFr = newSlug;
                    existing.SlugEn = newSlug;
                    updated = true;
                }

                if (parentId.HasValue && existing.ParentId != parentId)
                {
                    existing.ParentId = parentId;
                    updated = true;
                }

                if (!updated)
                    return;

                existing.UpdatedAt = DateTime.UtcNow;
                await _storage.UpdateErpCategoryAsync(existing);
                result.CategoriesUpdated++;
                return;
            }

            var created = new ErpCategory
            {
                ErpExternalId = node.ExternalId,
                Level = node.Level,
                NameNl = node.Name,
                NameFr = node.Name,
                NameEn = node.Name,
                SlugNl = slug,
                SlugFr = slug,
                SlugEn = slug,
                ParentId = parentId,
                SortOrder = 0,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            await _storage.InsertErpCategoryAsync(created);
            catsByKey[key] = created;
            result.CategoriesCreated++;
        }

        private sealed class PendingCategory
        {
            public string Level { get; set; } = string.Empty;
            public string ExternalId { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public string? ParentLevel { get; set; }
            public string? ParentExternalId { get; set; }
        }

        private static ErpCategory? FindCat(Dictionary<string, ErpCategory> map, string level, string? externalId)
        {
            if (IsEmptyErpId(externalId))
                return null;
            return map.TryGetValue(CatKey(level, externalId!.Trim()), out var cat) ? cat : null;
        }

        private static string CatKey(string level, string externalId) => $"{level}:{externalId}";

        private static bool IsEmptyErpId(string? id) =>
            string.IsNullOrWhiteSpace(id) || id.Trim() == "0";

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

        private static string EnsureUniqueSlug(string baseSlug, IEnumerable<ErpBrand> existing, int? excludeId)
        {
            var slug = baseSlug;
            var i = 2;
            while (existing.Any(b =>
                       (!excludeId.HasValue || b.Id != excludeId.Value)
                       && string.Equals(b.Slug, slug, StringComparison.OrdinalIgnoreCase)))
            {
                slug = $"{baseSlug}-{i++}";
            }

            return slug;
        }

        private static string EnsureUniqueCategorySlug(string baseSlug, IEnumerable<ErpCategory> existing, int? excludeId)
        {
            var slug = baseSlug;
            var i = 2;
            while (existing.Any(c =>
                       (!excludeId.HasValue || c.Id != excludeId.Value)
                       && string.Equals(c.SlugNl, slug, StringComparison.OrdinalIgnoreCase)))
            {
                slug = $"{baseSlug}-{i++}";
            }

            return slug;
        }
    }
}
