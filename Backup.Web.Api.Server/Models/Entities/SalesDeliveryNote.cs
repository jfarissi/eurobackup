using System;
using System.Collections.Generic;
using Backup.Web.Api.Server.Services.Audit;
using Backup.Web.Api.Server.Services.Sales;
using Backup.Web.Api.Server.Services.Tenancy;

namespace Backup.Web.Api.Server.Models.Entities
{
    /// <summary>
    /// Bon de livraison client (vente) : Commande → BL → Facture.
    /// </summary>
    public class SalesDeliveryNote : IHasCompanyId, IHasSoftDelete, IHasArchive, IHasAuditTrail
    {
        public int Id { get; set; }
        public string DeliveryNumber { get; set; } = string.Empty;
        public int CustomerId { get; set; }
        public Customer? Customer { get; set; }
        public int? SalesOrderId { get; set; }
        public SalesOrder? SalesOrder { get; set; }
        public int? SalesInvoiceId { get; set; }
        public SalesInvoice? SalesInvoice { get; set; }
        public DateTime DeliveryDate { get; set; } = DateTime.UtcNow;
        /// <summary>Draft, Sent, Delivered, Invoiced, Cancelled</summary>
        public string Status { get; set; } = "Draft";
        public string? DeliveryAddress { get; set; }
        public decimal TotalHT { get; set; }
        public decimal TotalVat { get; set; }
        public decimal TotalTTC { get; set; }
        public string? Notes { get; set; }
        public string? CompanyId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }
        public string? DeletedBy { get; set; }
        public bool IsArchived { get; set; }
        public DateTime? ArchivedAt { get; set; }
        public string? ArchivedBy { get; set; }
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public string? CreatedBy { get; set; }
        public string? UpdatedBy { get; set; }

        public List<SalesDeliveryNoteLine> Lines { get; set; } = new();
    }

    public class SalesDeliveryNoteLine : IHasAuditTrail
    {
        public int Id { get; set; }
        public int SalesDeliveryNoteId { get; set; }
        public SalesDeliveryNote? SalesDeliveryNote { get; set; }
        public string ProductKey { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal OrderedQuantity { get; set; }
        public decimal DeliveredQuantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal VatRate { get; set; } = 21.0m;
        public decimal TotalHT { get; set; }
        public decimal TotalTTC { get; set; }
        public int LineNumber { get; set; }
        /// <summary>RG-LS1–5 lite : n° de lot saisi manuellement (traçabilité simple, sans FEFO).</summary>
        public string? LotNumber { get; set; }
        /// <summary>Fournisseur associé à la ligne (repris de la commande).</summary>
        public int? SupplierId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public string? CreatedBy { get; set; }
        public string? UpdatedBy { get; set; }
    }
}
