using System;
using System.Collections.Generic;

namespace Backup.Web.Api.Server.Models.Entities
{
using Backup.Web.Api.Server.Services.Audit;
using Backup.Web.Api.Server.Services.Tenancy;

    public class SupplierInvoiceEntity : IHasCompanyId, IHasAuditTrail
    {
        public int Id { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public int SupplierId { get; set; }
        public Supplier? Supplier { get; set; }
        public int? DocumentId { get; set; } // FK vers Document (parsing PDF existant)
        public int? PurchaseOrderId { get; set; }
        public PurchaseOrder? PurchaseOrder { get; set; }
        /// <summary>Réception source si facture créée depuis un BL / réception.</summary>
        public int? ReceiptId { get; set; }
        public Receipt? Receipt { get; set; }
        public DateTime Date { get; set; } = DateTime.UtcNow;
        public DateTime DueDate { get; set; } = DateTime.UtcNow.AddDays(30);
        public string Status { get; set; } = "Draft"; // Draft, Validated, Paid, Cancelled
        public decimal TotalHT { get; set; }
        public decimal TotalVat { get; set; }
        public decimal TotalTTC { get; set; }
        /// <summary>RG-CP1 : devise figée à la création (copiée de Company.DefaultCurrencyCode), gelée hors Draft.</summary>
        public string CurrencyCode { get; set; } = "EUR";
        public string? Notes { get; set; }
        public string? CompanyId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public string? CreatedBy { get; set; }
        public string? UpdatedBy { get; set; }

        public List<SupplierInvoiceLineEntity> Lines { get; set; } = new();
    }

    public class SupplierInvoiceLineEntity : IHasAuditTrail
    {
        public int Id { get; set; }
        public int SupplierInvoiceEntityId { get; set; }
        public SupplierInvoiceEntity? SupplierInvoice { get; set; }
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
