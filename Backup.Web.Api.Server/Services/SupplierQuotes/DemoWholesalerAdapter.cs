using System;
using System.Threading;
using System.Threading.Tasks;

namespace Backup.Web.Api.Server.Services.SupplierQuotes
{
    /// <summary>Grossiste démo : prix/stock déterministes avec jitter pour le live SignalR.</summary>
    public sealed class DemoWholesalerAdapter : ISupplierFeedAdapter
    {
        private readonly decimal priceFactor;
        private readonly int baseLeadDays;
        private readonly int stockBase;

        public DemoWholesalerAdapter(string feedCode, decimal priceFactor, int baseLeadDays, int stockBase)
        {
            FeedCode = feedCode;
            this.priceFactor = priceFactor;
            this.baseLeadDays = baseLeadDays;
            this.stockBase = stockBase;
        }

        public string FeedCode { get; }

        public Task<SupplierQuoteDto?> QuoteAsync(SupplierQuoteRequest request, CancellationToken cancellationToken)
        {
            var hash = Hash(request.ProductId, FeedCode);
            var tick = DateTime.UtcNow.Ticks / TimeSpan.TicksPerSecond / 15;
            var wobble = 1m + 0.035m * (decimal)Math.Sin(tick * 0.55 + hash % 17);

            var baseCost = request.CatalogCost is > 0 ? request.CatalogCost.Value : 24.90m;
            var buy = Math.Round(baseCost * priceFactor * wobble, 2, MidpointRounding.AwayFromZero);
            if (buy < 0.5m) buy = 0.5m;

            var outOfStock = hash % 13 == 0 && tick % 4 == 0;
            var stock = outOfStock
                ? 0m
                : Math.Max(0, stockBase + (hash % 40) - (int)(tick % 5));

            var sku = string.IsNullOrWhiteSpace(request.Reference)
                ? $"FD-{request.ProductId}"
                : request.Reference.Trim();

            return Task.FromResult<SupplierQuoteDto?>(new SupplierQuoteDto
            {
                FeedCode = FeedCode,
                SupplierSku = sku,
                BuyPrice = buy,
                StockQty = stock,
                LeadDays = baseLeadDays,
                Available = stock > 0,
                Source = "demo",
                QuotedAt = DateTime.UtcNow
            });
        }

        private static int Hash(int productId, string feed)
        {
            unchecked
            {
                var h = 17;
                h = h * 31 + productId;
                foreach (var c in feed)
                    h = h * 31 + c;
                return Math.Abs(h);
            }
        }
    }
}
