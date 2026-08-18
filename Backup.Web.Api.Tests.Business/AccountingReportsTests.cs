using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Models.Entities;
using Backup.Web.Api.Server.Models.Entities.Accounting;
using Backup.Web.Api.Server.Services.Accounting;
using Moq;

namespace Backup.Web.Api.Tests.Business
{
    /// <summary>
    /// Balance et grand livre : ouverture / période / clôture, exclusion Draft/Reversed, isolation société.
    /// </summary>
    public class AccountingReportsTests
    {
        private sealed class FakeReportStorage
        {
            public List<AccountingEntry> Entries { get; } = new();
            public List<ChartOfAccount> Accounts { get; } = new();
            public Mock<IStorageBroker> Broker { get; }

            public FakeReportStorage()
            {
                this.Broker = new Mock<IStorageBroker>();
                this.Broker.Setup(s => s.SelectAllAccountingEntries()).Returns(() => this.Entries.AsQueryable());
                this.Broker.Setup(s => s.SelectAllChartOfAccounts()).Returns(() => this.Accounts.AsQueryable());
            }
        }

        private static AccountingEntry NewEntry(
            int id,
            DateTime date,
            string status,
            string companyId,
            params (string Code, string Label, decimal Debit, decimal Credit)[] lines) => new()
        {
            Id = id,
            EntryNumber = $"EC-{id:D4}",
            EntryDate = date,
            JournalType = "VEN",
            Description = $"Pièce {id}",
            Status = status,
            CompanyId = companyId,
            Lines = lines.Select((l, i) => new AccountingEntryLine
            {
                Id = id * 10 + i + 1,
                AccountingEntryId = id,
                AccountCode = l.Code,
                AccountLabel = l.Label,
                Debit = l.Debit,
                Credit = l.Credit,
                LineNumber = i + 1
            }).ToList()
        };

        [Fact]
        public async Task Balance_SplitsOpeningPeriodAndClosing()
        {
            var storage = new FakeReportStorage();
            storage.Entries.Add(NewEntry(1, new DateTime(2026, 1, 10), "Posted", "c1",
                ("411000", "Clients", 100m, 0m),
                ("701000", "Ventes", 0m, 100m)));
            storage.Entries.Add(NewEntry(2, new DateTime(2026, 2, 15), "Validated", "c1",
                ("411000", "Clients", 50m, 0m),
                ("701000", "Ventes", 0m, 50m)));

            var report = await AccountingReportsService.GetBalanceAsync(
                storage.Broker.Object, "c1",
                new DateTime(2026, 2, 1), new DateTime(2026, 2, 28));

            var clients = report.Rows.Single(r => r.AccountCode == "411000");
            Assert.Equal(100m, clients.OpeningDebit);
            Assert.Equal(0m, clients.OpeningCredit);
            Assert.Equal(50m, clients.PeriodDebit);
            Assert.Equal(0m, clients.PeriodCredit);
            Assert.Equal(150m, clients.ClosingDebit);
            Assert.Equal(0m, clients.ClosingCredit);

            var sales = report.Rows.Single(r => r.AccountCode == "701000");
            Assert.Equal(0m, sales.OpeningDebit);
            Assert.Equal(100m, sales.OpeningCredit);
            Assert.Equal(0m, sales.PeriodDebit);
            Assert.Equal(50m, sales.PeriodCredit);
            Assert.Equal(0m, sales.ClosingDebit);
            Assert.Equal(150m, sales.ClosingCredit);

            Assert.Equal(report.TotalOpeningDebit, report.TotalOpeningCredit);
            Assert.Equal(report.TotalPeriodDebit, report.TotalPeriodCredit);
            Assert.Equal(report.TotalClosingDebit, report.TotalClosingCredit);
        }

