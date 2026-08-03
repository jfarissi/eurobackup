using System;
using System.Collections.Generic;
using Backup.Web.Api.Server.Services.Sales;
using Backup.Web.Api.Server.Services.Tenancy;

namespace Backup.Web.Api.Server.Models.Entities
{
    /// <summary>
    /// Facture proforma (PF) : RG-PF1–4. Apparence de facture, sans effet GL ni stock.
    /// Cycle : Draft → Sent / Cancelled. Ne peut jamais être convertie directement en facture.
    /// </summary>
    public class Proforma : IHasCompanyId, IHasSoftDelete
    {
        public int Id { get; set; }
        public string ProformaNumber { get; set; } = string.Empty;
        public int CustomerId { get; set; }
        public Customer? Customer { get; set; }
        public int? QuoteId { get; set; }
        public Quote? Quote { get; set; }
        public int? SalesOrderId { get; set; }
        public SalesOrder? SalesOrder { get; set; }
        public DateTime Date { get; set; } = DateTime.UtcNow;
        /// <summary>Draft, Sent, Cancelled</summary>
        public string Status { get; set; } = "Draft";
        public decimal TotalHT { get; set; }
        public decimal TotalVat { get; set; }
        public decimal TotalTTC { get; set; }
        /// <summary>RG-CP1 : devise figée à la création (copiée de Company.DefaultCurrencyCode), gelée hors Draft.</summary>
        public string CurrencyCode { get; set; } = "EUR";
        public string? Notes { get; set; }
        public string? CompanyId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }
        public string? DeletedBy { get; set; }

        public List<ProformaLine> Lines { get; set; } = new();
    }

    public class ProformaLine
    {
        public int Id { get; set; }
        public int ProformaId { get; set; }
        public Proforma? Proforma { get; set; }
        public string ProductKey { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal VatRate { get; set; } = 21.0m;
        public decimal TotalHT { get; set; }
        public decimal TotalTTC { get; set; }
        public int LineNumber { get; set; }
    }
}
