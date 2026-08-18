using System;
using System.Collections.Generic;
using Backup.Web.Api.Server.Services.Audit;
using Backup.Web.Api.Server.Services.Tenancy;

namespace Backup.Web.Api.Server.Models.Entities.Accounting
{
    /// <summary>Rapprochement d'un relevé bancaire CSV avec les écritures du compte banque.</summary>
    public class BankReconciliation : IHasCompanyId, IHasAuditTrail
    {
        public int Id { get; set; }
        public string AccountCode { get; set; } = "512000";
        public string? FileName { get; set; }
        public DateTime StatementDate { get; set; }
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public decimal StatementBalance { get; set; }
        public decimal BookBalance { get; set; }
        /// <summary>Open / Balanced.</summary>
        public string Status { get; set; } = "Open";
        public DateTime? CompletedAt { get; set; }
        public string? CompletedBy { get; set; }
        public string? CompanyId { get; set; }
        public List<BankStatementLine> Lines { get; set; } = new();

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public string? CreatedBy { get; set; }
        public string? UpdatedBy { get; set; }
    }

    public class BankStatementLine
    {
        public int Id { get; set; }
        public int BankReconciliationId { get; set; }
        public BankReconciliation? BankReconciliation { get; set; }
        public DateTime OperationDate { get; set; }
        public string Label { get; set; } = string.Empty;
        public string? Reference { get; set; }
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
        public decimal? RunningBalance { get; set; }
        public bool IsMatched { get; set; }
        /// <summary>Reference / AmountDate / Amount / Manual.</summary>
        public string? MatchMethod { get; set; }
        public int? AccountingEntryId { get; set; }
        public int? AccountingEntryLineId { get; set; }
    }
}
