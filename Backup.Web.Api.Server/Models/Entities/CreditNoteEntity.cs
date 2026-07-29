using System;
using System.Collections.Generic;

namespace Backup.Web.Api.Server.Models.Entities
{
using Backup.Web.Api.Server.Services.Tenancy;

    public class CreditNoteEntity : IHasCompanyId
    {
        public int Id { get; set; }
        public string CreditNoteNumber { get; set; } = string.Empty;
        public int CustomerId { get; set; }
        public Customer? Customer { get; set; }
        public int? SalesInvoiceId { get; set; }
        public DateTime Date { get; set; } = DateTime.UtcNow;
        public string Status { get; set; } = "Draft"; // Draft, Validated, Applied, Refunded
        public decimal TotalHT { get; set; }
        public decimal TotalVat { get; set; }
        public decimal TotalTTC { get; set; }
        public string? Notes { get; set; }
        public string? CompanyId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public List<CreditNoteLineEntity> Lines { get; set; } = new();
    }

    public class CreditNoteLineEntity
    {
        public int Id { get; set; }
        public int CreditNoteEntityId { get; set; }
        public CreditNoteEntity? CreditNote { get; set; }
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
