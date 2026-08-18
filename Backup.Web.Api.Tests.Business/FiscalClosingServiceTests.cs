using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Models.Entities;
using Backup.Web.Api.Server.Models.Entities.Accounting;
using Backup.Web.Api.Server.Services.Accounting;
using Backup.Web.Api.Server.Services.Numbering;
using Moq;

namespace Backup.Web.Api.Tests.Business
{
    public class FiscalClosingServiceTests
    {
        private sealed class FakeClosingStorage
        {
            public List<FiscalYear> Years { get; } = new();
            public List<FiscalPeriod> Periods { get; } = new();
            public List<AccountingEntry> Entries { get; } = new();
            public List<ChartOfAccount> Accounts { get; } = new();
            public List<CompanyAccountingSettings> Settings { get; } = new();
            public List<Journal> Journals { get; } = new();
            public Mock<IStorageBroker> Broker { get; }

            public FakeClosingStorage()
            {
                this.Broker = new Mock<IStorageBroker>();
                this.Broker.Setup(s => s.SelectAllFiscalYears()).Returns(() => this.Years.AsQueryable());
                this.Broker.Setup(s => s.SelectAllFiscalPeriods()).Returns(() => this.Periods.AsQueryable());
                this.Broker.Setup(s => s.SelectAllAccountingEntries()).Returns(() => this.Entries.AsQueryable());
                this.Broker.Setup(s => s.SelectAllChartOfAccounts()).Returns(() => this.Accounts.AsQueryable());
                this.Broker.Setup(s => s.SelectAllCompanyAccountingSettings()).Returns(() => this.Settings.AsQueryable());
                this.Broker.Setup(s => s.SelectAllJournals()).Returns(() => this.Journals.AsQueryable());
                this.Broker.Setup(s => s.InsertAccountingEntryAsync(It.IsAny<AccountingEntry>()))
                    .ReturnsAsync((AccountingEntry e) =>
                    {
                        e.Id = this.Entries.Count + 1;
                        this.Entries.Add(e);
                        return e;
                    });
                this.Broker.Setup(s => s.InsertFiscalYearAsync(It.IsAny<FiscalYear>()))
                    .ReturnsAsync((FiscalYear y) =>
                    {
                        y.Id = this.Years.Count + 10;
                        foreach (var p in y.Periods)
                        {
                            p.Id = this.Periods.Count + 100;
                            p.FiscalYearId = y.Id;
                            this.Periods.Add(p);
                        }
                        this.Years.Add(y);
                        return y;
                    });
                this.Broker.Setup(s => s.UpdateFiscalYearAsync(It.IsAny<FiscalYear>()))
                    .ReturnsAsync((FiscalYear y) => y);
                this.Broker.Setup(s => s.UpdateFiscalPeriodAsync(It.IsAny<FiscalPeriod>()))
                    .ReturnsAsync((FiscalPeriod p) => p);
            }
        }

        private static Mock<INumberingSequenceService> Numbering()
        {
            var n = 1;
            var mock = new Mock<INumberingSequenceService>();
            mock.Setup(x => x.GetNextNumberAsync(It.IsAny<string>(), It.IsAny<string?>()))
                .ReturnsAsync(() => $"EC-C{n++:D3}");
            return mock;
        }

        private static FiscalYear SeedYear(FakeClosingStorage storage, string companyId = "c1")
        {
            var year = new FiscalYear
            {
                Id = 1,
                Name = "Exercice 2026",
                StartDate = new DateTime(2026, 1, 1),
                EndDate = new DateTime(2026, 12, 31),
                Status = "Open",
                CompanyId = companyId,
                Periods = FiscalYearCalendar.BuildMonthlyPeriods(
                    new DateTime(2026, 1, 1), new DateTime(2026, 12, 31), companyId)
            };
            var id = 1;
            foreach (var p in year.Periods)
            {
                p.Id = id++;
                p.FiscalYearId = year.Id;
            }
            storage.Years.Add(year);
            storage.Periods.AddRange(year.Periods);
            storage.Accounts.Add(new ChartOfAccount
            {
                AccountNumber = "120000", Label = "Résultat (bénéfice)", CompanyId = companyId, IsBilan = true
            });
            storage.Accounts.Add(new ChartOfAccount
            {
                AccountNumber = "411000", Label = "Clients", CompanyId = companyId, IsLettrable = true
            });
            return year;
        }

        private static AccountingEntry Sale(string companyId = "c1") => new()
        {
            Id = 1,
            EntryNumber = "EC-0001",
            EntryDate = new DateTime(2026, 3, 10),
            Status = "Posted",
            CompanyId = companyId,
            Lines = new List<AccountingEntryLine>
            {
                new() { AccountCode = "411000", AccountLabel = "Clients", Debit = 1210m, Credit = 0m, LineNumber = 1 },
                new() { AccountCode = "701000", AccountLabel = "Ventes", Debit = 0m, Credit = 1000m, LineNumber = 2 },
                new() { AccountCode = "445710", AccountLabel = "TVA", Debit = 0m, Credit = 210m, LineNumber = 3 }
            }
        };

