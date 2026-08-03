using System;
using Backup.Web.Api.Server.Services.Tenancy;

namespace Backup.Web.Api.Server.Models.Entities
{
    /// <summary>
    /// RG-PT1–5 lite : tarif spécifique client par référence produit, consulté en fallback
    /// quand une ligne de devis/commande est saisie sans prix (UnitPrice &lt;= 0).
    /// </summary>
    public class CustomerPriceListItem : IHasCompanyId
    {
        public int Id { get; set; }
        public string? CompanyId { get; set; }
        public int CustomerId { get; set; }
        public Customer? Customer { get; set; }
        public string ProductKey { get; set; } = string.Empty;
        public decimal UnitPrice { get; set; }
        public decimal? VatRate { get; set; }
        public DateTime? ValidFrom { get; set; }
        public DateTime? ValidTo { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
