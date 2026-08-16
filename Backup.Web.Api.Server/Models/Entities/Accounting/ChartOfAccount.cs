using System;
using Backup.Web.Api.Server.Services.Audit;
using Backup.Web.Api.Server.Services.Tenancy;

namespace Backup.Web.Api.Server.Models.Entities.Accounting
{
    /// <summary>Compte du plan comptable (PCM Maroc / PCG Europe), isolé par société.</summary>
    public class ChartOfAccount : IHasCompanyId, IHasAuditTrail
    {
        public int Id { get; set; }
        /// <summary>Numéro de compte (ex. "342100").</summary>
        public string AccountNumber { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string? LabelArabic { get; set; }
        /// <summary>Classe comptable (1 à 8).</summary>
        public int AccountClass { get; set; }
        /// <summary>Actif / Passif / Charge / Produit / CapitauxPropres.</summary>
        public string AccountType { get; set; } = "Actif";
        public bool IsLettrable { get; set; }
        public bool IsBilan { get; set; }
        public bool IsResultat { get; set; }
        public int? ParentId { get; set; }
        public ChartOfAccount? Parent { get; set; }
        public string? CompanyId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public string? CreatedBy { get; set; }
        public string? UpdatedBy { get; set; }
    }
}
