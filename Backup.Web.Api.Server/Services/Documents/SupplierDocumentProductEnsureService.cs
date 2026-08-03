using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Models;
using Backup.Web.Api.Server.Services.Documents.Parsing;
using Backup.Web.Api.Server.Services.Purchases;
using Microsoft.EntityFrameworkCore;

namespace Backup.Web.Api.Server.Services.Documents
{
    public interface ISupplierDocumentProductEnsureService
    {
        /// <summary>
        /// Style Pulse/EuroBrico : si une ligne OCR n'a pas de fiche catalogue, crée un ErpProduct plat.
        /// Non bloquant — les échecs sont ajoutés aux warnings.
        /// </summary>
        Task<EnsureProductsResult> EnsureProductsForLinesAsync(
            IReadOnlyList<DocumentLine> lines,
            string? supplierName = null,
            CancellationToken ct = default);
    }

    public sealed class EnsureProductsResult
    {
        public int CreatedCount { get; set; }
        public int MatchedCount { get; set; }
        public int SkippedCount { get; set; }
        public List<string> Warnings { get; } = new();
    }

    /// <summary>
    /// Auto-création catalogue à la comptabilisation (équivalent TryEnsureProductForLineAsync, sans variants).
    /// </summary>
    public sealed class SupplierDocumentProductEnsureService : ISupplierDocumentProductEnsureService
    {
        private readonly IStorageBroker storage;

        public SupplierDocumentProductEnsureService(IStorageBroker storage)
        {
            this.storage = storage;
        }

        public async Task<EnsureProductsResult> EnsureProductsForLinesAsync(
            IReadOnlyList<DocumentLine> lines,
            string? supplierName = null,
            CancellationToken ct = default)
        {
            var result = new EnsureProductsResult();
            if (lines == null || lines.Count == 0) return result;

            var catalog = await this.storage.SelectAllErpProducts()
                .AsNoTracking()
                .ToListAsync(ct);

            var brandHint = await ResolveBrandHintAsync(supplierName, ct);

            foreach (var line in lines)
            {
                try
                {
                    var outcome = await EnsureOneAsync(line, catalog, brandHint, ct);
                    switch (outcome)
                    {
                        case EnsureOutcome.Created:
                            result.CreatedCount++;
                            break;
                        case EnsureOutcome.Matched:
                            result.MatchedCount++;
                            break;
                        default:
                            result.SkippedCount++;
                            break;
                    }
                }
                catch (Exception ex)
                {
                    var key = ProductKeyHelper.GetProductKey(line);
                    result.Warnings.Add($"Produit non créé pour « {Truncate(key, 40)} » : {ex.Message}");
                }
            }

            if (result.CreatedCount > 0)
            {
                result.Warnings.Add(
                    $"{result.CreatedCount} produit(s) catalogue créé(s) automatiquement (introuvables en base).");
            }

            return result;
        }

        private enum EnsureOutcome { Skipped, Matched, Created }

