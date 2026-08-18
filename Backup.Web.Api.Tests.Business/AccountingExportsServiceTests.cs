using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Models.Entities;
using Backup.Web.Api.Server.Models.Entities.Accounting;
using Backup.Web.Api.Server.Models.Entities.SaaS;
using Backup.Web.Api.Server.Services.Accounting;
using Moq;

namespace Backup.Web.Api.Tests.Business
{
    public class AccountingExportsServiceTests
    {
        private sealed class FakeExportStorage
        {
            public List<FiscalYear> Years { get; } = new();
            public List<AccountingEntry> Entries { get; } = new();
            public List<Journal> Journals { get; } = new();
            public List<ChartOfAccount> Accounts { get; } = new();
            public Company Company { get; } = new() { Id = "c1", Name = "EuroBrico", DefaultCurrencyCode = "EUR" };
            public Mock<IStorageBroker> Broker { get; }

            public FakeExportStorage()
            {
                this.Broker = new Mock<IStorageBroker>();
                this.Broker.Setup(s => s.SelectAllFiscalYears()).Returns(() => this.Years.AsQueryable());
                this.Broker.Setup(s => s.SelectAllAccountingEntries()).Returns(() => this.Entries.AsQueryable());
                this.Broker.Setup(s => s.SelectAllJournals()).Returns(() => this.Journals.AsQueryable());
                this.Broker.Setup(s => s.SelectAllChartOfAccounts()).Returns(() => this.Accounts.AsQueryable());
                this.Broker.Setup(s => s.SelectCompanyByIdAsync(It.IsAny<string>()))
                    .ReturnsAsync((string id) => this.Company.Id == id ? this.Company : null);
            }
        }

        private static FiscalYear Year() => new()
        {
            Id = 1,
            Name = "Exercice 2026",
            StartDate = new DateTime(2026, 1, 1),
            EndDate = new DateTime(2026, 12, 31),
            Status = "Open",
            CompanyId = "c1"
        };

        private static AccountingEntry Entry(
            int id, DateTime date, string status, string number, string journalType,
            string description, params (string Account, decimal Debit, decimal Credit, string? Lettrage)[] lines) => new()
        {
            Id = id,
            EntryNumber = number,
            EntryDate = date,
            Description = description,
            Status = status,
            JournalType = journalType,
            ReferenceType = "SalesInvoice",
            ReferenceId = id,
            CompanyId = "c1",
            Lines = lines.Select((l, i) => new AccountingEntryLine
            {
                Id = id * 10 + i,
                AccountingEntryId = id,
                AccountCode = l.Account,
                AccountLabel = l.Account == "411000" ? "Clients" : "Ventes",
                Debit = l.Debit,
                Credit = l.Credit,
                LineNumber = i + 1,
                LettrageCode = l.Lettrage,
                LettrageDate = l.Lettrage == null ? null : date
            }).ToList()
        };

        [Fact]
        public async Task Preview_UnknownYear_ReturnsError()
        {
            var storage = new FakeExportStorage();
            storage.Years.Add(Year());

            var (dto, error) = await AccountingExportsService.PreviewAsync(storage.Broker.Object, "c1", 99);

            Assert.Null(dto);
            Assert.Contains("introuvable", error);
        }

