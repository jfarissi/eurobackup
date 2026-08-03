using System;
using Backup.Web.Api.Server.Services.Tenancy;

namespace Backup.Web.Api.Server.Models.Entities
{
    /// <summary>
    /// Règlement client (aligné Pulse ErpPayments) — historique d'un paiement sur facture vente.
    /// </summary>
    public class Payment : IHasCompanyId
    {
        public int Id { get; set; }
        public string? CompanyId { get; set; }
        public int SalesInvoiceId { get; set; }
        public SalesInvoice? SalesInvoice { get; set; }

        public decimal Amount { get; set; }
        public decimal RoundingDifference { get; set; }
        public decimal ReceivedAmount { get; set; }
        public decimal ChangeAmount { get; set; }

        public DateTime PaidAt { get; set; } = DateTime.UtcNow;
        /// <summary>Cash, Card, Check, BankTransfer / Transfer</summary>
        public string? Method { get; set; }
        public string? Reference { get; set; }
        public string? Bank { get; set; }
        /// <summary>Success, Cancelled, Refunded, Pending</summary>
        public string Status { get; set; } = "Success";

        public int? CashSessionId { get; set; }
        public string? TerminalTransactionId { get; set; }

        public string? CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
