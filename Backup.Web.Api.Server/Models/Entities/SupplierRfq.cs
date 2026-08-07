using System;
using System.Collections.Generic;
using Backup.Web.Api.Server.Services.Audit;
using Backup.Web.Api.Server.Services.Sales;
using Backup.Web.Api.Server.Services.Tenancy;

namespace Backup.Web.Api.Server.Models.Entities
{
    /// <summary>
    /// Demande de prix fournisseur (DPF) : RG-DPF1–4.
    /// Cycle : Draft → Sent → Awaiting → Processed (converti en CDF) / Cancelled.
    /// </summary>
    public class SupplierRfq : IHasCompanyId, IHasSoftDelete, IHasAuditTrail
    {
        public int Id { get; set; }
        public string RfqNumber { get; set; } = string.Empty;
        public int? SupplierId { get; set; }
        public Supplier? Supplier { get; set; }
        public DateTime Date { get; set; } = DateTime.UtcNow;
        /// <summary>Draft, Sent, Awaiting, Processed, Cancelled</summary>
        public string Status { get; set; } = "Draft";
        public string? Notes { get; set; }
        public string? CompanyId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }
        public string? DeletedBy { get; set; }
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public string? CreatedBy { get; set; }
        public string? UpdatedBy { get; set; }

        /// <summary>RG-DPF4 : commande fournisseur Draft créée depuis convert-to-purchase-order.</summary>
        public int? PurchaseOrderId { get; set; }

        public List<SupplierRfqLine> Lines { get; set; } = new();
    }

    public class SupplierRfqLine : IHasAuditTrail
    {
        public int Id { get; set; }
        public int SupplierRfqId { get; set; }
        public SupplierRfq? SupplierRfq { get; set; }
        public string ProductKey { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal EstimatedUnitPrice { get; set; }
        public int LineNumber { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public string? CreatedBy { get; set; }
        public string? UpdatedBy { get; set; }
    }
}
