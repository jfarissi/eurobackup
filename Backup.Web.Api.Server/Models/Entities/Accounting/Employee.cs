using System;
using Backup.Web.Api.Server.Services.Audit;
using Backup.Web.Api.Server.Services.Tenancy;

namespace Backup.Web.Api.Server.Models.Entities.Accounting
{
    /// <summary>Salarié pour le module paie / CNSS Maroc.</summary>
    public class Employee : IHasCompanyId, IHasAuditTrail
    {
        public int Id { get; set; }
        public string LastName { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string? CnssNumber { get; set; }
        public decimal BaseSalary { get; set; }
        public decimal Overtime { get; set; }
        public decimal Bonuses { get; set; }
        public decimal BenefitsInKind { get; set; }
        public DateTime HireDate { get; set; } = DateTime.UtcNow.Date;
        public DateTime? ExitDate { get; set; }
        public bool IsActive { get; set; } = true;
        public string? CompanyId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public string? CreatedBy { get; set; }
        public string? UpdatedBy { get; set; }
    }

    /// <summary>Bulletin de paie mensuel (CNSS / AMO / IGR).</summary>
    public class Payslip : IHasCompanyId, IHasAuditTrail
    {
        public int Id { get; set; }
        public int EmployeeId { get; set; }
        public Employee? Employee { get; set; }
        public int Year { get; set; }
        public int Month { get; set; }
        public decimal BaseSalary { get; set; }
        public decimal Overtime { get; set; }
        public decimal Bonuses { get; set; }
        public decimal BenefitsInKind { get; set; }
        public decimal Gross { get; set; }
        public decimal CnssEmployee { get; set; }
        public decimal CnssEmployer { get; set; }
        public decimal AmoEmployee { get; set; }
        public decimal AmoEmployer { get; set; }
        public decimal Igr { get; set; }
        public decimal Net { get; set; }
        public bool IsPosted { get; set; }
        public int? AccountingEntryId { get; set; }
        public bool IsExportedCnss { get; set; }
        public string? CompanyId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public string? CreatedBy { get; set; }
        public string? UpdatedBy { get; set; }
    }
}
