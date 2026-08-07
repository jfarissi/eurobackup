using System;
using System.Collections.Generic;
using Backup.Web.Api.Server.Services.Audit;
using Backup.Web.Api.Server.Services.Tenancy;

namespace Backup.Web.Api.Server.Models.Entities
{
    /// <summary>Écriture comptable (P2 — RG-V7 / A3 / AC6).</summary>
    public class AccountingEntry : IHasCompanyId, IHasAuditTrail
    {
        public int Id { get; set; }
        public string EntryNumber { get; set; } = string.Empty;
        public DateTime EntryDate { get; set; } = DateTime.UtcNow;
        /// <summary>SalesInvoice, CreditNote, SupplierInvoice, Payment.</summary>
        public string JournalType { get; set; } = "SalesInvoice";
        public string ReferenceType { get; set; } = string.Empty;
        public int ReferenceId { get; set; }
        public string Description { get; set; } = string.Empty;
        public string Status { get; set; } = "Posted";
        public string? CompanyId { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public List<AccountingEntryLine> Lines { get; set; } = new();

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public string? UpdatedBy { get; set; }
    }

    public class AccountingEntryLine : IHasAuditTrail
    {
        public int Id { get; set; }
        public int AccountingEntryId { get; set; }
        public AccountingEntry? AccountingEntry { get; set; }
        public string AccountCode { get; set; } = string.Empty;
        public string AccountLabel { get; set; } = string.Empty;
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
        public int LineNumber { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public string? CreatedBy { get; set; }
        public string? UpdatedBy { get; set; }
    }
}