        [Fact]
        public async Task Fec_WritesHeaderAndBookedLines()
        {
            var storage = new FakeExportStorage();
            storage.Years.Add(Year());
            storage.Journals.Add(new Journal { Id = 1, Code = "VEN", Label = "Journal des ventes", CompanyId = "c1" });
            storage.Entries.Add(Entry(1, new DateTime(2026, 3, 10), "Posted", "EC-0001", "SalesInvoice", "Vente F-1",
                ("411000", 1210m, 0m, "A01"),
                ("701000", 0m, 1000m, null)));
            storage.Entries.Add(Entry(2, new DateTime(2026, 3, 11), "Draft", "EC-0002", "SalesInvoice", "Brouillon",
                ("411000", 10m, 0m, null)));
            storage.Entries.Add(Entry(3, new DateTime(2025, 12, 31), "Posted", "EC-0003", "SalesInvoice", "Hors exercice",
                ("411000", 50m, 0m, null)));

            var (file, error) = await AccountingExportsService.ExportFecAsync(storage.Broker.Object, "c1", 1);

            Assert.Null(error);
            Assert.Equal(2, file!.LineCount);
            var text = Encoding.UTF8.GetString(file.Content);
            var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal(AccountingExportsService.FecHeader, lines[0]);
            Assert.Equal(3, lines.Length);
            Assert.Contains("VEN|Journal des ventes|EC-0001|20260310|411000|Clients||", lines[1]);
            Assert.Contains("|SalesInvoice-1|20260310|Vente F-1|1210.00|0.00|A01|20260310|", lines[1]);
            Assert.Contains("|701000|Ventes||", lines[2]);
            Assert.DoesNotContain("EC-0002", text);
            Assert.DoesNotContain("EC-0003", text);
            Assert.Equal("FEC_EuroBrico_20260101_20261231.txt", file.FileName);
        }

        [Fact]
        public async Task Fec_SanitizesPipeInDescription()
        {
            var storage = new FakeExportStorage();
            storage.Years.Add(Year());
            storage.Entries.Add(Entry(1, new DateTime(2026, 6, 1), "Validated", "EC-0001", "OD", "Libellé | dangereux",
                ("512000", 0m, 100m, null)));

            var (file, error) = await AccountingExportsService.ExportFecAsync(storage.Broker.Object, "c1", 1);

            Assert.Null(error);
            var text = Encoding.UTF8.GetString(file!.Content);
            Assert.Contains("dangereux", text);
            Assert.DoesNotContain("|dangereux", text);
            Assert.Equal(18, text.Split('\n', StringSplitOptions.RemoveEmptyEntries)[1].Split('|').Length);
        }

        [Fact]
        public async Task Fec_UsesLinkedJournal()
        {
            var storage = new FakeExportStorage();
            storage.Years.Add(Year());
            storage.Journals.Add(new Journal { Id = 7, Code = "BAN", Label = "Banque principale", CompanyId = "c1" });
            var entry = Entry(1, new DateTime(2026, 4, 1), "Posted", "EC-0001", "Payment", "Règlement",
                ("512000", 200m, 0m, null));
            entry.JournalId = 7;
            storage.Entries.Add(entry);

            var (file, error) = await AccountingExportsService.ExportFecAsync(storage.Broker.Object, "c1", 1);

            Assert.Null(error);
            var body = Encoding.UTF8.GetString(file!.Content).Split('\n')[1];
            Assert.StartsWith("BAN|Banque principale|", body);
        }

        [Fact]
        public async Task Csv_UsesSemicolonAndBom()
        {
            var storage = new FakeExportStorage();
            storage.Years.Add(Year());
            storage.Entries.Add(Entry(1, new DateTime(2026, 1, 15), "Posted", "EC-0001", "SalesInvoice", "Vente",
                ("411000", 10m, 0m, null)));

            var (file, error) = await AccountingExportsService.ExportCsvAsync(storage.Broker.Object, "c1", 1);

            Assert.Null(error);
            Assert.StartsWith("text/csv", file!.ContentType);
            Assert.True(file.Content[0] == 0xEF && file.Content[1] == 0xBB && file.Content[2] == 0xBF);
            var text = Encoding.UTF8.GetString(file.Content, 3, file.Content.Length - 3);
            Assert.StartsWith(AccountingExportsService.FecHeader.Replace('|', ';'), text);
            Assert.Contains(";10.00;0.00;", text);
            Assert.DoesNotContain("|", text.Split('\n')[1]);
            Assert.Equal("ECRITURES_EuroBrico_20260101_20261231.csv", file.FileName);
        }
    }
}
