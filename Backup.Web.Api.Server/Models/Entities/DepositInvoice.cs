using System;
using Backup.Web.Api.Server.Services.Tenancy;

namespace Backup.Web.Api.Server.Models.Entities
{
    /// <summary>
    /// Facture d'acompte (AAC) : RG-AA1–4. Toujours liée à une commande.
    /// Cycle : Draft → Validated (GL 411/419) → Applied (déduite d'une facture finale) / Cancelled (reverse GL si Validated).
    /// </summary>
    public class DepositInvoice : IHasCompanyId
    {
        public int Id { get; set; }
        public string DepositNumber { get; set; } = string.Empty;
        public int CustomerId { get; set; }
        public Customer? Customer { get; set; }
        public int SalesOrderId { get; set; }
        public SalesOrder? SalesOrder { get; set; }
        public DateTime Date { get; set; } = DateTime.UtcNow;
        public decimal AmountHT { get; set; }
        public decimal VatRate { get; set; } = 21.0m;
        public decimal AmountTTC { get; set; }
        /// <summary>Draft, Validated, Applied, Cancelled</summary>
        public string Status { get; set; } = "Draft";
        /// <summary>RG-CP1 : devise figée à la création (copiée de Company.DefaultCurrencyCode), gelée hors Draft.</summary>
        public string CurrencyCode { get; set; } = "EUR";
        public string? Notes { get; set; }
        public string? CompanyId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>RG-AA3 : facture de solde à laquelle l'acompte a été appliqué (déduction).</summary>
        public int? AppliedSalesInvoiceId { get; set; }
        public SalesInvoice? AppliedSalesInvoice { get; set; }
        public DateTime? AppliedAt { get; set; }
    }
}
