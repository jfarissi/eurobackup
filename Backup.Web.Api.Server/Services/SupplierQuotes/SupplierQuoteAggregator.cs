using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Models.Catalog;
using Backup.Web.Api.Server.Services.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Backup.Web.Api.Server.Services.SupplierQuotes
{
    public sealed class SupplierQuoteAggregator : ISupplierQuoteService
    {
        public static readonly TimeSpan SnapshotTtl = TimeSpan.FromSeconds(30);

        private readonly IStorageBroker storage;
        private readonly IEnumerable<ISupplierFeedAdapter> adapters;
        private readonly SupplierSeedService supplierSeed;
        private readonly ILogger<SupplierQuoteAggregator> logger;

        public SupplierQuoteAggregator(
            IStorageBroker storage,
            IEnumerable<ISupplierFeedAdapter> adapters,
            SupplierSeedService supplierSeed,
            ILogger<SupplierQuoteAggregator> logger)
        {
            this.storage = storage;
            this.adapters = adapters;
            this.supplierSeed = supplierSeed;
            this.logger = logger;
        }

        public async Task<SupplierQuotesResult> GetQuotesAsync(
            int productId, string companyId, bool forceRefresh, CancellationToken cancellationToken)
        {
            if (!forceRefresh)
            {
                var cached = await LoadSnapshotAsync(productId, companyId, cancellationToken);
                if (cached != null && cached.QuotedAt > DateTime.UtcNow.Subtract(SnapshotTtl))
                    return cached;
            }

            var product = await this.storage.SelectErpProductByIdAsync(productId);
            if (product == null)
                throw new InvalidOperationException("Produit introuvable.");

            await this.supplierSeed.EnsureDemoFeedSuppliersAsync(companyId);

            var suppliers = await this.storage.SelectAllSuppliers()
                .AsNoTracking()
                .Where(s => s.CompanyId == companyId && s.IsActive && s.FeedCode != null && s.FeedCode != "")
                .ToListAsync(cancellationToken);

            var request = new SupplierQuoteRequest
            {
                ProductId = product.Id,
                CompanyId = companyId,
                Reference = product.Reference,
                Ean = product.Ean,
                Name = product.Name,
                CatalogCost = product.CPrice ?? product.PriceHT,
                CatalogStock = product.StockQuantity,
                StockProductKey = product.Reference
            };

            var adapterMap = this.adapters
                .GroupBy(a => a.FeedCode, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            var tasks = suppliers
                .Where(s => adapterMap.ContainsKey(s.FeedCode!))
                .Select(async s =>
                {
                    try
                    {
                        var quote = await adapterMap[s.FeedCode!].QuoteAsync(request, cancellationToken);
                        if (quote == null) return null;
                        quote.SupplierId = s.Id;
                        quote.SupplierName = s.Name;
                        quote.FeedCode = s.FeedCode!;
                        quote.QuotedAt = DateTime.UtcNow;
                        return quote;
                    }
                    catch (Exception ex)
                    {
                        this.logger.LogWarning(ex, "Feed {Feed} failed for product {ProductId}", s.FeedCode, productId);
                        return null;
                    }
                })
                .ToList();

            var quotes = (await Task.WhenAll(tasks)).Where(q => q != null).Cast<SupplierQuoteDto>().ToList();

            foreach (var quote in quotes)
            {
                await this.storage.UpsertErpProductSupplierOfferAsync(new ErpProductSupplierOffer
                {
                    CompanyId = companyId,
                    ProductId = productId,
                    SupplierId = quote.SupplierId,
                    SupplierSku = quote.SupplierSku,
                    BuyPrice = quote.BuyPrice,
                    StockQty = quote.StockQty,
                    LeadDays = quote.LeadDays,
                    Available = quote.Available && quote.StockQty > 0,
                    Source = quote.Source,
                    QuotedAt = quote.QuotedAt
                });
            }

            return Score(productId, quotes);
        }

        private async Task<SupplierQuotesResult?> LoadSnapshotAsync(
            int productId, string companyId, CancellationToken cancellationToken)
        {
            var rows = await this.storage.SelectAllErpProductSupplierOffers()
                .AsNoTracking()
                .Where(o => o.CompanyId == companyId && o.ProductId == productId)
                .ToListAsync(cancellationToken);
            if (rows.Count == 0) return null;

            var supplierIds = rows.Select(r => r.SupplierId).Distinct().ToList();
            var names = await this.storage.SelectAllSuppliers()
                .AsNoTracking()
                .Where(s => supplierIds.Contains(s.Id))
                .ToDictionaryAsync(s => s.Id, s => new { s.Name, s.FeedCode }, cancellationToken);

            var quotes = rows.Select(r => new SupplierQuoteDto
            {
                SupplierId = r.SupplierId,
                SupplierName = names.TryGetValue(r.SupplierId, out var n) ? n.Name : $"#{r.SupplierId}",
                FeedCode = names.TryGetValue(r.SupplierId, out var f) ? (f.FeedCode ?? "") : "",
                SupplierSku = r.SupplierSku,
                BuyPrice = r.BuyPrice,
                StockQty = r.StockQty,
                LeadDays = r.LeadDays,
                Available = r.Available && r.StockQty > 0,
                Source = r.Source,
                QuotedAt = r.QuotedAt
            }).ToList();

            var result = Score(productId, quotes);
            result.QuotedAt = rows.Max(r => r.QuotedAt);
            return result;
        }

        private static SupplierQuotesResult Score(int productId, List<SupplierQuoteDto> quotes)
        {
            foreach (var q in quotes) q.IsBest = false;

            var eligible = quotes
                .Where(q => q.Available && q.StockQty > 0)
                .OrderBy(q => q.BuyPrice)
                .ThenBy(q => q.LeadDays)
                .ThenBy(q => q.FeedCode == SupplierFeedCodes.Local ? 0 : 1)
                .ToList();

            string? reason = null;
            int? bestId = null;
            if (eligible.Count > 0)
            {
                eligible[0].IsBest = true;
                bestId = eligible[0].SupplierId;
                reason = eligible[0].FeedCode == SupplierFeedCodes.Local
                    ? "stock_local"
                    : "lowest_price";
            }

            return new SupplierQuotesResult
            {
                ProductId = productId,
                BestSupplierId = bestId,
                ScoreReason = reason,
                QuotedAt = quotes.Count == 0 ? DateTime.UtcNow : quotes.Max(q => q.QuotedAt),
                Offers = quotes
                    .OrderByDescending(q => q.IsBest)
                    .ThenByDescending(q => q.Available)
                    .ThenBy(q => q.BuyPrice)
                    .ToList()
            };
        }
    }
}
