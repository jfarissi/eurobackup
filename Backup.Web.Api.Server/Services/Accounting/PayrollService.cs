using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Models.Entities;
using Backup.Web.Api.Server.Models.Entities.Accounting;
using Backup.Web.Api.Server.Services.Numbering;
using Backup.Web.Api.Server.Services.Sales;
using Backup.Web.Api.Server.Services.Tenancy;

namespace Backup.Web.Api.Server.Services.Accounting
{
    /// <summary>Paie Maroc : bulletins (CNSS / AMO / IGR), écriture SAL, export EDI CNSS TXT/XML.</summary>
    public static class PayrollService
    {
        public const decimal CnssCeiling = 6000m;
        public const decimal CnssEmployeeRate = 0.0429m;
        public const decimal CnssEmployerRate = 0.1309m;
        public const decimal AmoRate = 0.0226m;

        public sealed class EmployeeDto
        {
            public int Id { get; set; }
            public string LastName { get; set; } = string.Empty;
            public string FirstName { get; set; } = string.Empty;
            public string? CnssNumber { get; set; }
            public decimal BaseSalary { get; set; }
            public decimal Overtime { get; set; }
            public decimal Bonuses { get; set; }
            public decimal BenefitsInKind { get; set; }
            public DateTime HireDate { get; set; }
            public DateTime? ExitDate { get; set; }
            public bool IsActive { get; set; }
        }

        public sealed class EmployeeForm
        {
            public string LastName { get; set; } = string.Empty;
            public string FirstName { get; set; } = string.Empty;
            public string? CnssNumber { get; set; }
            public decimal BaseSalary { get; set; }
            public decimal Overtime { get; set; }
            public decimal Bonuses { get; set; }
            public decimal BenefitsInKind { get; set; }
            public DateTime HireDate { get; set; }
            public DateTime? ExitDate { get; set; }
            public bool IsActive { get; set; } = true;
        }

        public sealed class PayslipDto
        {
            public int Id { get; set; }
            public int EmployeeId { get; set; }
            public string EmployeeName { get; set; } = string.Empty;
            public string? CnssNumber { get; set; }
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
        }

        public sealed class PeriodSummaryDto
        {
            public int Year { get; set; }
            public int Month { get; set; }
            public int PayslipCount { get; set; }
            public decimal TotalGross { get; set; }
            public decimal TotalNet { get; set; }
            public decimal TotalCnss { get; set; }
            public decimal TotalIgr { get; set; }
            public bool AllPosted { get; set; }
            public List<PayslipDto> Payslips { get; set; } = new();
        }

        public sealed class PostResultDto
        {
            public int PostedCount { get; set; }
            public int? AccountingEntryId { get; set; }
            public string? EntryNumber { get; set; }
        }

        public sealed class ExportFile
        {
            public byte[] Content { get; set; } = Array.Empty<byte>();
            public string FileName { get; set; } = string.Empty;
        }

        public static List<EmployeeDto> ListEmployees(IStorageBroker storage, string? companyId) =>
            storage.SelectAllEmployees()
                .ForCompany(companyId)
                .AsEnumerable()
                .OrderBy(e => e.LastName).ThenBy(e => e.FirstName)
                .Select(ToEmployeeDto)
                .ToList();

        public static async Task<(EmployeeDto? Dto, string? Error)> UpsertEmployeeAsync(
            IStorageBroker storage, string? companyId, int? id, EmployeeForm form, string? actor)
        {
            if (string.IsNullOrWhiteSpace(form.LastName) || string.IsNullOrWhiteSpace(form.FirstName))
                return (null, "Le nom et le prénom sont obligatoires.");
            if (form.BaseSalary < 0) return (null, "Le salaire de base ne peut pas être négatif.");

            Employee employee;
            if (id is int existingId)
            {
                var found = storage.SelectAllEmployees().ForCompany(companyId).FirstOrDefault(e => e.Id == existingId);
                if (found == null) return (null, "Salarié introuvable.");
                employee = found;
            }
            else
            {
                employee = new Employee { CompanyId = companyId, CreatedBy = actor };
            }

            employee.LastName = form.LastName.Trim();
            employee.FirstName = form.FirstName.Trim();
            employee.CnssNumber = string.IsNullOrWhiteSpace(form.CnssNumber) ? null : form.CnssNumber.Trim();
            employee.BaseSalary = Round(form.BaseSalary);
            employee.Overtime = Round(form.Overtime);
            employee.Bonuses = Round(form.Bonuses);
            employee.BenefitsInKind = Round(form.BenefitsInKind);
            employee.HireDate = form.HireDate.Date;
            employee.ExitDate = form.ExitDate?.Date;
            employee.IsActive = form.IsActive;
            employee.UpdatedBy = actor;
            employee.UpdatedAt = DateTime.UtcNow;

            if (id == null)
                employee = await storage.InsertEmployeeAsync(employee);
            else
                employee = await storage.UpdateEmployeeAsync(employee);
            return (ToEmployeeDto(employee), null);
        }

