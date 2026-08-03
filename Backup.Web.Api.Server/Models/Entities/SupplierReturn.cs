using System;
using System.Collections.Generic;
using Backup.Web.Api.Server.Services.Sales;
using Backup.Web.Api.Server.Services.Tenancy;

namespace Backup.Web.Api.Server.Models.Entities
{
    /// <summary>
    /// Bon de retour fournisseur (BRF) : RG-BRF1–5.
    /// Cycle : Draft → Shipped (stock Out) / Cancelled (reverse stock si déjà expédié).
    /// </summary>
    public class SupplierReturn : IHasCompanyId, IHasSoftDelete
    {
        public int Id { get; set; }
        public string ReturnNumber { get; set; } = string.Empty;
        public int SupplierId { get; set; }
        public Supplier? Supplier { get; set; }
        public int? PurchaseOrderId { get; set; }
        public PurchaseOrder? PurchaseOrder { get; set; }
        public int? ReceiptId { get; set; }
        public Receipt? Receipt { get; set; }
        public int? SupplierInvoiceId { get; set; }
        public SupplierInvoiceEntity? SupplierInvoice { get; set; }
        public DateTime Date { get; set; } = DateTime.UtcNow;
        /// <summary>Draft, Shipped, Cancelled</summary>
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

        /// <summary>True dès que le stock a été impacté (Shipped), pour piloter la réversibilité de Cancel.</summary>
        public bool StockApplied { get; set; }

        /// <summary>Avoir fournisseur généré depuis ce retour (RG-BRF4), si déjà créé.</summary>
        public int? CreditNoteId { get; set; }

        public List<SupplierReturnLine> Lines { get; set; } = new();
    }

    public class SupplierReturnLine
    {
        public int Id { get; set; }
        public int SupplierReturnId { get; set; }
        public SupplierReturn? SupplierReturn { get; set; }
        public string ProductKey { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal VatRate { get; set; } = 21.0m;
        public decimal TotalHT { get; set; }
        public decimal TotalTTC { get; set; }
        public int LineNumber { get; set; }
        /// <summary>Conforme, Degraded, NonConforming — motif qualité du retour.</summary>
        public string? QualityStatus { get; set; }
    }
}
