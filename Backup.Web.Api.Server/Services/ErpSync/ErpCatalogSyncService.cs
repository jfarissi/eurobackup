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
        public bool Completed { get; set; }
        public int ProductsTotal { get; set; }
        public int BrandsTotal { get; set; }
        public int CategoriesTotal { get; set; }
        public int BrandsCreated { get; set; }
        public int BrandsUpdated { get; set; }
        public int CategoriesCreated { get; set; }
        public int CategoriesUpdated { get; set; }
        public int ProductsLinked { get; set; }
    }

    public interface IErpCatalogSyncService
    {
        Task<ErpCatalogRebuildResult> RebuildFromProductsAsync(CancellationToken ct = default);

        /// <summary>
        /// Upsert des catégories d'un niveau (ex. MainType depuis GetProductMainTypes).
        /// </summary>
        Task<ErpCatalogRebuildResult> UpsertErpLevelAsync(
            string level,
            IReadOnlyList<(string ExternalId, string Name, string? ParentLevel, string? ParentExternalId)> items,
            CancellationToken ct = default);
    }

    public class ErpCatalogSyncService : IErpCatalogSyncService
    {
        private const int ProductLinkBatchSize = 500;

        private readonly IStorageBroker _storage;
        private readonly ILogger<ErpCatalogSyncService> _logger;

        public ErpCatalogSyncService(IStorageBroker storage, ILogger<ErpCatalogSyncService> logger)
        {
            _storage = storage;
            _logger = logger;
        }

        public async Task<ErpCatalogRebuildResult> UpsertErpLevelAsync(
            string level,
            IReadOnlyList<(string ExternalId, string Name, string? ParentLevel, string? ParentExternalId)> items,
            CancellationToken ct = default)
        {
            var result = new ErpCatalogRebuildResult();
            if (items.Count == 0)
                return result;

            var categories = await _storage.SelectAllErpCategories().ToListAsync(ct);
            var catsByKey = categories.ToDictionary(
                c => CatKey(c.Level, c.ErpExternalId),
                StringComparer.OrdinalIgnoreCase);

            var dirty = false;
            foreach (var item in items)
            {
                if (IsEmptyErpId(item.ExternalId) || string.IsNullOrWhiteSpace(item.Name))
                    continue;

                var node = new PendingCategory
                {
                    Level = level,
                    ExternalId = item.ExternalId.Trim(),
                    Name = item.Name.Trim(),
                    ParentLevel = item.ParentLevel,
                    ParentExternalId = item.ParentExternalId
                };

                if (await UpsertCategoryStagedAsync(node, catsByKey, result))
                    dirty = true;
            }

            if (dirty)
                await _storage.FlushChangesAsync(ct);

            result.Completed = true;
            result.CategoriesTotal = await _storage.SelectAllErpCategories().CountAsync(ct);
            return result;
        }

        public async Task<ErpCatalogRebuildResult> RebuildFromProductsAsync(CancellationToken ct = default)
        {
            var result = new ErpCatalogRebuildResult();

            // Projection légère : pas besoin de charger tout le produit.
            var productRows = await _storage.SelectAllErpProducts()
                .AsNoTracking()
                .Select(p => new ProductCatalogRow(
                    p.Id,
                    p.Brand,
                    p.BrandId,
                    p.MainTypeID,
                    p.MainTypeName,
                    p.MainSubTypeID,
                    p.MainSubTypeName,
                    p.TypeID,
                    p.TypeName,
                    p.SubTypeID,
                    p.SubTypeName,
                    p.CategoryId))
                .ToListAsync(ct);

            result.ProductsTotal = productRows.Count;

            var brands = await _storage.SelectAllErpBrands().ToListAsync(ct);
            var categories = await _storage.SelectAllErpCategories().ToListAsync(ct);

            var brandsByName = brands.ToDictionary(b => b.Name, StringComparer.OrdinalIgnoreCase);
            var catsByKey = categories.ToDictionary(
                c => CatKey(c.Level, c.ErpExternalId),
                StringComparer.OrdinalIgnoreCase);

            // ── 1) Brands (batch) ──────────────────────────────────────────
            var brandDirty = false;
            foreach (var brandName in productRows
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
                        _storage.StageUpdateErpBrand(existing);
                        result.BrandsUpdated++;
                        brandDirty = true;
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
                await _storage.StageInsertErpBrandAsync(created);
                brandsByName[created.Name] = created;
                result.BrandsCreated++;
                brandDirty = true;
            }

            if (brandDirty)
                await _storage.FlushChangesAsync(ct);

            // ── 2) Categories (collect → insert by level) ─────────────────
            var pending = new Dictionary<string, PendingCategory>(StringComparer.OrdinalIgnoreCase);

            void Touch(string level, string? externalId, string? name, string? parentLevel, string? parentExternalId)
            {
                if (IsEmptyErpId(externalId) || string.IsNullOrWhiteSpace(name))
                    return;

                var id = externalId!.Trim();
                var key = CatKey(level, id);
                if (!pending.TryGetValue(key, out var node))
                {
                    pending[key] = new PendingCategory
                    {
                        Level = level,
                        ExternalId = id,
                        Name = name.Trim(),
                        ParentLevel = parentLevel,
                        ParentExternalId = parentExternalId
                    };
                }
                else if (!string.Equals(node.Name, name.Trim(), StringComparison.Ordinal))
                {
                    node.Name = name.Trim();
                }
            }

            foreach (var p in productRows)
            {
                Touch("MainType", p.MainTypeID, p.MainTypeName, null, null);

                var typeParentLevel = "MainType";
                var typeParentId = p.MainTypeID;
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
                var levelDirty = false;
                foreach (var node in pending.Values.Where(n => n.Level == level))
                {
                    if (await UpsertCategoryStagedAsync(node, catsByKey, result))
                        levelDirty = true;
                }

                if (levelDirty)
                    await _storage.FlushChangesAsync(ct);
            }

            // ── 3) Link products (batch updates) ──────────────────────────
            var idToBrandId = new Dictionary<int, int>();
            var idToCategoryId = new Dictionary<int, int>();

            foreach (var p in productRows)
            {
                if (!string.IsNullOrWhiteSpace(p.Brand)
                    && brandsByName.TryGetValue(p.Brand.Trim(), out var brand)
                    && p.BrandId != brand.Id)
                {
                    idToBrandId[p.Id] = brand.Id;
                }

                var leaf = FindCat(catsByKey, "SubType", p.SubTypeID)
                           ?? FindCat(catsByKey, "Type", p.TypeID)
                           ?? FindCat(catsByKey, "MainType", p.MainTypeID);

                if (leaf != null && p.CategoryId != leaf.Id)
                    idToCategoryId[p.Id] = leaf.Id;
            }

            var idsToTouch = idToBrandId.Keys.Union(idToCategoryId.Keys).Distinct().ToList();
            for (var offset = 0; offset < idsToTouch.Count; offset += ProductLinkBatchSize)
            {
                ct.ThrowIfCancellationRequested();
                var batchIds = idsToTouch.Skip(offset).Take(ProductLinkBatchSize).ToList();
                var batch = await _storage.SelectAllErpProducts()
                    .Where(p => batchIds.Contains(p.Id))
                    .ToListAsync(ct);

                foreach (var product in batch)
                {
                    var changed = false;
                    if (idToBrandId.TryGetValue(product.Id, out var brandId))
                    {
                        product.BrandId = brandId;
                        changed = true;
                    }

                    if (idToCategoryId.TryGetValue(product.Id, out var categoryId))
                    {
                        product.CategoryId = categoryId;
                        changed = true;
                    }

                    if (!changed)
                        continue;

                    product.UpdatedAt = DateTime.UtcNow;
                    _storage.StageUpdateErpProduct(product);
                    result.ProductsLinked++;
                }

                await _storage.FlushChangesAsync(ct);
            }

            result.BrandsTotal = await _storage.SelectAllErpBrands().CountAsync(ct);
            result.CategoriesTotal = await _storage.SelectAllErpCategories().CountAsync(ct);
            result.Completed = true;

            _logger.LogInformation(
                "ERP catalog rebuild completed: products={Products} brands={BrandsTotal} (+{CreatedB}/~{UpdatedB}) categories={CategoriesTotal} (+{CreatedC}/~{UpdatedC}) linked={Linked}",
                result.ProductsTotal,
                result.BrandsTotal,
                result.BrandsCreated,
                result.BrandsUpdated,
                result.CategoriesTotal,
                result.CategoriesCreated,
                result.CategoriesUpdated,
                result.ProductsLinked);

            return result;
        }

        private async Task<bool> UpsertCategoryStagedAsync(
            PendingCategory node,
            Dictionary<string, ErpCategory> catsByKey,
            ErpCatalogRebuildResult result)
        {
            int? parentId = null;
            if (!string.IsNullOrWhiteSpace(node.ParentLevel) && !IsEmptyErpId(node.ParentExternalId))
            {
                var parent = FindCat(catsByKey, node.ParentLevel!, node.ParentExternalId);
                parentId = parent?.Id;
            }

            var key = CatKey(node.Level, node.ExternalId);

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
                    return false;

                existing.UpdatedAt = DateTime.UtcNow;
                _storage.StageUpdateErpCategory(existing);
                result.CategoriesUpdated++;
                return true;
            }

            var slug = EnsureUniqueCategorySlug(Slugify(node.Name), catsByKey.Values, null);
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
            await _storage.StageInsertErpCategoryAsync(created);
            catsByKey[key] = created;
            result.CategoriesCreated++;
            return true;
        }

        private sealed record ProductCatalogRow(
            int Id,
            string? Brand,
            int? BrandId,
            string? MainTypeID,
            string? MainTypeName,
            string? MainSubTypeID,
            string? MainSubTypeName,
            string? TypeID,
            string? TypeName,
            string? SubTypeID,
            string? SubTypeName,
            int? CategoryId);

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
