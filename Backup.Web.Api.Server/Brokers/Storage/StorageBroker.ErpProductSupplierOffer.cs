using System;
using System.Linq;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Models.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Backup.Web.Api.Server.Brokers.Storage
{
    public partial class StorageBroker
    {
        public DbSet<ErpProductSupplierOffer> ErpProductSupplierOffers { get; set; } = null!;

        public IQueryable<ErpProductSupplierOffer> SelectAllErpProductSupplierOffers() =>
            this.ErpProductSupplierOffers.AsQueryable();

        public async ValueTask<ErpProductSupplierOffer> UpsertErpProductSupplierOfferAsync(ErpProductSupplierOffer row)
        {
            var existing = await this.ErpProductSupplierOffers
                .FirstOrDefaultAsync(o =>
                    o.CompanyId == row.CompanyId
                    && o.ProductId == row.ProductId
                    && o.SupplierId == row.SupplierId);

            if (existing == null)
            {
                if (row.Id == Guid.Empty) row.Id = Guid.NewGuid();
                EntityEntry<ErpProductSupplierOffer> entry = await this.ErpProductSupplierOffers.AddAsync(row);
                await this.SaveChangesAsync();
                return entry.Entity;
            }

            existing.SupplierSku = row.SupplierSku;
            existing.BuyPrice = row.BuyPrice;
            existing.StockQty = row.StockQty;
            existing.LeadDays = row.LeadDays;
            existing.Available = row.Available;
            existing.Source = row.Source;
            existing.QuotedAt = row.QuotedAt;
            await this.SaveChangesAsync();
            return existing;
        }
    }
}
