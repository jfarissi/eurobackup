using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Backup.Web.Api.Server.Services.Tenancy;

namespace Backup.Web.Api.Server.Models.Entities
{
    /// <summary>
    /// Réception fournisseur (équivalent Pulse/EuroBrico ErpReceipts).
    /// Créée via Comptabiliser depuis un Document parsé (BonLivraison).
    /// </summary>
    public class Receipt : IHasCompanyId
    {
        public int Id { get; set; }
        public string ReceiptNumber { get; set; } = string.Empty;
        public int? PurchaseOrderId { get; set; }
        public PurchaseOrder? PurchaseOrder { get; set; }
        public int SupplierId { get; set; }
        public Supplier? Supplier { get; set; }
        /// <summary>Document parsé source (BonLivraison) dans la table Documents.</summary>
        public int? DocumentId { get; set; }
        public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
        public string Status { get; set; } = "Posted"; // Draft, QualityHold, Posted
        public string? Notes { get; set; }
        public string? CompanyId { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public List<ReceiptLine> Lines { get; set; } = new();
    }

    public class ReceiptLine
    {
        public int Id { get; set; }
        public int ReceiptId { get; set; }
        [JsonIgnore]
        public Receipt? Receipt { get; set; }
        public string ProductKey { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal QuantityReceived { get; set; }
        public decimal UnitPriceExclTax { get; set; }
        public decimal TaxRatePercent { get; set; } = 21m;
        public decimal LineAmountExclTax { get; set; }
        public decimal LineTaxAmount { get; set; }
        public int LineNumber { get; set; }
    }
}