        public static PeriodSummaryDto ListPayslips(IStorageBroker storage, string? companyId, int year, int month)
        {
            var slips = storage.SelectAllPayslips()
                .ForCompany(companyId)
                .AsEnumerable()
                .Where(p => p.Year == year && p.Month == month)
                .OrderBy(p => p.Employee?.LastName).ThenBy(p => p.Employee?.FirstName)
                .Select(ToPayslipDto)
                .ToList();
            return new PeriodSummaryDto
            {
                Year = year,
                Month = month,
                PayslipCount = slips.Count,
                TotalGross = slips.Sum(s => s.Gross),
                TotalNet = slips.Sum(s => s.Net),
                TotalCnss = slips.Sum(s => s.CnssEmployee + s.CnssEmployer),
                TotalIgr = slips.Sum(s => s.Igr),
                AllPosted = slips.Count > 0 && slips.All(s => s.IsPosted),
                Payslips = slips
            };
        }

        public static async Task<(PayslipDto? Dto, string? Error)> CalculateAsync(
            IStorageBroker storage, string? companyId, int employeeId, int year, int month, string? actor)
        {
            if (month is < 1 or > 12) return (null, "Mois invalide.");
            var employee = storage.SelectAllEmployees().ForCompany(companyId).FirstOrDefault(e => e.Id == employeeId);
            if (employee == null) return (null, "Salarié introuvable.");

            var monthStart = new DateTime(year, month, 1);
            var monthEnd = monthStart.AddMonths(1).AddDays(-1);
            if (employee.HireDate.Date > monthEnd)
                return (null, "Le salarié n'était pas encore embauché sur cette période.");
            if (employee.ExitDate != null && employee.ExitDate.Value.Date < monthStart)
                return (null, "Le salarié est sorti avant cette période.");

            var existing = storage.SelectAllPayslips()
                .ForCompany(companyId)
                .FirstOrDefault(p => p.EmployeeId == employeeId && p.Year == year && p.Month == month);
            if (existing is { IsPosted: true })
                return (null, "Ce bulletin est déjà comptabilisé.");

            var calc = Compute(employee);
            var slip = existing ?? new Payslip
            {
                EmployeeId = employee.Id,
                Year = year,
                Month = month,
                CompanyId = companyId,
                CreatedBy = actor
            };
            Apply(slip, employee, calc, actor);
            if (existing == null)
                slip = await storage.InsertPayslipAsync(slip);
            else
                slip = await storage.UpdatePayslipAsync(slip);
            slip.Employee = employee;
            return (ToPayslipDto(slip), null);
        }

        public static async Task<(int Count, string? Error)> CalculateAllAsync(
            IStorageBroker storage, string? companyId, int year, int month, string? actor)
        {
            var monthStart = new DateTime(year, month, 1);
            var monthEnd = monthStart.AddMonths(1).AddDays(-1);
            var employees = storage.SelectAllEmployees()
                .ForCompany(companyId)
                .AsEnumerable()
                .Where(e => e.IsActive
                    && e.HireDate.Date <= monthEnd
                    && (e.ExitDate == null || e.ExitDate.Value.Date >= monthStart))
                .ToList();
            var count = 0;
            foreach (var employee in employees)
            {
                var (dto, error) = await CalculateAsync(storage, companyId, employee.Id, year, month, actor);
                if (error != null && dto == null) continue;
                count++;
            }
            return (count, null);
        }

