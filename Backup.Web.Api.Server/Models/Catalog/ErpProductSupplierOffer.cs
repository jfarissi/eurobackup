using System;
using Backup.Web.Api.Server.Services.Tenancy;

namespace Backup.Web.Api.Server.Models.Catalog
{
    /// <summary>Snapshot d'offre fournisseur (prix + stock + délai) pour un produit.</summary>
    public class ErpProductSupplierOffer : IHasCompanyId
    {
        public Guid Id { get; set; }
        public string? CompanyId { get; set; }
        public int ProductId { get; set; }
        public int SupplierId { get; set; }
        public string? SupplierSku { get; set; }
        public decimal BuyPrice { get; set; }
        public decimal StockQty { get; set; }
        public int LeadDays { get; set; }
        public bool Available { get; set; }
        /// <summary>demo | edi | http</summary>
        public string Source { get; set; } = "demo";
        public DateTime QuotedAt { get; set; } = DateTime.UtcNow;
    }
}
