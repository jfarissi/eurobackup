using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Models.Entities;
using Backup.Web.Api.Server.Models.Entities.Accounting;
using Backup.Web.Api.Server.Services.Accounting;
using Backup.Web.Api.Server.Models.Entities.SaaS;
using Moq;

namespace Backup.Web.Api.Tests.Business
{
    public class PayrollServiceTests
    {
        [Fact]
        public void Cnss_IsCappedAt6000()
        {
            var employee = new Employee { BaseSalary = 20000m };
            var calc = PayrollService.Compute(employee);
            Assert.Equal(Math.Round(6000m * 0.0429m, 2, MidpointRounding.AwayFromZero), calc.CnssEmp);
            Assert.Equal(Math.Round(20000m * 0.0226m, 2, MidpointRounding.AwayFromZero), calc.AmoEmp);
        }

        [Fact]
        public void Igr_IsZeroOnLowSalary()
        {
            var calc = PayrollService.Compute(new Employee { BaseSalary = 3000m });
            Assert.Equal(0m, calc.Igr);
            Assert.Equal(calc.Gross - calc.CnssEmp - calc.AmoEmp, calc.Net);
        }

        [Fact]
        public void Igr_IsPositiveOnHighSalary()
        {
            var calc = PayrollService.Compute(new Employee { BaseSalary = 20000m });
            Assert.True(calc.Igr > 0);
            Assert.Equal(calc.Gross - calc.CnssEmp - calc.AmoEmp - calc.Igr, calc.Net);
        }

        private sealed class FakePayrollStorage
        {
            public List<Employee> Employees { get; } = new();
            public List<Payslip> Payslips { get; } = new();
            public List<AccountingEntry> Entries { get; } = new();
            public List<FiscalPeriod> Periods { get; } = new();
            public List<Journal> Journals { get; } = new();
            public List<CompanyAccountingSettings> Settings { get; } = new();
            public Mock<IStorageBroker> Broker { get; }

            public FakePayrollStorage()
            {
                this.Broker = new Mock<IStorageBroker>();
                this.Broker.Setup(s => s.SelectAllEmployees()).Returns(() => this.Employees.AsQueryable());
                this.Broker.Setup(s => s.SelectAllPayslips()).Returns(() => this.Payslips.AsQueryable());
                this.Broker.Setup(s => s.SelectAllCompanies()).Returns(() => Enumerable.Empty<Company>().AsQueryable());
                this.Broker.Setup(s => s.SelectAllAccountingEntries()).Returns(() => this.Entries.AsQueryable());
                this.Broker.Setup(s => s.SelectAllFiscalPeriods()).Returns(() => this.Periods.AsQueryable());
                this.Broker.Setup(s => s.SelectAllJournals()).Returns(() => this.Journals.AsQueryable());
                this.Broker.Setup(s => s.SelectAllCompanyAccountingSettings()).Returns(() => this.Settings.AsQueryable());
                this.Broker.Setup(s => s.InsertEmployeeAsync(It.IsAny<Employee>()))
                    .ReturnsAsync((Employee e) => { e.Id = this.Employees.Count + 1; this.Employees.Add(e); return e; });
                this.Broker.Setup(s => s.UpdateEmployeeAsync(It.IsAny<Employee>())).ReturnsAsync((Employee e) => e);
                this.Broker.Setup(s => s.InsertPayslipAsync(It.IsAny<Payslip>()))
                    .ReturnsAsync((Payslip p) =>
                    {
                        p.Id = this.Payslips.Count + 1;
                        p.Employee ??= this.Employees.FirstOrDefault(e => e.Id == p.EmployeeId);
                        this.Payslips.Add(p);
                        return p;
                    });
                this.Broker.Setup(s => s.UpdatePayslipAsync(It.IsAny<Payslip>())).ReturnsAsync((Payslip p) => p);
                this.Broker.Setup(s => s.InsertAccountingEntryAsync(It.IsAny<AccountingEntry>()))
                    .ReturnsAsync((AccountingEntry e) =>
                    {
                        e.Id = this.Entries.Count + 1;
                        this.Entries.Add(e);
                        return e;
                    });
            }
        }

