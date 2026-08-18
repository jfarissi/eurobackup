using System;
using System.Collections.Generic;
using Backup.Web.Api.Server.Services.Audit;
using Backup.Web.Api.Server.Services.Tenancy;

namespace Backup.Web.Api.Server.Models.Entities.Accounting
{
    /// <summary>Cabinet d'expertise comptable (société porteuse = FirmCompanyId).</summary>
    public class AccountingFirm : IHasAuditTrail
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Ice { get; set; }
        public string? TaxId { get; set; }
        public string FirmCompanyId { get; set; } = string.Empty;
        public List<AccountingFirmClient> Clients { get; set; } = new();

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public string? CreatedBy { get; set; }
        public string? UpdatedBy { get; set; }
    }

    /// <summary>Dossier client rattaché à un cabinet.</summary>
    public class AccountingFirmClient
    {
        public int Id { get; set; }
        public int AccountingFirmId { get; set; }
        public AccountingFirm? Firm { get; set; }
        public string ClientCompanyId { get; set; } = string.Empty;
        /// <summary>Saisie / Revue / Audit.</summary>
        public string MissionLevel { get; set; } = "Revue";
        public bool IsActive { get; set; } = true;
        public DateTime StartDate { get; set; } = DateTime.UtcNow.Date;
    }

    /// <summary>Annotation cabinet sur une société / écriture.</summary>
    public class AccountingAnnotation : IHasCompanyId, IHasAuditTrail
    {
        public int Id { get; set; }
        public string? CompanyId { get; set; }
        public int? AccountingEntryId { get; set; }
        /// <summary>Question, Correction, Information, Avertissement.</summary>
        public string Type { get; set; } = "Question";
        public string Message { get; set; } = string.Empty;
        public string? Author { get; set; }
        public bool IsResolved { get; set; }
        public DateTime? ResolvedAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public string? CreatedBy { get; set; }
        public string? UpdatedBy { get; set; }
    }
}
