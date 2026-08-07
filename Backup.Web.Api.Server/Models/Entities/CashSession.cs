using System;
using System.Collections.Generic;

namespace Backup.Web.Api.Server.Models.Entities
{
using Backup.Web.Api.Server.Services.Audit;
using Backup.Web.Api.Server.Services.Tenancy;

    public class CashSession : IHasCompanyId, IHasAuditTrail
    {
        public int Id { get; set; }
        public string SessionNumber { get; set; } = string.Empty;
        public DateTime OpenedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ClosedAt { get; set; }
        public decimal OpeningBalance { get; set; }
        public decimal? ClosingBalance { get; set; }
        public decimal? ExpectedClosingBalance { get; set; }
        public string Status { get; set; } = "Open"; // Open, Closed
        public string? OpenedBy { get; set; }
        public string? ClosedBy { get; set; }
        public string? CompanyId { get; set; }

        public List<CashOperation> Operations { get; set; } = new();

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public string? CreatedBy { get; set; }
        public string? UpdatedBy { get; set; }
    }

    public class CashOperation : IHasAuditTrail
    {
        public int Id { get; set; }
        public int CashSessionId { get; set; }
        public CashSession? CashSession { get; set; }
        public string OperationType { get; set; } = "Deposit"; // Deposit, Withdrawal, SalePayment
        public decimal Amount { get; set; }
        public string? Description { get; set; }
        public string? ReferenceDocument { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public string? UpdatedBy { get; set; }
    }
}