        private async Task<EnsureOutcome> EnsureOneAsync(
            DocumentLine line,
            List<ErpProduct> catalog,
            (int? brandId, string? brandName) brandHint,
            CancellationToken ct)
        {
            var sku = line.ProductCode?.Trim();
            var ean = line.Ean?.Trim();
            var description = (line.Product ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(description))
                description = (line.RawLine ?? string.Empty).Trim();

            if (string.IsNullOrEmpty(sku) && string.IsNullOrEmpty(ean) && description.Length < 3)
                return EnsureOutcome.Skipped;

            var existing = FindInCatalog(catalog, sku, ean);
            if (existing != null)
                return EnsureOutcome.Matched;

            var reference = !string.IsNullOrWhiteSpace(sku)
                ? sku!
                : !string.IsNullOrWhiteSpace(ean)
                    ? ean!
                    : Truncate(ProductKeyHelper.Normalize(description), 48);

            if (string.IsNullOrWhiteSpace(reference))
                return EnsureOutcome.Skipped;

            // Double-check collision after normalize / concurrent creates in same batch
            existing = FindInCatalog(catalog, reference, ean);
            if (existing != null)
                return EnsureOutcome.Matched;

            var name = description.Length >= 2 ? description : reference;
            var unitPrice = line.UnitPrice;
            if (unitPrice <= 0 && line.Quantity != 0 && line.TotalValue != 0)
                unitPrice = line.TotalValue / line.Quantity;

            var erpProductId = $"DOC-{SanitizeId(reference)}";
            if (catalog.Any(p => string.Equals(p.ErpProductId, erpProductId, StringComparison.OrdinalIgnoreCase))
                || await this.storage.SelectAllErpProducts().AnyAsync(p => p.ErpProductId == erpProductId, ct))
            {
                erpProductId = $"DOC-{SanitizeId(reference)}-{DateTime.UtcNow:HHmmssfff}";
            }

            var product = new ErpProduct
            {
                ErpProductId = erpProductId,
                Name = Truncate(name, 250),
                Reference = Truncate(reference, 64),
                Ean = string.IsNullOrWhiteSpace(ean) ? null : Truncate(ean!, 32),
                BrandId = brandHint.brandId,
                Brand = brandHint.brandName,
                CPrice = unitPrice > 0 ? unitPrice : null,
                UnitPrice = unitPrice > 0 ? unitPrice : null,
                PriceHT = unitPrice > 0 ? unitPrice : null,
                TypeVatPerc = 21m,
                StockQuantity = 0,
                DataSource = "SupplierDocument",
                FromExcel = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var inserted = await this.storage.InsertErpProductAsync(product);
            catalog.Add(inserted);
            return EnsureOutcome.Created;
        }

        private static ErpProduct? FindInCatalog(List<ErpProduct> catalog, string? reference, string? ean)
        {
            if (!string.IsNullOrWhiteSpace(ean))
            {
                var eanKeys = IdentifierKeys(ean);
                var byEan = catalog.FirstOrDefault(p =>
                    !string.IsNullOrWhiteSpace(p.Ean) && IdentifierKeys(p.Ean).Overlaps(eanKeys));
                if (byEan != null) return byEan;
            }

            if (!string.IsNullOrWhiteSpace(reference))
            {
                var refKeys = IdentifierKeys(reference);
                return catalog.FirstOrDefault(p =>
                    (!string.IsNullOrWhiteSpace(p.Reference) && IdentifierKeys(p.Reference).Overlaps(refKeys))
                    || (!string.IsNullOrWhiteSpace(p.ErpProductId) && IdentifierKeys(p.ErpProductId).Overlaps(refKeys))
                    || (!string.IsNullOrWhiteSpace(p.Ean) && IdentifierKeys(p.Ean).Overlaps(refKeys)));
            }

            return null;
        }

        /// <summary>Clés de matching : brut + sans zéros de tête si numérique (aligné Pulse).</summary>
        private static HashSet<string> IdentifierKeys(string? value)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(value)) return set;
            var raw = value.Trim();
            set.Add(raw);
            var digits = new string(raw.Where(char.IsDigit).ToArray());
            if (digits.Length > 0)
            {
                set.Add(digits);
                set.Add(digits.TrimStart('0'));
                if (set.Contains(string.Empty)) set.Remove(string.Empty);
            }
            return set;
        }

        private async Task<(int? brandId, string? brandName)> ResolveBrandHintAsync(
            string? supplierName,
            CancellationToken ct)
        {
            var token = SupplierBrandMatcher.DeriveBrandToken(supplierName);
            if (string.IsNullOrWhiteSpace(token)) return (null, null);

            var tokenLower = token.ToLowerInvariant();
            var matches = await this.storage.SelectAllErpBrands()
                .AsNoTracking()
                .Where(b => b.Name != null && b.Name.ToLower().Contains(tokenLower))
                .OrderBy(b => b.Name!.Length)
                .Take(3)
                .ToListAsync(ct);

            if (matches.Count >= 1)
                return (matches[0].Id, matches[0].Name);

            return (null, token);
        }

        private static string SanitizeId(string value)
        {
            var chars = value.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_').ToArray();
            var s = new string(chars);
            return string.IsNullOrWhiteSpace(s) ? Guid.NewGuid().ToString("N")[..10].ToUpperInvariant() : Truncate(s, 40);
        }

        private static string Truncate(string value, int max) =>
            value.Length <= max ? value : value[..max];
    }
}
