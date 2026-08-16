using System;
using Backup.Web.Api.Server.Services.Audit;
using Backup.Web.Api.Server.Services.Tenancy;

namespace Backup.Web.Api.Server.Models.Entities.Accounting
{
    /// <summary>
    /// Paramètres comptables d'une société : plan utilisé et comptes par défaut.
    /// Les valeurs par défaut reprennent les comptes en dur d'AccountingLedger (continuité).
    /// </summary>
    public class CompanyAccountingSettings : IHasCompanyId, IHasAuditTrail
    {
        public int Id { get; set; }
        /// <summary>Société propriétaire (requis, unique).</summary>
        public string CompanyId { get; set; } = string.Empty;
        /// <summary>PcmMaroc / PcgEurope.</summary>
        public string PlanType { get; set; } = "PcgEurope";
        public string CustomerAccountCode { get; set; } = "411000";
        public string SupplierAccountCode { get; set; } = "401000";
        public string SalesAccountCode { get; set; } = "701000";
        public string PurchaseAccountCode { get; set; } = "607000";
        public string VatCollectedAccountCode { get; set; } = "445710";
        public string VatDeductibleAccountCode { get; set; } = "445660";
        public string BankAccountCode { get; set; } = "512000";
        public string CashAccountCode { get; set; } = "530000";
        public string CustomerDepositAccountCode { get; set; } = "419000";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public string? CreatedBy { get; set; }
        public string? UpdatedBy { get; set; }
    }
}