        public static async Task<(PostResultDto? Result, string? Error)> PostMonthAsync(
            IStorageBroker storage,
            INumberingSequenceService numbering,
            string? companyId,
            int year,
            int month,
            string? actor)
        {
            var slips = storage.SelectAllPayslips()
                .ForCompany(companyId)
                .AsEnumerable()
                .Where(p => p.Year == year && p.Month == month && !p.IsPosted)
                .ToList();
            if (slips.Count == 0) return (new PostResultDto { PostedCount = 0 }, null);

            var period = storage.SelectAllFiscalPeriods()
                .ForCompany(companyId)
                .FirstOrDefault(p => p.Year == year && p.Month == month);
            if (period is { IsLocked: true })
                return (null, $"La période {month:00}/{year} est verrouillée.");

            var settings = await AccountingEntryResolver.ResolveSettingsAsync(storage, companyId);
            var accounts = PayrollAccounts(settings.PlanType);
            var gross = Round(slips.Sum(s => s.Gross));
            var employer = Round(slips.Sum(s => s.CnssEmployer + s.AmoEmployer));
            var net = Round(slips.Sum(s => s.Net));
            var cnss = Round(slips.Sum(s => s.CnssEmployee + s.CnssEmployer));
            var amo = Round(slips.Sum(s => s.AmoEmployee + s.AmoEmployer));
            var igr = Round(slips.Sum(s => s.Igr));

            var lines = new List<AccountingEntryLine>
            {
                Line(accounts.Salary, "Rémunérations brutes", gross, 0, 1),
                Line(accounts.EmployerCharges, "Charges patronales", employer, 0, 2),
                Line(accounts.Payable, "Salaires nets à payer", 0, net, 3),
                Line(accounts.Social, "CNSS à payer", 0, cnss, 4)
            };
            var n = 5;
            if (amo > 0 && !string.Equals(accounts.Amo, accounts.Social, StringComparison.Ordinal))
                lines.Add(Line(accounts.Amo, "AMO à payer", 0, amo, n++));
            else if (amo > 0)
                lines[^1].Credit = Round(lines[^1].Credit + amo);
            if (igr > 0)
                lines.Add(Line(accounts.Tax, "IGR à payer", 0, igr, n));

            var delta = Round(lines.Sum(l => l.Debit) - lines.Sum(l => l.Credit));
            if (delta != 0m)
                lines.Add(Line(accounts.Payable, "Écart d'arrondi paie", delta > 0 ? 0 : -delta, delta > 0 ? delta : 0, n + 1));

            var journal = await AccountingEntryResolver.ResolveJournalAsync(storage, companyId, "SAL")
                ?? await AccountingEntryResolver.ResolveJournalAsync(storage, companyId, "OD");
            var entry = new AccountingEntry
            {
                EntryNumber = await numbering.GetNextNumberAsync("AccountingEntry", companyId),
                EntryDate = new DateTime(year, month, DateTime.DaysInMonth(year, month)),
                JournalType = journal?.Code ?? "SAL",
                JournalId = journal?.Id,
                FiscalPeriodId = period?.Id,
                ReferenceType = "Payroll",
                ReferenceId = year * 100 + month,
                Description = $"Paie — {month:00}/{year}",
                Status = "Posted",
                CompanyId = companyId,
                CreatedBy = SalesDocumentAudit.IsReadableActor(actor) ? actor!.Trim() : null,
                Lines = lines
            };
            var saved = await storage.InsertAccountingEntryAsync(entry);
            foreach (var slip in slips)
            {
                slip.IsPosted = true;
                slip.AccountingEntryId = saved.Id;
                slip.UpdatedAt = DateTime.UtcNow;
                slip.UpdatedBy = actor;
                await storage.UpdatePayslipAsync(slip);
            }
            return (new PostResultDto
            {
                PostedCount = slips.Count,
                AccountingEntryId = saved.Id,
                EntryNumber = saved.EntryNumber
            }, null);
        }

