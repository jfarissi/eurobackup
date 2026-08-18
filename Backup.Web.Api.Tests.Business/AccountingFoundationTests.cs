using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Controllers;
using Backup.Web.Api.Server.Models.Entities.Accounting;
using Backup.Web.Api.Server.Models.Entities.SaaS;
using Backup.Web.Api.Server.Services.Accounting;
using Backup.Web.Api.Server.Services.Tenancy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Backup.Web.Api.Tests.Business
{
    /// <summary>Phase 1 — socle comptable : seed idempotent, journaux, plans PCM/PCG, exercices/périodes.</summary>
    public class AccountingFoundationTests
    {
        private sealed class FakeAccountingStorage
        {
            public List<Company> Companies = new();
            public List<Journal> Journals = new();
            public List<ChartOfAccount> Accounts = new();
            public List<CompanyAccountingSettings> Settings = new();
            public List<FiscalYear> FiscalYears = new();
            public List<FiscalPeriod> FiscalPeriods = new();
            public List<CompanyVatRateAccount> VatMaps = new();

            public Mock<IStorageBroker> Broker { get; }

            public FakeAccountingStorage()
            {
                this.Broker = new Mock<IStorageBroker>();
                // Queryables évalués paresseusement pour refléter les insertions successives.
                this.Broker.Setup(s => s.SelectAllCompanies()).Returns(() => this.Companies.AsQueryable());
                this.Broker.Setup(s => s.SelectAllJournals()).Returns(() => this.Journals.AsQueryable());
                this.Broker.Setup(s => s.SelectAllChartOfAccounts()).Returns(() => this.Accounts.AsQueryable());
                this.Broker.Setup(s => s.SelectAllCompanyAccountingSettings()).Returns(() => this.Settings.AsQueryable());
                this.Broker.Setup(s => s.SelectAllFiscalYears()).Returns(() => this.FiscalYears.AsQueryable());
                this.Broker.Setup(s => s.SelectAllFiscalPeriods()).Returns(() => this.FiscalPeriods.AsQueryable());
                this.Broker.Setup(s => s.SelectAllCompanyVatRateAccounts()).Returns(() => this.VatMaps.AsQueryable());

                this.Broker.Setup(s => s.InsertJournalAsync(It.IsAny<Journal>()))
                    .ReturnsAsync((Journal j) => { this.Journals.Add(j); return j; });
                this.Broker.Setup(s => s.InsertChartOfAccountAsync(It.IsAny<ChartOfAccount>()))
                    .ReturnsAsync((ChartOfAccount a) => { this.Accounts.Add(a); return a; });
                this.Broker.Setup(s => s.InsertCompanyAccountingSettingsAsync(It.IsAny<CompanyAccountingSettings>()))
                    .ReturnsAsync((CompanyAccountingSettings s) => { this.Settings.Add(s); return s; });
                this.Broker.Setup(s => s.InsertCompanyVatRateAccountAsync(It.IsAny<CompanyVatRateAccount>()))
                    .ReturnsAsync((CompanyVatRateAccount v) => { this.VatMaps.Add(v); return v; });
                this.Broker.Setup(s => s.InsertFiscalYearAsync(It.IsAny<FiscalYear>()))
                    .ReturnsAsync((FiscalYear f) => { this.FiscalYears.Add(f); this.FiscalPeriods.AddRange(f.Periods); return f; });
                this.Broker.Setup(s => s.UpdateFiscalPeriodAsync(It.IsAny<FiscalPeriod>()))
                    .ReturnsAsync((FiscalPeriod p) => p);
                this.Broker.Setup(s => s.SelectFiscalPeriodByIdAsync(It.IsAny<int>()))
                    .ReturnsAsync((int id) => this.FiscalPeriods.FirstOrDefault(p => p.Id == id));
            }
        }

        private static AccountingSeedService NewSeedService(FakeAccountingStorage storage) =>
            new(storage.Broker.Object, NullLogger<AccountingSeedService>.Instance);

        private static Company NewCompany(string id, bool withFiscalBounds = true) => new()
        {
            Id = id,
            TenantId = "t1",
            Name = $"Société {id}",
            OpenFiscalPeriodStart = withFiscalBounds ? new DateTime(2026, 1, 1) : null,
            OpenFiscalPeriodEnd = withFiscalBounds ? new DateTime(2026, 12, 31) : null
        };

        [Fact]
        public async Task Seed_CreatesSevenJournals_WithCounterpartAccounts()
        {
            var storage = new FakeAccountingStorage();
            storage.Companies.Add(NewCompany("c1"));

            await NewSeedService(storage).EnsureDefaultsAsync();

            Assert.Equal(7, storage.Journals.Count);
            Assert.Equal(
                new[] { "ACH", "AN", "BAN", "CAIS", "OD", "SAL", "VEN" },
                storage.Journals.Select(j => j.Code).OrderBy(c => c).ToArray());
            Assert.Equal("512000", storage.Journals.Single(j => j.Code == "BAN").CounterpartAccountCode);
            Assert.Equal("530000", storage.Journals.Single(j => j.Code == "CAIS").CounterpartAccountCode);
            Assert.All(storage.Journals, j => Assert.Equal("c1", j.CompanyId));
        }

        [Fact]
        public async Task Seed_AddsSalJournalWhenOthersAlreadyExist()
        {
            var storage = new FakeAccountingStorage();
            storage.Companies.Add(NewCompany("c1"));
            storage.Journals.Add(new Journal { Code = "OD", Label = "OD", CompanyId = "c1" });

            await NewSeedService(storage).EnsureDefaultsAsync();

            Assert.Contains(storage.Journals, j => j.Code == "SAL" && j.CompanyId == "c1");
        }

        [Fact]
        public async Task Seed_CreatesDefaultSettings_WithLedgerAccountCodes()
        {
            var storage = new FakeAccountingStorage();
            storage.Companies.Add(NewCompany("c1"));

            await NewSeedService(storage).EnsureDefaultsAsync();

            var settings = Assert.Single(storage.Settings);
            Assert.Equal("c1", settings.CompanyId);
            Assert.Equal(AccountingSeedService.PlanTypePcgEurope, settings.PlanType);
            // Continuité avec les comptes en dur d'AccountingLedger.
            Assert.Equal("411000", settings.CustomerAccountCode);
            Assert.Equal("401000", settings.SupplierAccountCode);
            Assert.Equal("701000", settings.SalesAccountCode);
            Assert.Equal("607000", settings.PurchaseAccountCode);
            Assert.Equal("445710", settings.VatCollectedAccountCode);
            Assert.Equal("445660", settings.VatDeductibleAccountCode);
            Assert.Equal("512000", settings.BankAccountCode);
            Assert.Equal("530000", settings.CashAccountCode);
            Assert.Equal("419000", settings.CustomerDepositAccountCode);

            var vatMap = Assert.Single(storage.VatMaps);
            Assert.Equal(21m, vatMap.Rate);
            Assert.Equal("445710", vatMap.CollectedAccountCode);
            Assert.Equal("445660", vatMap.DeductibleAccountCode);
        }

        [Fact]
        public async Task Seed_IsIdempotent_SecondRunCreatesNoDuplicates()
        {
            var storage = new FakeAccountingStorage();
            storage.Companies.Add(NewCompany("c1"));
            var service = NewSeedService(storage);

            await service.EnsureDefaultsAsync();
            var journalsAfterFirst = storage.Journals.Count;
            var accountsAfterFirst = storage.Accounts.Count;
            var settingsAfterFirst = storage.Settings.Count;
            var yearsAfterFirst = storage.FiscalYears.Count;
            var periodsAfterFirst = storage.FiscalPeriods.Count;

            await service.EnsureDefaultsAsync();

            Assert.Equal(journalsAfterFirst, storage.Journals.Count);
            Assert.Equal(accountsAfterFirst, storage.Accounts.Count);
            Assert.Equal(settingsAfterFirst, storage.Settings.Count);
            Assert.Equal(yearsAfterFirst, storage.FiscalYears.Count);
            Assert.Equal(periodsAfterFirst, storage.FiscalPeriods.Count);
            // Unicité (CompanyId, AccountNumber) préservée par l'idempotence.
            Assert.Equal(
                storage.Accounts.Count,
                storage.Accounts.Select(a => (a.CompanyId, a.AccountNumber)).Distinct().Count());
        }

        [Fact]
        public async Task Seed_MigratesLegacyFiscalBounds_ToFiscalYearWith12Periods()
        {
            var storage = new FakeAccountingStorage();
            storage.Companies.Add(NewCompany("c1"));

            await NewSeedService(storage).EnsureDefaultsAsync();

            var year = Assert.Single(storage.FiscalYears);
            Assert.Equal("c1", year.CompanyId);
            Assert.Equal("Open", year.Status);
            Assert.Equal(new DateTime(2026, 1, 1), year.StartDate);
            Assert.Equal(new DateTime(2026, 12, 31), year.EndDate);
            Assert.Equal(12, year.Periods.Count);
            Assert.Equal(Enumerable.Range(1, 12), year.Periods.OrderBy(p => p.Month).Select(p => p.Month));
            Assert.All(year.Periods, p => Assert.False(p.IsLocked));
        }

        [Fact]
        public async Task Seed_WithoutLegacyBounds_CreatesNoFiscalYear()
        {
            var storage = new FakeAccountingStorage();
            storage.Companies.Add(NewCompany("c1", withFiscalBounds: false));

            await NewSeedService(storage).EnsureDefaultsAsync();

            Assert.Empty(storage.FiscalYears);
            Assert.NotEmpty(storage.Journals);
            Assert.NotEmpty(storage.Accounts);
        }

        [Fact]
        public async Task Seed_PcgPlan_ContainsLedgerAccounts_AndRespectsCompanyIsolation()
        {
            var storage = new FakeAccountingStorage();
            storage.Companies.Add(NewCompany("c1"));
            storage.Companies.Add(NewCompany("c2"));

            await NewSeedService(storage).EnsureDefaultsAsync();

            var ledgerCodes = new[]
                { "411000", "401000", "701000", "607000", "445710", "445660", "419000", "512000", "530000" };
            var c1Codes = storage.Accounts.Where(a => a.CompanyId == "c1").Select(a => a.AccountNumber).ToHashSet();
            foreach (var code in ledgerCodes)
                Assert.Contains(code, c1Codes);

            // Deux sociétés = deux plans isolés (même numéro, CompanyId différent).
            Assert.Equal(
                storage.Accounts.Count(a => a.CompanyId == "c1"),
                storage.Accounts.Count(a => a.CompanyId == "c2"));
        }

        [Fact]
        public async Task Seed_PcmMarocPlan_WhenPlanTypeIsPcmMaroc()
        {
            var storage = new FakeAccountingStorage();
            storage.Companies.Add(NewCompany("c1"));
            storage.Settings.Add(new CompanyAccountingSettings
            {
                CompanyId = "c1",
                PlanType = AccountingSeedService.PlanTypePcmMaroc
            });

            await NewSeedService(storage).EnsureDefaultsAsync();

            Assert.Equal(AccountingChartSeedData.PcmMaroc.Count, storage.Accounts.Count);
            Assert.Contains(storage.Accounts, a => a.AccountNumber == "342100" && a.AccountClass == 4 && a.IsLettrable);
            Assert.Contains(storage.Accounts, a => a.AccountNumber == "711100" && a.IsResultat && !a.IsBilan);
        }

        [Fact]
        public void SeedData_Plans_HaveNoDuplicateAccountNumbers()
        {
            Assert.Equal(
                AccountingChartSeedData.PcmMaroc.Count,
                AccountingChartSeedData.PcmMaroc.Select(a => a.AccountNumber).Distinct().Count());
            Assert.Equal(
                AccountingChartSeedData.PcgEurope.Count,
                AccountingChartSeedData.PcgEurope.Select(a => a.AccountNumber).Distinct().Count());
            Assert.All(AccountingChartSeedData.PcmMaroc, a => Assert.InRange(a.AccountClass, 1, 8));
        }

        [Fact]
        public async Task Open_CreatesFiscalYearWith12MonthlyPeriods()
        {
            var storage = new FakeAccountingStorage();
            var companyContext = new Mock<ICompanyContextService>();
            companyContext.Setup(c => c.GetCurrentCompanyId()).Returns("c1");
            var controller = new FiscalYearsController(storage.Broker.Object, companyContext.Object);

            var result = await controller.Open(new FiscalYearsController.OpenFiscalYearRequest
            {
                StartDate = new DateTime(2026, 1, 1),
                EndDate = new DateTime(2026, 12, 31)
            });

            var year = Assert.Single(storage.FiscalYears);
            Assert.Equal(12, year.Periods.Count);
            Assert.Equal(Enumerable.Range(1, 12), year.Periods.OrderBy(p => p.Month).Select(p => p.Month));
            Assert.Equal("Open", year.Status);
            Assert.Equal("c1", year.CompanyId);
            Assert.IsNotType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task Open_RefusesOverlapWithOpenFiscalYear()
        {
            var storage = new FakeAccountingStorage();
            storage.FiscalYears.Add(new FiscalYear
            {
                Id = 1,
                Name = "Exercice 2026",
                StartDate = new DateTime(2026, 1, 1),
                EndDate = new DateTime(2026, 12, 31),
                Status = "Open",
                CompanyId = "c1"
            });
            var companyContext = new Mock<ICompanyContextService>();
            companyContext.Setup(c => c.GetCurrentCompanyId()).Returns("c1");
            var controller = new FiscalYearsController(storage.Broker.Object, companyContext.Object);

            var result = await controller.Open(new FiscalYearsController.OpenFiscalYearRequest
            {
                StartDate = new DateTime(2026, 7, 1),
                EndDate = new DateTime(2027, 6, 30)
            });

            Assert.IsType<BadRequestObjectResult>(result);
            Assert.Single(storage.FiscalYears);
        }

        [Fact]
        public async Task LockAndUnlockPeriod_PersistsIsLocked()
        {
            var storage = new FakeAccountingStorage();
            storage.FiscalPeriods.Add(new FiscalPeriod
            {
                Id = 5,
                FiscalYearId = 1,
                Year = 2026,
                Month = 3,
                CompanyId = "c1"
            });
            var companyContext = new Mock<ICompanyContextService>();
            companyContext.Setup(c => c.GetCurrentCompanyId()).Returns("c1");
            var controller = new FiscalYearsController(storage.Broker.Object, companyContext.Object);

            var lockResult = await controller.LockPeriod(5);
            Assert.True(storage.FiscalPeriods.Single(p => p.Id == 5).IsLocked);
            Assert.IsType<OkObjectResult>(lockResult);

            await controller.UnlockPeriod(5);
            Assert.False(storage.FiscalPeriods.Single(p => p.Id == 5).IsLocked);
        }
    }
}
