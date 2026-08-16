using System;
using Backup.Web.Api.Server.Services.Audit;
using Backup.Web.Api.Server.Services.Tenancy;

namespace Backup.Web.Api.Server.Models.Entities.Accounting
{
    /// <summary>
    /// Mapping TVA par taux et par société (Phase 2) : ventile la TVA collectée / déductible
    /// sur des comptes distincts selon le taux (ex. 20% → 445720, 10% → 445721).
    /// Table vide au départ : en l'absence de ligne pour un taux, le compte par défaut des
    /// paramètres comptables de la société s'applique (comportement historique garanti).
    /// </summary>
    public class CompanyVatRateAccount : IHasCompanyId, IHasAuditTrail
    {
        public int Id { get; set; }
        /// <summary>Taux de TVA concerné (ex. 21.00).</summary>
        public decimal Rate { get; set; }
        /// <summary>Compte de TVA collectée (ventes) pour ce taux.</summary>
        public string CollectedAccountCode { get; set; } = string.Empty;
        /// <summary>Compte de TVA déductible (achats) pour ce taux.</summary>
        public string DeductibleAccountCode { get; set; } = string.Empty;
        public string? CompanyId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public string? CreatedBy { get; set; }
        public string? UpdatedBy { get; set; }
    }
}
