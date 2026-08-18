using System;
using System.Collections.Generic;
using Backup.Web.Api.Server.Services.Audit;
using Backup.Web.Api.Server.Services.Tenancy;

namespace Backup.Web.Api.Server.Models.Entities.Accounting
{
    /// <summary>
    /// Déclaration TVA mensuelle (snapshot figé à la validation).
    /// Collectée / déductible par taux, crédit reporté, TVA nette.
    /// </summary>
    public class VatDeclaration : IHasCompanyId, IHasAuditTrail
    {
        public int Id { get; set; }
        public int Year { get; set; }
        /// <summary>Mois calendaire (1 à 12).</summary>
        public int Month { get; set; }
        public int? FiscalPeriodId { get; set; }
        public FiscalPeriod? FiscalPeriod { get; set; }
        /// <summary>Draft (calcul live, non persisté) / Declared (snapshot).</summary>
        public string Status { get; set; } = "Declared";
        public decimal TotalCollected { get; set; }
        public decimal TotalDeductible { get; set; }
        public decimal PreviousCredit { get; set; }
        public decimal NetToPay { get; set; }
        public DateTime? DeclaredAt { get; set; }
        public string? DeclaredBy { get; set; }
        public string? CompanyId { get; set; }
        public List<VatDeclarationLine> Lines { get; set; } = new();

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public string? CreatedBy { get; set; }
        public string? UpdatedBy { get; set; }
    }

    /// <summary>Ventilation d'une déclaration TVA par taux.</summary>
    public class VatDeclarationLine
    {
        public int Id { get; set; }
        public int VatDeclarationId { get; set; }
        public VatDeclaration? VatDeclaration { get; set; }
        /// <summary>Taux (ex. 21). 0 = non ventilé (compte par défaut sans mapping).</summary>
        public decimal Rate { get; set; }
        public decimal CollectedBase { get; set; }
        public decimal CollectedVat { get; set; }
        public decimal DeductibleBase { get; set; }
        public decimal DeductibleVat { get; set; }
    }
}
