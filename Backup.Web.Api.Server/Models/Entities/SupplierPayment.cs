using System;
using Backup.Web.Api.Server.Services.Audit;
using Backup.Web.Api.Server.Services.Tenancy;

namespace Backup.Web.Api.Server.Models.Entities
{
    /// <summary>Règlement fournisseur (paiement d'une facture d'achat).</summary>
    public class SupplierPayment : IHasCompanyId, IHasAuditTrail
    {
        public int Id { get; set; }
        public string? CompanyId { get; set; }
        public int SupplierInvoiceId { get; set; }
        public SupplierInvoiceEntity? SupplierInvoice { get; set; }

        public decimal Amount { get; set; }
        public DateTime PaidAt { get; set; } = DateTime.UtcNow;
        /// <summary>Cash, Card, Check, BankTransfer</summary>
        public string? Method { get; set; }
        public string? Reference { get; set; }
        /// <summary>Success, Cancelled</summary>
        public string Status { get; set; } = "Success";

        public string? CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public string? UpdatedBy { get; set; }
    }
}