        [Fact]
        public void Preview_Drafts_AreBlocking()
        {
            var storage = new FakeClosingStorage();
            SeedYear(storage);
            storage.Entries.Add(Sale());
            storage.Entries.Add(new AccountingEntry
            {
                Id = 2,
                EntryDate = new DateTime(2026, 4, 1),
                Status = "Draft",
                CompanyId = "c1",
                Lines = new List<AccountingEntryLine>
                {
                    new() { AccountCode = "512000", Debit = 10m, LineNumber = 1 },
                    new() { AccountCode = "411000", Credit = 10m, LineNumber = 2 }
                }
            });

            var preview = FiscalClosingService.PreviewYear(storage.Broker.Object, "c1", 1);

            Assert.False(preview.CanClose);
            Assert.Contains(preview.Checks, c => c.Code == "E005" && c.Severity == "Blocking");
            Assert.Equal(1000m, preview.Profit);
            Assert.Equal("120000", preview.ResultAccountCode);
        }

        [Fact]
        public async Task ClosePeriod_LocksWhenNoDrafts()
        {
            var storage = new FakeClosingStorage();
            var year = SeedYear(storage);
            var march = year.Periods.Single(p => p.Month == 3);

            var (period, error) = await FiscalClosingService.ClosePeriodAsync(
                storage.Broker.Object, "c1", march.Id, "Alice");

            Assert.Null(error);
            Assert.True(period!.IsLocked);
        }

        [Fact]
        public async Task ClosePeriod_RefusesDrafts()
        {
            var storage = new FakeClosingStorage();
            var year = SeedYear(storage);
            storage.Entries.Add(new AccountingEntry
            {
                EntryDate = new DateTime(2026, 3, 5),
                Status = "Draft",
                CompanyId = "c1",
                Lines = new List<AccountingEntryLine>()
            });
            var march = year.Periods.Single(p => p.Month == 3);

            var (period, error) = await FiscalClosingService.ClosePeriodAsync(
                storage.Broker.Object, "c1", march.Id, "Alice");

            Assert.Null(period);
            Assert.Contains("brouillon", error);
            Assert.False(march.IsLocked);
        }

        [Fact]
        public async Task CloseYear_PostsOdAndCarryForward_ThenCloses()
        {
            var storage = new FakeClosingStorage();
            var year = SeedYear(storage);
            storage.Entries.Add(Sale());

            var result = await FiscalClosingService.CloseYearAsync(
                storage.Broker.Object, Numbering().Object, "c1", 1, "Alice");

            Assert.True(result.Success);
            Assert.Equal("Closed", year.Status);
            Assert.All(year.Periods, p => Assert.True(p.IsLocked));
            Assert.NotNull(result.CloseEntryNumber);
            Assert.NotNull(result.CarryForwardEntryNumber);
            Assert.NotNull(result.NextYearId);

            var od = storage.Entries.Single(e => e.ReferenceType == FiscalClosingService.RefYearClose);
            Assert.Equal(new DateTime(2026, 12, 31), od.EntryDate);
            Assert.Equal(1000m, od.Lines.Single(l => l.AccountCode == "701000").Debit);
            Assert.Equal(1000m, od.Lines.Single(l => l.AccountCode == "120000").Credit);
            Assert.Equal(od.Lines.Sum(l => l.Debit), od.Lines.Sum(l => l.Credit));

            var an = storage.Entries.Single(e => e.ReferenceType == FiscalClosingService.RefCarryForward);
            Assert.Equal(new DateTime(2027, 1, 1), an.EntryDate);
            Assert.Equal(1210m, an.Lines.Single(l => l.AccountCode == "411000").Debit);
            Assert.Equal(210m, an.Lines.Single(l => l.AccountCode == "445710").Credit);
            Assert.Equal(1000m, an.Lines.Single(l => l.AccountCode == "120000").Credit);
            Assert.DoesNotContain(an.Lines, l => l.AccountCode == "701000");
            Assert.Equal(an.Lines.Sum(l => l.Debit), an.Lines.Sum(l => l.Credit));

            var next = storage.Years.Single(y => y.Id == result.NextYearId);
            Assert.Equal(new DateTime(2027, 1, 1), next.StartDate);
            Assert.Equal("Open", next.Status);
        }

        [Fact]
        public async Task CloseYear_AlreadyClosed_Refused()
        {
            var storage = new FakeClosingStorage();
            var year = SeedYear(storage);
            year.Status = "Closed";
            storage.Entries.Add(Sale());

            var result = await FiscalClosingService.CloseYearAsync(
                storage.Broker.Object, Numbering().Object, "c1", 1, "Alice");

            Assert.False(result.Success);
            Assert.Contains("déjà clôturé", result.Error);
        }

        [Fact]
        public async Task CloseYear_UnletteredIsWarningNotBlocking()
        {
            var storage = new FakeClosingStorage();
            SeedYear(storage);
            storage.Entries.Add(Sale());

            var preview = FiscalClosingService.PreviewYear(storage.Broker.Object, "c1", 1);

            Assert.True(preview.CanClose);
            Assert.Contains(preview.Checks, c => c.Code == "E004" && c.Severity == "Warning");
        }
    }
}