        public static async Task<(ExportFile? File, string? Error)> ExportCnssAsync(
            IStorageBroker storage, string? companyId, int year, int month, string? format = null)
        {
            var slips = storage.SelectAllPayslips()
                .ForCompany(companyId)
                .AsEnumerable()
                .Where(p => p.Year == year && p.Month == month)
                .OrderBy(p => p.Employee?.LastName)
                .ToList();
            if (slips.Count == 0) return (null, "Aucun bulletin pour cette période.");

            var xml = string.Equals(format, "xml", StringComparison.OrdinalIgnoreCase);
            byte[] content;
            string fileName;
            if (xml)
            {
                var companyName = storage.SelectAllCompanies().AsEnumerable()
                    .FirstOrDefault(c => c.Id == companyId)?.Name;
                content = Encoding.UTF8.GetBytes(BuildCnssXml(slips, companyId, companyName, year, month));
                fileName = $"CNSS_{year}{month:00}.xml";
            }
            else
            {
                var sb = new StringBuilder();
                sb.AppendLine($"00{month:00}{year}{slips.Count:D4}{DateTime.UtcNow:yyyyMMdd}");
                var ligne = 1;
                foreach (var b in slips)
                {
                    var emp = b.Employee;
                    sb.AppendLine(string.Join("|",
                        ligne.ToString("D5", CultureInfo.InvariantCulture),
                        emp?.CnssNumber ?? "",
                        Sanitize(emp?.LastName),
                        Sanitize(emp?.FirstName),
                        Amount(b.Gross),
                        Amount(b.CnssEmployee),
                        Amount(b.CnssEmployer),
                        Amount(b.AmoEmployee),
                        Amount(b.AmoEmployer),
                        Amount(b.Igr),
                        Amount(b.Net)));
                    ligne++;
                }
                sb.AppendLine($"99{(ligne - 1):D5}{Amount(slips.Sum(s => s.Gross))}");
                content = Encoding.UTF8.GetBytes(sb.ToString());
                fileName = $"CNSS_{year}{month:00}.txt";
            }

            foreach (var slip in slips)
            {
                slip.IsExportedCnss = true;
                await storage.UpdatePayslipAsync(slip);
            }

            return (new ExportFile { Content = content, FileName = fileName }, null);
        }

        public static string BuildCnssXml(
            IReadOnlyList<Payslip> slips, string? companyId, string? companyName, int year, int month)
        {
            string Amt(decimal value) => value.ToString("F2", CultureInfo.InvariantCulture);
            var xml = new XDocument(
                new XDeclaration("1.0", "UTF-8", null),
                new XElement("DeclarationCNSS",
                    new XAttribute("Version", "1.0"),
                    new XElement("Identifiant",
                        new XElement("SocieteId", companyId ?? ""),
                        new XElement("Nom", companyName ?? "")),
                    new XElement("Periode",
                        new XElement("Mois", month),
                        new XElement("Annee", year)),
                    new XElement("NombreSalaries", slips.Count),
                    new XElement("Salaries",
                        slips.Select((b, i) => new XElement("Salarie",
                            new XElement("Ligne", (i + 1).ToString("D5", CultureInfo.InvariantCulture)),
                            new XElement("NumeroCNSS", b.Employee?.CnssNumber ?? ""),
                            new XElement("Nom", b.Employee?.LastName ?? ""),
                            new XElement("Prenom", b.Employee?.FirstName ?? ""),
                            new XElement("Brut", Amt(b.Gross)),
                            new XElement("CNSSSalariale", Amt(b.CnssEmployee)),
                            new XElement("CNSSPatronale", Amt(b.CnssEmployer)),
                            new XElement("AMOSalariale", Amt(b.AmoEmployee)),
                            new XElement("AMOPatronale", Amt(b.AmoEmployer)),
                            new XElement("IGR", Amt(b.Igr)),
                            new XElement("Net", Amt(b.Net))))),
                    new XElement("Totaux",
                        new XElement("Brut", Amt(slips.Sum(s => s.Gross))),
                        new XElement("CNSSSalariale", Amt(slips.Sum(s => s.CnssEmployee))),
                        new XElement("CNSSPatronale", Amt(slips.Sum(s => s.CnssEmployer))),
                        new XElement("AMOSalariale", Amt(slips.Sum(s => s.AmoEmployee))),
                        new XElement("AMOPatronale", Amt(slips.Sum(s => s.AmoEmployer))),
                        new XElement("IGR", Amt(slips.Sum(s => s.Igr))),
                        new XElement("Net", Amt(slips.Sum(s => s.Net)))),
                    new XElement("DateGeneration", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture))));
            return xml.Declaration + Environment.NewLine + xml.ToString();
        }

        public static decimal ComputeIgr(decimal gross, decimal cnssEmployee, decimal amoEmployee)
        {
            var annualNet = (gross - cnssEmployee - amoEmployee) * 12m;
            var abatement = annualNet * 0.20m;
            var taxable = Math.Max(annualNet - abatement, 0m);
            decimal tax = 0;
            var rest = taxable;
            tax += Slice(ref rest, 30000m, 0m);
            tax += Slice(ref rest, 50000m, 0.10m);
            tax += Slice(ref rest, 60000m, 0.20m);
            tax += Slice(ref rest, 80000m, 0.30m);
            if (rest > 0) tax += rest * 0.34m;
            return Math.Max(Round(tax / 12m), 0m);
        }

