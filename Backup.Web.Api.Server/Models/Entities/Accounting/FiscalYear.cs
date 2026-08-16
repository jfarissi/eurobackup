using System;
using System.Collections.Generic;
using Backup.Web.Api.Server.Services.Audit;
using Backup.Web.Api.Server.Services.Tenancy;

namespace Backup.Web.Api.Server.Models.Entities.Accounting
{
    /// <summary>Exercice comptable (statut Open/Closed), isolé par société.</summary>
    public class FiscalYear : IHasCompanyId, IHasAuditTrail
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        /// <summary>Open / Closed.</summary>
        public string Status { get; set; } = "Open";
        public string? CompanyId { get; set; }
        public List<FiscalPeriod> Periods { get; set; } = new();

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public string? CreatedBy { get; set; }
        public string? UpdatedBy { get; set; }
    }

    /// <summary>Période mensuelle d'un exercice (verrouillage, TVA déclarée, rapprochement).</summary>
    public class FiscalPeriod : IHasCompanyId, IHasAuditTrail
    {
        public int Id { get; set; }
        public int FiscalYearId { get; set; }
        public FiscalYear? FiscalYear { get; set; }
        public int Year { get; set; }
        /// <summary>Mois calendaire (1 à 12).</summary>
        public int Month { get; set; }
        public bool IsLocked { get; set; }
        public bool IsVatDeclared { get; set; }
        public bool IsBankReconciled { get; set; }
        public string? CompanyId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public string? CreatedBy { get; set; }
        public string? UpdatedBy { get; set; }
    }
}
