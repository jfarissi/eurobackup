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
}
