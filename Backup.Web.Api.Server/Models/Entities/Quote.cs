using System;
using System.Collections.Generic;

namespace Backup.Web.Api.Server.Models.Entities
{
using Backup.Web.Api.Server.Services.Tenancy;

    public class Quote : IHasCompanyId
    {
        public int Id { get; set; }
        public string QuoteNumber { get; set; } = string.Empty;
        public int CustomerId { get; set; }
        public Customer? Customer { get; set; }
        public DateTime Date { get; set; } = DateTime.UtcNow;
        public DateTime ExpirationDate { get; set; } = DateTime.UtcNow.AddDays(30);
        public string Status { get; set; } = "Draft"; // Draft, Sent, Accepted, Rejected, Converted
        public decimal TotalHT { get; set; }
        public decimal TotalVat { get; set; }
        public decimal TotalTTC { get; set; }
        public string? Notes { get; set; }
        public string? CompanyId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public List<QuoteLine> Lines { get; set; } = new();
    }

    public class QuoteLine
    {
        public int Id { get; set; }
        public int QuoteId { get; set; }
        public Quote? Quote { get; set; }
        public string ProductKey { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal VatRate { get; set; } = 21.0m;
        public decimal TotalHT { get; set; }
        public decimal TotalTTC { get; set; }
        public int LineNumber { get; set; }
    }
}
