using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Brokers.Storage;
using Microsoft.EntityFrameworkCore;

namespace Backup.Web.Api.Server.Services.SupplierQuotes
{
    /// <summary>Offre magasin : CPrice + stock local (ErpProduct / StockItem).</summary>
    public sealed class LocalWarehouseAdapter : ISupplierFeedAdapter
    {
        private readonly IStorageBroker storage;

        public LocalWarehouseAdapter(IStorageBroker storage)
        {
            this.storage = storage;
        }

        public string FeedCode => SupplierFeedCodes.Local;

        public async Task<SupplierQuoteDto?> QuoteAsync(SupplierQuoteRequest request, CancellationToken cancellationToken)
        {
            var stock = request.CatalogStock ?? 0m;
            var keys = new[]
            {
                request.StockProductKey,
                request.Reference,
                request.Ean
            }.Where(k => !string.IsNullOrWhiteSpace(k)).Select(k => k!.Trim()).Distinct().ToList();

            if (keys.Count > 0)
            {
                var companyId = request.CompanyId;
                var item = await this.storage.SelectAllStock()
                    .AsNoTracking()
                    .Where(s => s.CompanyId == companyId && keys.Contains(s.ProductKey))
                    .OrderByDescending(s => s.LastUpdated)
                    .FirstOrDefaultAsync(cancellationToken);
                if (item != null)
                    stock = item.QuantityOnHand;
            }

            var cost = request.CatalogCost is > 0 ? request.CatalogCost.Value : 0m;
            return new SupplierQuoteDto
            {
                FeedCode = FeedCode,
                SupplierSku = request.Reference,
                BuyPrice = Math.Round(cost, 4, MidpointRounding.AwayFromZero),
                StockQty = stock,
                LeadDays = 0,
                Available = stock > 0,
                Source = "demo",
                QuotedAt = DateTime.UtcNow
            };
        }
    }
}
