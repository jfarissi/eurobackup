using System;
using System.Collections.Generic;
using Backup.Web.Api.Server.Services.Sales;
using Backup.Web.Api.Server.Services.Tenancy;

namespace Backup.Web.Api.Server.Models.Entities
{
    public class CreditNoteEntity : IHasCompanyId, IHasSoftDelete, IHasArchive
    {
        public int Id { get; set; }
        public string CreditNoteNumber { get; set; } = string.Empty;
        public int CustomerId { get; set; }
        public Customer? Customer { get; set; }
        public int? SalesInvoiceId { get; set; }
        /// <summary>RG-AC4 : retour physique (BRC) à l'origine de cet avoir, le cas échéant.</summary>
        public int? SalesReturnId { get; set; }
        public SalesReturn? SalesReturn { get; set; }
        public DateTime Date { get; set; } = DateTime.UtcNow;
        public string Status { get; set; } = "Draft"; // Draft, Validated, Applied, Refunded
        public decimal TotalHT { get; set; }
        public decimal TotalVat { get; set; }
        public decimal TotalTTC { get; set; }
        /// <summary>RG-CP1 : devise figée à la création (copiée de Company.DefaultCurrencyCode), gelée hors Draft.</summary>
        public string CurrencyCode { get; set; } = "EUR";
        public string? Notes { get; set; }
        public string? CompanyId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }
        public string? DeletedBy { get; set; }
        public bool IsArchived { get; set; }
        public DateTime? ArchivedAt { get; set; }
        public string? ArchivedBy { get; set; }

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
