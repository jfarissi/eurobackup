using System;
using System.Collections.Generic;

namespace Backup.Web.Api.Server.Models
{
    public class StoreChatQuote
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string SessionId { get; set; } = string.Empty;
        public string Number { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public string? PdfBase64 { get; set; }
        public string? FileName { get; set; }
        public string LinesJson { get; set; } = "[]";
        public Guid? SalesProjectId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class StoreChatOrder
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string SessionId { get; set; } = string.Empty;
        public string Status { get; set; } = "pending";
        public string? StripeSessionId { get; set; }
        public string? InvoiceNumber { get; set; }
        public decimal TotalAmount { get; set; }
        public string? InvoicePdfBase64 { get; set; }
        public string? InvoiceFileName { get; set; }
        public string LinesJson { get; set; } = "[]";
        public Guid? SalesProjectId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? PaidAt { get; set; }
    }

    /// <summary>Tour Q/R StoreChat — revue QA / correction a posteriori.</summary>
    public class StoreChatTurn
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string SessionId { get; set; } = string.Empty;
        public Guid? SalesProjectId { get; set; }
        public string? PreferredLanguage { get; set; }
        public string? DomainId { get; set; }
        public string? ClientIntent { get; set; }
        public string? ActionType { get; set; }
        public string? UserText { get; set; }
        public string? ReplyText { get; set; }
        public string? ProductsJson { get; set; }
        /// <summary>null | ok | bad | fixed</summary>
        public string? ReviewStatus { get; set; }
        public string? ReviewNote { get; set; }
        /// <summary>null | auto | manual</summary>
        public string? ReviewSource { get; set; }
        /// <summary>True quand le bug lié à ce tour a été corrigé dans le code.</summary>
        public bool IsCorrected { get; set; }
        public DateTime? CorrectedAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
