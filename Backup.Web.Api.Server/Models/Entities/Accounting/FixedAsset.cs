using System;
using System.Collections.Generic;
using Backup.Web.Api.Server.Services.Audit;
using Backup.Web.Api.Server.Services.Tenancy;

namespace Backup.Web.Api.Server.Models.Entities.Accounting
{
    /// <summary>Immobilisation corporelle/incorporelle avec plan d'amortissement mensuel.</summary>
    public class FixedAsset : IHasCompanyId, IHasAuditTrail
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Designation { get; set; } = string.Empty;
        public string AssetAccountCode { get; set; } = "218300";
        public string DepreciationAccountCode { get; set; } = "281500";
        public string ExpenseAccountCode { get; set; } = "681000";
        public DateTime AcquisitionDate { get; set; }
        public DateTime ServiceDate { get; set; }
        public decimal OriginValue { get; set; }
        public decimal ResidualValue { get; set; }
        public int DurationMonths { get; set; } = 36;
        /// <summary>Lineaire / Degressif.</summary>
        public string Mode { get; set; } = "Lineaire";
        public decimal? DecliningRate { get; set; }
        public decimal AccumulatedDepreciation { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime? DisposalDate { get; set; }
        public decimal? DisposalPrice { get; set; }
        public string? CompanyId { get; set; }
        public List<DepreciationScheduleLine> Schedule { get; set; } = new();

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public string? CreatedBy { get; set; }
        public string? UpdatedBy { get; set; }
    }

    public class DepreciationScheduleLine
    {
        public int Id { get; set; }
        public int FixedAssetId { get; set; }
        public FixedAsset? FixedAsset { get; set; }
        public int Year { get; set; }
        public int Month { get; set; }
        public decimal Charge { get; set; }
        public decimal Accumulated { get; set; }
        public decimal NetBookValue { get; set; }
        public bool IsPosted { get; set; }
        public int? AccountingEntryId { get; set; }
        public DateTime? PostedAt { get; set; }
    }
}
