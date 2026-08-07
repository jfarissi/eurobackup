using System;
using System.Collections.Generic;
using Backup.Web.Api.Server.Services.Audit;
using Backup.Web.Api.Server.Services.Tenancy;

namespace Backup.Web.Api.Server.Models.Entities
{
    /// <summary>
    /// Avoir fournisseur (AF) : RG-AF1–5. Toujours lié à une facture fournisseur.
    /// Cycle : Draft → Validated (écriture GL inverse) → Applied (réduit l'encours fournisseur) / Cancelled.
    /// </summary>
    public class SupplierCreditNoteEntity : IHasCompanyId, IHasAuditTrail
    {
        public int Id { get; set; }
        public string CreditNoteNumber { get; set; } = string.Empty;
        public int SupplierId { get; set; }
        public Supplier? Supplier { get; set; }
        public int SupplierInvoiceId { get; set; }
        public SupplierInvoiceEntity? SupplierInvoice { get; set; }
        public DateTime Date { get; set; } = DateTime.UtcNow;
        public string Status { get; set; } = "Draft"; // Draft, Validated, Applied, Cancelled
        public decimal TotalHT { get; set; }
        public decimal TotalVat { get; set; }
        public decimal TotalTTC { get; set; }
        public string? Notes { get; set; }
        public string? CompanyId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public string? CreatedBy { get; set; }
        public string? UpdatedBy { get; set; }

        public List<SupplierCreditNoteLineEntity> Lines { get; set; } = new();
    }

    public class SupplierCreditNoteLineEntity : IHasAuditTrail
    {
        public int Id { get; set; }
        public int SupplierCreditNoteEntityId { get; set; }
        public SupplierCreditNoteEntity? SupplierCreditNote { get; set; }
        public string ProductKey { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal VatRate { get; set; } = 21.0m;
        public decimal TotalHT { get; set; }
        public decimal TotalTTC { get; set; }
        public int LineNumber { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public string? CreatedBy { get; set; }
        public string? UpdatedBy { get; set; }
    }
}