        [Fact]
        public async Task Balance_IgnoresDraftAndReversedAndOtherCompany()
        {
            var storage = new FakeReportStorage();
            storage.Entries.Add(NewEntry(1, new DateTime(2026, 3, 1), "Posted", "c1",
                ("512000", "Banque", 10m, 0m),
                ("411000", "Clients", 0m, 10m)));
            storage.Entries.Add(NewEntry(2, new DateTime(2026, 3, 1), "Draft", "c1",
                ("512000", "Banque", 999m, 0m),
                ("411000", "Clients", 0m, 999m)));
            storage.Entries.Add(NewEntry(3, new DateTime(2026, 3, 1), "Reversed", "c1",
                ("512000", "Banque", 888m, 0m),
                ("411000", "Clients", 0m, 888m)));
            storage.Entries.Add(NewEntry(4, new DateTime(2026, 3, 1), "Posted", "other",
                ("512000", "Banque", 777m, 0m),
                ("411000", "Clients", 0m, 777m)));

            var report = await AccountingReportsService.GetBalanceAsync(
                storage.Broker.Object, "c1",
                new DateTime(2026, 3, 1), new DateTime(2026, 3, 31));

            Assert.Equal(2, report.Rows.Count);
            Assert.Equal(10m, report.Rows.Single(r => r.AccountCode == "512000").PeriodDebit);
            Assert.Equal(10m, report.Rows.Single(r => r.AccountCode == "411000").PeriodCredit);
        }

        [Fact]
        public async Task Balance_PrefersChartOfAccountsLabel()
        {
            var storage = new FakeReportStorage();
            storage.Accounts.Add(new ChartOfAccount
            {
                AccountNumber = "411000",
                Label = "Clients - plan",
                CompanyId = "c1"
            });
            storage.Entries.Add(NewEntry(1, new DateTime(2026, 1, 1), "Posted", "c1",
                ("411000", "Libellé ligne", 20m, 0m),
                ("701000", "Ventes", 0m, 20m)));

            var report = await AccountingReportsService.GetBalanceAsync(
                storage.Broker.Object, "c1", null, null);

            Assert.Equal("Clients - plan", report.Rows.Single(r => r.AccountCode == "411000").AccountLabel);
            Assert.Equal("Ventes", report.Rows.Single(r => r.AccountCode == "701000").AccountLabel);
        }

        [Fact]
        public async Task GeneralLedger_ComputesRunningBalanceFromOpening()
        {
            var storage = new FakeReportStorage();
            storage.Entries.Add(NewEntry(1, new DateTime(2026, 1, 5), "Posted", "c1",
                ("411000", "Clients", 100m, 0m),
                ("701000", "Ventes", 0m, 100m)));
            storage.Entries.Add(NewEntry(2, new DateTime(2026, 2, 10), "Posted", "c1",
                ("411000", "Clients", 40m, 0m),
                ("701000", "Ventes", 0m, 40m)));
            storage.Entries.Add(NewEntry(3, new DateTime(2026, 2, 20), "Posted", "c1",
                ("512000", "Banque", 30m, 0m),
                ("411000", "Clients", 0m, 30m)));

            var ledger = await AccountingReportsService.GetGeneralLedgerAsync(
                storage.Broker.Object, "c1", "411000",
                new DateTime(2026, 2, 1), new DateTime(2026, 2, 28));

            Assert.Equal(100m, ledger.OpeningDebit);
            Assert.Equal(0m, ledger.OpeningCredit);
            Assert.Equal(100m, ledger.OpeningBalance);
            Assert.Equal(2, ledger.Movements.Count);
            Assert.Equal(140m, ledger.Movements[0].RunningBalance);
            Assert.Equal(110m, ledger.Movements[1].RunningBalance);
            Assert.Equal(40m, ledger.PeriodDebit);
            Assert.Equal(30m, ledger.PeriodCredit);
            Assert.Equal(110m, ledger.ClosingDebit);
            Assert.Equal(110m, ledger.ClosingBalance);
        }

        [Fact]
        public async Task GeneralLedger_EmptyAccount_ReturnsZeros()
        {
            var storage = new FakeReportStorage();

            var ledger = await AccountingReportsService.GetGeneralLedgerAsync(
                storage.Broker.Object, "c1", "999999",
                new DateTime(2026, 1, 1), new DateTime(2026, 12, 31));

            Assert.Equal("999999", ledger.AccountCode);
            Assert.Empty(ledger.Movements);
            Assert.Equal(0m, ledger.OpeningBalance);
            Assert.Equal(0m, ledger.ClosingBalance);
        }
    }
}
