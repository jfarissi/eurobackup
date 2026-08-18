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
    public class BankReconciliationServiceTests
    {
        private sealed class FakeBankStorage
        {
            public List<BankReconciliation> Recs { get; } = new();
            public List<AccountingEntry> Entries { get; } = new();
            public List<CompanyAccountingSettings> Settings { get; } = new();
            public List<FiscalPeriod> Periods { get; } = new();
            public Mock<IStorageBroker> Broker { get; }

            public FakeBankStorage()
            {
                this.Broker = new Mock<IStorageBroker>();
                this.Broker.Setup(s => s.SelectAllBankReconciliations()).Returns(() => this.Recs.AsQueryable());
                this.Broker.Setup(s => s.SelectAllAccountingEntries()).Returns(() => this.Entries.AsQueryable());
                this.Broker.Setup(s => s.SelectAllCompanyAccountingSettings()).Returns(() => this.Settings.AsQueryable());
                this.Broker.Setup(s => s.SelectAllFiscalPeriods()).Returns(() => this.Periods.AsQueryable());
                this.Broker.Setup(s => s.InsertBankReconciliationAsync(It.IsAny<BankReconciliation>()))
                    .ReturnsAsync((BankReconciliation r) =>
                    {
                        r.Id = this.Recs.Count + 1;
                        var lineId = 1;
                        foreach (var line in r.Lines)
                        {
                            line.Id = lineId++;
                            line.BankReconciliationId = r.Id;
                        }
                        this.Recs.Add(r);
                        return r;
                    });
                this.Broker.Setup(s => s.UpdateBankReconciliationAsync(It.IsAny<BankReconciliation>()))
                    .ReturnsAsync((BankReconciliation r) => r);
                this.Broker.Setup(s => s.UpdateFiscalPeriodAsync(It.IsAny<FiscalPeriod>()))
                    .ReturnsAsync((FiscalPeriod p) => p);
            }
        }

        private static AccountingEntry BankEntry(
            int id, DateTime date, decimal debit, decimal credit, string number, string description) => new()
        {
            Id = id,
            EntryNumber = number,
            EntryDate = date,
            Description = description,
            Status = "Posted",
            CompanyId = "c1",
            Lines = new List<AccountingEntryLine>
            {
                new() { Id = id * 10, AccountingEntryId = id, AccountCode = "512000", Debit = debit, Credit = credit, LineNumber = 1 },
                new() { Id = id * 10 + 1, AccountingEntryId = id, AccountCode = "411000", Debit = credit, Credit = debit, LineNumber = 2 }
            }
        };

        [Fact]
        public void Parser_ReadsSemicolonHeader()
        {
            var csv = "DateOp;Libelle;Reference;Debit;Credit;Solde\n10/03/2026;Virement client;EC-0001;;1210;1210\n11/03/2026;Loyer;LOYER;800;;410";
            var lines = BankStatementCsvParser.Parse(csv);
            Assert.Equal(2, lines.Count);
            Assert.Equal(new DateTime(2026, 3, 10), lines[0].OperationDate);
            Assert.Equal(1210m, lines[0].Credit);
            Assert.Equal("EC-0001", lines[0].Reference);
            Assert.Equal(800m, lines[1].Debit);
        }

        [Fact]
        public async Task Import_CreatesOpenReconciliation()
        {
            var storage = new FakeBankStorage();
            storage.Entries.Add(BankEntry(1, new DateTime(2026, 3, 10), 1210m, 0m, "EC-0001", "Règlement F-1"));

            var csv = "Date;Libelle;Reference;Debit;Credit;Solde\n10/03/2026;VIR client;EC-0001;;1210;1210";
            var (dto, error) = await BankReconciliationService.ImportAsync(
                storage.Broker.Object, "c1", csv, "releve.csv", null, "Alice");

            Assert.Null(error);
            Assert.Equal("Open", dto!.Status);
            Assert.Equal("512000", dto.AccountCode);
            Assert.Equal(1210m, dto.StatementBalance);
            Assert.Equal(1210m, dto.BookBalance);
            Assert.Single(dto.Lines);
        }

        [Fact]
        public async Task AutoMatch_Pass1_MatchesExactReference()
        {
            var storage = new FakeBankStorage();
            storage.Entries.Add(BankEntry(1, new DateTime(2026, 3, 10), 1210m, 0m, "EC-0001", "Règlement F-1"));
            var csv = "Date;Libelle;Reference;Debit;Credit\n12/03/2026;VIR;EC-0001;;1210";
            var imported = (await BankReconciliationService.ImportAsync(
                storage.Broker.Object, "c1", csv, "r.csv", null, "A")).Dto!;

            var (result, error) = await BankReconciliationService.AutoMatchAsync(
                storage.Broker.Object, "c1", imported.Id);

            Assert.Null(error);
            Assert.Equal(1, result!.Matched);
            Assert.Equal("Reference", result.Reconciliation!.Lines.Single().MatchMethod);
        }

        [Fact]
        public async Task AutoMatch_Pass2_MatchesAmountAndDateWindow()
        {
            var storage = new FakeBankStorage();
            storage.Entries.Add(BankEntry(1, new DateTime(2026, 3, 10), 500m, 0m, "EC-0099", "Encaissement"));
            var csv = "Date;Libelle;Reference;Debit;Credit\n12/03/2026;VIR client;;;500";
            var imported = (await BankReconciliationService.ImportAsync(
                storage.Broker.Object, "c1", csv, "r.csv", null, "A")).Dto!;

            var (result, error) = await BankReconciliationService.AutoMatchAsync(
                storage.Broker.Object, "c1", imported.Id);

            Assert.Null(error);
            Assert.Equal(1, result!.Matched);
            Assert.Equal("AmountDate", result.Reconciliation!.Lines.Single().MatchMethod);
        }

        [Fact]
        public async Task AutoMatch_Pass3_SkipsWhenTwoCandidates()
        {
            var storage = new FakeBankStorage();
            storage.Entries.Add(BankEntry(1, new DateTime(2026, 3, 10), 100m, 0m, "EC-0001", "A"));
            storage.Entries.Add(BankEntry(2, new DateTime(2026, 3, 20), 100m, 0m, "EC-0002", "B"));
            var csv = "Date;Libelle;Reference;Debit;Credit\n15/03/2026;VIR ambigu;;;100";
            var imported = (await BankReconciliationService.ImportAsync(
                storage.Broker.Object, "c1", csv, "r.csv", null, "A")).Dto!;

            var (result, error) = await BankReconciliationService.AutoMatchAsync(
                storage.Broker.Object, "c1", imported.Id);

            Assert.Null(error);
            Assert.Equal(0, result!.Matched);
            Assert.False(result.Reconciliation!.Lines.Single().IsMatched);
        }

        [Fact]
        public async Task Complete_FlagsOverlappingPeriod()
        {
            var storage = new FakeBankStorage();
            storage.Periods.Add(new FiscalPeriod { Id = 3, Year = 2026, Month = 3, CompanyId = "c1" });
            storage.Entries.Add(BankEntry(1, new DateTime(2026, 3, 10), 50m, 0m, "EC-0001", "OK"));
            var csv = "Date;Libelle;Reference;Debit;Credit\n10/03/2026;VIR;EC-0001;;50";
            var imported = (await BankReconciliationService.ImportAsync(
                storage.Broker.Object, "c1", csv, "r.csv", null, "A")).Dto!;
            await BankReconciliationService.AutoMatchAsync(storage.Broker.Object, "c1", imported.Id);

            var (dto, error) = await BankReconciliationService.CompleteAsync(
                storage.Broker.Object, "c1", imported.Id, "Alice");

            Assert.Null(error);
            Assert.Equal("Balanced", dto!.Status);
            Assert.True(storage.Periods.Single().IsBankReconciled);
        }

        [Fact]
        public async Task Complete_RefusesUnmatchedLines()
        {
            var storage = new FakeBankStorage();
            var csv = "Date;Libelle;Reference;Debit;Credit\n10/03/2026;Inconnu;;;99";
            var imported = (await BankReconciliationService.ImportAsync(
                storage.Broker.Object, "c1", csv, "r.csv", null, "A")).Dto!;

            var (dto, error) = await BankReconciliationService.CompleteAsync(
                storage.Broker.Object, "c1", imported.Id, "Alice");

            Assert.Null(dto);
            Assert.Contains("pointées", error);
        }

        [Fact]
        public void Parser_CihIgnoresDateValeurAndParsesSpaceAmount()
        {
            var csv = "Date opération;Date valeur;Intitulé;Débit;Crédit\n15/03/2026;16/03/2026;VIR CLIENT;;1 234,56";
            var lines = BankStatementImport.Parse(csv, "CIH_mars.csv");
            Assert.Single(lines);
            Assert.Equal(new DateTime(2026, 3, 15), lines[0].OperationDate);
            Assert.Equal(1234.56m, lines[0].Credit);
            Assert.Equal("CIH", BankStatementImport.DetectBank("CIH_mars.csv", csv));
        }

        [Fact]
        public void Parser_BmceSpaceThousands()
        {
            var csv = "Date;Libelle;Debit;Credit\n10/03/2026;PRELEVEMENT BMCE;1 050,00;";
            var lines = BankStatementImport.Parse(csv, "releve_bmce.csv");
            Assert.Equal(1050m, lines[0].Debit);
            Assert.Equal("BMCE", BankStatementImport.DetectBank("releve_bmce.csv", csv));
        }

        [Fact]
        public void Parser_ExtractsChequeIntoReference()
        {
            var csv = "Date;Libelle;Debit;Credit\n10/03/2026;CHQ 458921;250,00;";
            var lines = BankStatementImport.Parse(csv);
            Assert.Equal("458921", lines[0].Reference);
        }

        [Fact]
        public void Parser_ReadsOfxStmttrn()
        {
            var ofx = """
                OFXHEADER:100
                <OFX>
                <BANKTRANLIST>
                <STMTTRN>
                <TRNTYPE>CREDIT
                <DTPOSTED>20260315
                <TRNAMT>1210.50
                <FITID>OFX-1
                <NAME>VIR CLIENT
                <MEMO>FACTURE F-2026-12
                </STMTTRN>
                <STMTTRN>
                <TRNTYPE>DEBIT
                <DTPOSTED>20260316
                <TRNAMT>-80.00
                <FITID>OFX-2
                <NAME>CHQ 778899
                </STMTTRN>
                </BANKTRANLIST>
                </OFX>
                """;
            var lines = BankStatementImport.Parse(ofx, "attijari.ofx");
            Assert.Equal(2, lines.Count);
            Assert.Equal(new DateTime(2026, 3, 15), lines[0].OperationDate);
            Assert.Equal(1210.50m, lines[0].Credit);
            Assert.Equal("F-2026-12", lines[0].Reference);
            Assert.Equal(80m, lines[1].Debit);
            Assert.Equal("778899", lines[1].Reference);
            Assert.Equal("ATTIJARI", BankStatementImport.DetectBank("attijari.ofx", ofx));
        }
    }
}