        public static (decimal Gross, decimal CnssEmp, decimal CnssEr, decimal AmoEmp, decimal AmoEr, decimal Igr, decimal Net)
            Compute(Employee employee)
        {
            var gross = Round(employee.BaseSalary + employee.Overtime + employee.Bonuses + employee.BenefitsInKind);
            var cnssBase = Math.Min(gross, CnssCeiling);
            var cnssEmp = Round(cnssBase * CnssEmployeeRate);
            var cnssEr = Round(cnssBase * CnssEmployerRate);
            var amoEmp = Round(gross * AmoRate);
            var amoEr = Round(gross * AmoRate);
            var igr = ComputeIgr(gross, cnssEmp, amoEmp);
            var net = Round(gross - cnssEmp - amoEmp - igr);
            return (gross, cnssEmp, cnssEr, amoEmp, amoEr, igr, net);
        }

        private static void Apply(
            Payslip slip,
            Employee employee,
            (decimal Gross, decimal CnssEmp, decimal CnssEr, decimal AmoEmp, decimal AmoEr, decimal Igr, decimal Net) calc,
            string? actor)
        {
            slip.BaseSalary = employee.BaseSalary;
            slip.Overtime = employee.Overtime;
            slip.Bonuses = employee.Bonuses;
            slip.BenefitsInKind = employee.BenefitsInKind;
            slip.UpdatedBy = actor;
            slip.UpdatedAt = DateTime.UtcNow;
            slip.Gross = calc.Gross;
            slip.CnssEmployee = calc.CnssEmp;
            slip.CnssEmployer = calc.CnssEr;
            slip.AmoEmployee = calc.AmoEmp;
            slip.AmoEmployer = calc.AmoEr;
            slip.Igr = calc.Igr;
            slip.Net = calc.Net;
        }

        private static decimal Slice(ref decimal rest, decimal width, decimal rate)
        {
            if (rest <= 0) return 0;
            var take = Math.Min(rest, width);
            rest -= take;
            return take * rate;
        }

        private static AccountingEntryLine Line(string account, string label, decimal debit, decimal credit, int n) => new()
        {
            AccountCode = account,
            AccountLabel = label,
            Debit = debit,
            Credit = credit,
            LineNumber = n
        };

        private static (string Salary, string EmployerCharges, string Payable, string Social, string Amo, string Tax)
            PayrollAccounts(string? planType) =>
            string.Equals(planType, "PcmMaroc", StringComparison.OrdinalIgnoreCase)
                ? ("617100", "617400", "421100", "431100", "432100", "442300")
                : ("641000", "645000", "421000", "431000", "431000", "442000");

        private static EmployeeDto ToEmployeeDto(Employee e) => new()
        {
            Id = e.Id,
            LastName = e.LastName,
            FirstName = e.FirstName,
            CnssNumber = e.CnssNumber,
            BaseSalary = e.BaseSalary,
            Overtime = e.Overtime,
            Bonuses = e.Bonuses,
            BenefitsInKind = e.BenefitsInKind,
            HireDate = e.HireDate,
            ExitDate = e.ExitDate,
            IsActive = e.IsActive
        };

        private static PayslipDto ToPayslipDto(Payslip p) => new()
        {
            Id = p.Id,
            EmployeeId = p.EmployeeId,
            EmployeeName = p.Employee == null ? "" : $"{p.Employee.LastName} {p.Employee.FirstName}".Trim(),
            CnssNumber = p.Employee?.CnssNumber,
            Year = p.Year,
            Month = p.Month,
            BaseSalary = p.BaseSalary,
            Overtime = p.Overtime,
            Bonuses = p.Bonuses,
            BenefitsInKind = p.BenefitsInKind,
            Gross = p.Gross,
            CnssEmployee = p.CnssEmployee,
            CnssEmployer = p.CnssEmployer,
            AmoEmployee = p.AmoEmployee,
            AmoEmployer = p.AmoEmployer,
            Igr = p.Igr,
            Net = p.Net,
            IsPosted = p.IsPosted,
            AccountingEntryId = p.AccountingEntryId,
            IsExportedCnss = p.IsExportedCnss
        };

        private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
        private static string Amount(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);
        private static string Sanitize(string? value) => (value ?? string.Empty).Replace('|', ' ').Trim();
    }
}