        [Fact]
        public async Task Calculate_PersistsBulletinAndRejectsPostedRecalc()
        {
            var storage = new FakePayrollStorage();
            var employee = new Employee
            {
                LastName = "Alaoui",
                FirstName = "Sara",
                BaseSalary = 8000m,
                HireDate = new DateTime(2025, 1, 1),
                CompanyId = "c1",
                IsActive = true
            };
            await PayrollService.UpsertEmployeeAsync(
                storage.Broker.Object, "c1", null,
                new PayrollService.EmployeeForm
                {
                    LastName = employee.LastName,
                    FirstName = employee.FirstName,
                    BaseSalary = employee.BaseSalary,
                    HireDate = employee.HireDate
                }, "A");

            var (slip, error) = await PayrollService.CalculateAsync(
                storage.Broker.Object, "c1", 1, 2026, 3, "A");
            Assert.Null(error);
            Assert.True(slip!.Gross > 0);
            Assert.True(slip.Net < slip.Gross);

            var (updated, updateError) = await PayrollService.CalculateAsync(
                storage.Broker.Object, "c1", 1, 2026, 3, "A");
            Assert.Null(updateError);
            Assert.NotNull(updated);
            Assert.Single(storage.Payslips);

            storage.Payslips[0].IsPosted = true;
            var (again, againError) = await PayrollService.CalculateAsync(
                storage.Broker.Object, "c1", 1, 2026, 3, "A");
            Assert.Null(again);
            Assert.Contains("comptabilisé", againError);
        }

        [Fact]
        public async Task ExportCnss_WritesHeaderAndDetail()
        {
            var storage = new FakePayrollStorage();
            await PayrollService.UpsertEmployeeAsync(
                storage.Broker.Object, "c1", null,
                new PayrollService.EmployeeForm
                {
                    LastName = "Alaoui",
                    FirstName = "Sara",
                    CnssNumber = "123456789",
                    BaseSalary = 5000m,
                    HireDate = new DateTime(2025, 1, 1)
                }, "A");
            await PayrollService.CalculateAsync(storage.Broker.Object, "c1", 1, 2026, 3, "A");

            var (file, error) = await PayrollService.ExportCnssAsync(storage.Broker.Object, "c1", 2026, 3);
            Assert.Null(error);
            var text = Encoding.UTF8.GetString(file!.Content);
            Assert.StartsWith("00032026", text);
            Assert.Contains("123456789", text);
            Assert.Contains("Alaoui", text);
            Assert.Contains("99", text.Split('\n').Last(l => l.Length > 0));
            Assert.True(storage.Payslips.Single().IsExportedCnss);
        }

        [Fact]
        public async Task ExportCnssXml_ContainsEmployeeAndTotals()
        {
            var storage = new FakePayrollStorage();
            await PayrollService.UpsertEmployeeAsync(
                storage.Broker.Object, "c1", null,
                new PayrollService.EmployeeForm
                {
                    LastName = "Alaoui",
                    FirstName = "Sara",
                    CnssNumber = "123456789",
                    BaseSalary = 5000m,
                    HireDate = new DateTime(2025, 1, 1)
                }, "A");
            await PayrollService.CalculateAsync(storage.Broker.Object, "c1", 1, 2026, 3, "A");

            var (file, error) = await PayrollService.ExportCnssAsync(
                storage.Broker.Object, "c1", 2026, 3, "xml");
            Assert.Null(error);
            Assert.EndsWith(".xml", file!.FileName);
            var xml = Encoding.UTF8.GetString(file.Content);
            Assert.Contains("<DeclarationCNSS", xml);
            Assert.Contains("<NumeroCNSS>123456789</NumeroCNSS>", xml);
            Assert.Contains("<Nom>Alaoui</Nom>", xml);
            Assert.Contains("<Totaux>", xml);
        }
    }
}
