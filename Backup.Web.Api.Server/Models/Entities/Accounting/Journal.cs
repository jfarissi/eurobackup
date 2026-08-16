using System;
using Backup.Web.Api.Server.Services.Audit;
using Backup.Web.Api.Server.Services.Tenancy;

namespace Backup.Web.Api.Server.Models.Entities.Accounting
{
    /// <summary>Journal comptable (ACH/VEN/BAN/CAIS/OD/AN), isolé par société.</summary>
    public class Journal : IHasCompanyId, IHasAuditTrail
    {
        public int Id { get; set; }
        /// <summary>Code journal : ACH, VEN, BAN, CAIS, OD, AN.</summary>
        public string Code { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        /// <summary>Compte de contrepartie par défaut (ex. banque pour BAN, caisse pour CAIS).</summary>
        public string? CounterpartAccountCode { get; set; }
        public string? CompanyId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public string? CreatedBy { get; set; }
        public string? UpdatedBy { get; set; }
    }
}
