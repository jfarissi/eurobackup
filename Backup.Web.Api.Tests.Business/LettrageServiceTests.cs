using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Models.Entities;
using Backup.Web.Api.Server.Models.Entities.Accounting;
using Backup.Web.Api.Server.Models.Entities.SaaS;
using Backup.Web.Api.Server.Services.Accounting;
using Backup.Web.Api.Server.Services.Numbering;
using Moq;

namespace Backup.Web.Api.Tests.Business
{
    /// <summary>
    /// Phase 3 — lettrage comptable par lignes d'écritures : automatique (référence exacte puis
    /// montant FIFO), manuel, délettrage et consultation des lignes non lettrées / groupes.
    /// </summary>
    public class LettrageServiceTests
    {
        private sealed class FakeLettrageStorage
        {
            public List<AccountingEntry> Entries = new();
            public List<Payment> Payments = new();
            public List<SupplierPayment> SupplierPayments = new();
            public List<CompanyAccountingSettings> Settings = new();
            public List<FiscalYear> FiscalYears = new();
            public List<FiscalPeriod> FiscalPeriods = new();
            public List<Company> Companies = new();

            public Mock<IStorageBroker> Broker { get; }

            public FakeLettrageStorage()
            {
                this.Broker = new Mock<IStorageBroker>();
                // Queryables évalués paresseusement pour refléter les mises à jour successives.
                this.Broker.Setup(s => s.SelectAllAccountingEntries()).Returns(() => this.Entries.AsQueryable());
                this.Broker.Setup(s => s.SelectAllPayments()).Returns(() => this.Payments.AsQueryable());
                this.Broker.Setup(s => s.SelectAllSupplierPayments()).Returns(() => this.SupplierPayments.AsQueryable());
                this.Broker.Setup(s => s.SelectAllCompanyAccountingSettings()).Returns(() => this.Settings.AsQueryable());
                this.Broker.Setup(s => s.SelectAllFiscalYears()).Returns(() => this.FiscalYears.AsQueryable());
                this.Broker.Setup(s => s.SelectAllFiscalPeriods()).Returns(() => this.FiscalPeriods.AsQueryable());

                this.Broker.Setup(s => s.SelectCompanyByIdAsync(It.IsAny<string>()))
                    .ReturnsAsync((string id) => this.Companies.FirstOrDefault(c => c.Id == id));
                this.Broker.Setup(s => s.UpdateAccountingEntryLineAsync(It.IsAny<AccountingEntryLine>()))
                    .ReturnsAsync((AccountingEntryLine l) => l);
            }
        }

        /// <summary>Séquence LET- incrémentale (un code par groupe de lettrage).</summary>
        private static Mock<INumberingSequenceService> NewLetteringNumbering()
        {
            var numbering = new Mock<INumberingSequenceService>();
            var next = 0;
            numbering.Setup(n => n.GetNextNumberAsync("Lettering", It.IsAny<string?>()))
                .ReturnsAsync(() => $"LET-2026-{++next:D4}");
            return numbering;
        }

        /// <summary>
        /// Écriture de test à deux lignes équilibrées : une ligne sur le compte lettrable,
        /// la contrepartie sur 512000 (ne doit jamais être lettrée par le service).
        /// Ligne compte : Id = id*10+1 ; contrepartie : Id = id*10+2.
        /// </summary>
        private static AccountingEntry NewEntry(
            int id,
            string referenceType,
            int referenceId,
            string accountCode,
            decimal debit,
            decimal credit,
            int daysAgo,
            string status = "Posted") => new()
        {
            Id = id,
            EntryNumber = $"EC-2026-{id:D4}",
            EntryDate = DateTime.UtcNow.Date.AddDays(-daysAgo),
            JournalType = "Manual",
            ReferenceType = referenceType,
            ReferenceId = referenceId,
            Description = $"Écriture {id}",
            Status = status,
            CompanyId = "c1",
            Lines = new List<AccountingEntryLine>
            {
                new() { Id = id * 10 + 1, AccountingEntryId = id, AccountCode = accountCode, AccountLabel = "Compte", Debit = debit, Credit = credit, LineNumber = 1 },
                new() { Id = id * 10 + 2, AccountingEntryId = id, AccountCode = "512000", AccountLabel = "Banque", Debit = credit, Credit = debit, LineNumber = 2 }
            }
        };

        private static AccountingEntryLine AccountLine(FakeLettrageStorage storage, int entryId) =>
            storage.Entries.Single(e => e.Id == entryId).Lines.Single(l => l.AccountCode != "512000");

        /// <summary>Exercice ouvert couvrant la date du jour, avec la période mensuelle courante verrouillée.</summary>
        private static void AddLockedFiscalYearCoveringToday(FakeLettrageStorage storage, string companyId)
        {
            var today = DateTime.UtcNow.Date;
            var year = new FiscalYear
            {
                Id = 1,
                Name = "Exercice courant",
                StartDate = today.AddMonths(-1),
                EndDate = today.AddMonths(1),
                Status = "Open",
                CompanyId = companyId
            };
            var period = new FiscalPeriod
            {
                Id = 10,
                FiscalYearId = year.Id,
                Year = today.Year,
                Month = today.Month,
                IsLocked = true,
                CompanyId = companyId
            };
            year.Periods.Add(period);
            storage.FiscalYears.Add(year);
            storage.FiscalPeriods.Add(period);
        }

        // --- Consultation ---

        [Fact]
        public void GetUnletteredLines_ExcludesDraftAndReversed_SortsByDate()
        {
            var storage = new FakeLettrageStorage();
            storage.Entries.Add(NewEntry(1, "Manual", 1, "411000", 120m, 0m, daysAgo: 5));
            storage.Entries.Add(NewEntry(2, "Manual", 2, "411000", 80m, 0m, daysAgo: 4, status: "Draft"));
            storage.Entries.Add(NewEntry(3, "Manual", 3, "411000", 60m, 0m, daysAgo: 3, status: "Reversed"));
            storage.Entries.Add(NewEntry(4, "Manual", 4, "411000", 0m, 120m, daysAgo: 10, status: "Validated"));

            var lines = LettrageService.GetUnletteredLinesAsync(storage.Broker.Object, "c1", "411000").Result;

            Assert.Equal(2, lines.Count);
            Assert.Equal("EC-2026-0004", lines[0].EntryNumber); // plus ancienne d'abord
            Assert.Equal("EC-2026-0001", lines[1].EntryNumber);
            Assert.All(lines, l => Assert.Equal("411000", l.AccountCode));
        }

        // --- Stratégie 1 : référence exacte ---

        [Fact]
        public async Task Automatic_ExactReference_InvoiceFullyPaid_LettersGroup()
        {
            var storage = new FakeLettrageStorage();
            storage.Entries.Add(NewEntry(1, AccountingLedger.RefSalesInvoice, 100, "411000", 120m, 0m, daysAgo: 10));
            storage.Entries.Add(NewEntry(2, AccountingLedger.RefPayment, 50, "411000", 0m, 120m, daysAgo: 5));
            storage.Payments.Add(new Payment { Id = 50, SalesInvoiceId = 100, Amount = 120m, Status = "Success", CompanyId = "c1" });

            var summaries = await LettrageService.AutomaticAsync(
                storage.Broker.Object, NewLetteringNumbering().Object, "c1", "411000", "Test");

            var summary = Assert.Single(summaries);
            Assert.Equal("411000", summary.AccountCode);
            Assert.Equal(1, summary.GroupsCreated);
            var code = Assert.Single(summary.Codes);
            Assert.StartsWith("LET-", code);
            Assert.Equal(code, AccountLine(storage, 1).LettrageCode);
            Assert.Equal(code, AccountLine(storage, 2).LettrageCode);
            Assert.NotNull(AccountLine(storage, 1).LettrageDate);
            // Les contreparties hors compte lettrable ne sont jamais lettrées.
            Assert.All(storage.Entries.SelectMany(e => e.Lines).Where(l => l.AccountCode == "512000"),
                l => Assert.Null(l.LettrageCode));
        }

        [Fact]
        public async Task Automatic_ExactReference_MultiplePartialPayments_CoveringInvoice_LettersWholeGroup()
        {
            var storage = new FakeLettrageStorage();
            storage.Entries.Add(NewEntry(1, AccountingLedger.RefSalesInvoice, 100, "411000", 120m, 0m, daysAgo: 10));
            storage.Entries.Add(NewEntry(2, AccountingLedger.RefPayment, 50, "411000", 0m, 60m, daysAgo: 6));
            storage.Entries.Add(NewEntry(3, AccountingLedger.RefPayment, 51, "411000", 0m, 60m, daysAgo: 3));
            storage.Payments.Add(new Payment { Id = 50, SalesInvoiceId = 100, Amount = 60m, Status = "Success", CompanyId = "c1" });
            storage.Payments.Add(new Payment { Id = 51, SalesInvoiceId = 100, Amount = 60m, Status = "Success", CompanyId = "c1" });

            var summaries = await LettrageService.AutomaticAsync(
                storage.Broker.Object, NewLetteringNumbering().Object, "c1", "411000", "Test");

            var summary = Assert.Single(summaries);
            Assert.Equal(1, summary.GroupsCreated);
            var code = Assert.Single(summary.Codes);
            Assert.Equal(code, AccountLine(storage, 1).LettrageCode);
            Assert.Equal(code, AccountLine(storage, 2).LettrageCode);
            Assert.Equal(code, AccountLine(storage, 3).LettrageCode);
        }

        [Fact]
        public async Task Automatic_ExactReference_PartialPaymentNotCovering_NoLettering()
        {
            var storage = new FakeLettrageStorage();
            storage.Entries.Add(NewEntry(1, AccountingLedger.RefSalesInvoice, 100, "411000", 120m, 0m, daysAgo: 10));
            storage.Entries.Add(NewEntry(2, AccountingLedger.RefPayment, 50, "411000", 0m, 80m, daysAgo: 5));
            storage.Payments.Add(new Payment { Id = 50, SalesInvoiceId = 100, Amount = 80m, Status = "Success", CompanyId = "c1" });

            var summaries = await LettrageService.AutomaticAsync(
                storage.Broker.Object, NewLetteringNumbering().Object, "c1", "411000", "Test");

            var summary = Assert.Single(summaries);
            Assert.Equal(0, summary.GroupsCreated);
            Assert.All(storage.Entries.SelectMany(e => e.Lines), l => Assert.Null(l.LettrageCode));
        }

        [Fact]
        public async Task Automatic_NullAccountCode_ProcessesCustomerAndSupplierAccounts()
        {
            var storage = new FakeLettrageStorage();
            storage.Settings.Add(new CompanyAccountingSettings
            {
                CompanyId = "c1", CustomerAccountCode = "411000", SupplierAccountCode = "401000"
            });
            storage.Entries.Add(NewEntry(5, AccountingLedger.RefSupplierInvoice, 200, "401000", 0m, 175m, daysAgo: 10));
            storage.Entries.Add(NewEntry(6, AccountingLedger.RefSupplierPayment, 60, "401000", 175m, 0m, daysAgo: 5));
            storage.SupplierPayments.Add(new SupplierPayment { Id = 60, SupplierInvoiceId = 200, Amount = 175m, Status = "Success", CompanyId = "c1" });

            var summaries = await LettrageService.AutomaticAsync(
                storage.Broker.Object, NewLetteringNumbering().Object, "c1", null, "Test");

            Assert.Equal(2, summaries.Count);
            Assert.Equal(0, summaries.Single(s => s.AccountCode == "411000").GroupsCreated);
            var supplierSummary = summaries.Single(s => s.AccountCode == "401000");
            Assert.Equal(1, supplierSummary.GroupsCreated);
            var code = Assert.Single(supplierSummary.Codes);
            Assert.Equal(code, AccountLine(storage, 5).LettrageCode);
            Assert.Equal(code, AccountLine(storage, 6).LettrageCode);
        }

        // --- Stratégie 2 : montant FIFO ---

        [Fact]
        public async Task Automatic_Fifo_SoldedRun_LettersGroup_LeavesRemainder()
        {
            var storage = new FakeLettrageStorage();
            storage.Entries.Add(NewEntry(1, "Manual", 1, "411000", 100m, 0m, daysAgo: 10));
            storage.Entries.Add(NewEntry(2, "Manual", 2, "411000", 0m, 60m, daysAgo: 8));
            storage.Entries.Add(NewEntry(3, "Manual", 3, "411000", 0m, 40m, daysAgo: 6));
            storage.Entries.Add(NewEntry(4, "Manual", 4, "411000", 50m, 0m, daysAgo: 4)); // reliquat non soldé

            var summaries = await LettrageService.AutomaticAsync(
                storage.Broker.Object, NewLetteringNumbering().Object, "c1", "411000", "Test");

            var summary = Assert.Single(summaries);
            Assert.Equal(1, summary.GroupsCreated);
            var code = Assert.Single(summary.Codes);
            Assert.Equal(code, AccountLine(storage, 1).LettrageCode);
            Assert.Equal(code, AccountLine(storage, 2).LettrageCode);
            Assert.Equal(code, AccountLine(storage, 3).LettrageCode);
            Assert.Null(AccountLine(storage, 4).LettrageCode);
        }

        // --- Lettrage manuel ---

        [Fact]
        public async Task Manual_DifferentAccounts_Refused()
        {
            var storage = new FakeLettrageStorage();
            storage.Entries.Add(NewEntry(1, "Manual", 1, "411000", 100m, 0m, daysAgo: 5));
            storage.Entries.Add(NewEntry(2, "Manual", 2, "411000", 0m, 100m, daysAgo: 3));
            var bankLineId = storage.Entries.Single(e => e.Id == 2).Lines.Single(l => l.AccountCode == "512000").Id;

            var (code, error) = await LettrageService.ManualAsync(
                storage.Broker.Object, NewLetteringNumbering().Object, "c1",
                new[] { 11, bankLineId }, "Test");

            Assert.Null(code);
            Assert.NotNull(error);
            Assert.Contains("même compte", error);
        }

        [Fact]
        public async Task Manual_Unbalanced_Refused()
        {
            var storage = new FakeLettrageStorage();
            storage.Entries.Add(NewEntry(1, "Manual", 1, "411000", 100m, 0m, daysAgo: 5));
            storage.Entries.Add(NewEntry(2, "Manual", 2, "411000", 0m, 80m, daysAgo: 3));

            var (code, error) = await LettrageService.ManualAsync(
                storage.Broker.Object, NewLetteringNumbering().Object, "c1", new[] { 11, 21 }, "Test");

            Assert.Null(code);
            Assert.NotNull(error);
            Assert.Contains("équilibré", error);
            Assert.All(storage.Entries.SelectMany(e => e.Lines), l => Assert.Null(l.LettrageCode));
        }

        [Fact]
        public async Task Manual_Ok_AssignsCode_AndGroupIsVisible()
        {
            var storage = new FakeLettrageStorage();
            storage.Entries.Add(NewEntry(1, "Manual", 1, "411000", 100m, 0m, daysAgo: 5));
            storage.Entries.Add(NewEntry(2, "Manual", 2, "411000", 0m, 100m, daysAgo: 3));

            var (code, error) = await LettrageService.ManualAsync(
                storage.Broker.Object, NewLetteringNumbering().Object, "c1", new[] { 11, 21 }, "Test");

            Assert.Null(error);
            Assert.Equal("LET-2026-0001", code);
            Assert.Equal(code, AccountLine(storage, 1).LettrageCode);
            Assert.Equal(code, AccountLine(storage, 2).LettrageCode);
            Assert.NotNull(AccountLine(storage, 1).LettrageDate);

            var groups = await LettrageService.GetLetteringGroupsAsync(storage.Broker.Object, "c1", null);
            var group = Assert.Single(groups);
            Assert.Equal(code, group.Code);
            Assert.Equal("411000", group.AccountCode);
            Assert.Equal(2, group.LineCount);
            Assert.Equal(100m, group.TotalDebit);
        }

        // --- Délettrage ---

        [Fact]
        public async Task Deletter_LockedPeriod_Refused()
        {
            var storage = new FakeLettrageStorage();
            storage.Entries.Add(NewEntry(1, "Manual", 1, "411000", 100m, 0m, daysAgo: 5));
            storage.Entries.Add(NewEntry(2, "Manual", 2, "411000", 0m, 100m, daysAgo: 3));
            foreach (var line in storage.Entries.SelectMany(e => e.Lines).Where(l => l.AccountCode == "411000"))
            {
                line.LettrageCode = "LET-2026-0001";
                line.LettrageDate = DateTime.UtcNow;
            }
            AddLockedFiscalYearCoveringToday(storage, "c1");

            var (count, error) = await LettrageService.DeletterAsync(
                storage.Broker.Object, "c1", "LET-2026-0001", "Test");

            Assert.Equal(0, count);
            Assert.NotNull(error);
            Assert.Contains("verrouillée", error);
            Assert.All(storage.Entries.SelectMany(e => e.Lines).Where(l => l.AccountCode == "411000"),
                l => Assert.Equal("LET-2026-0001", l.LettrageCode));
        }

        [Fact]
        public async Task Deletter_Ok_ClearsCodes()
        {
            var storage = new FakeLettrageStorage();
            storage.Entries.Add(NewEntry(1, "Manual", 1, "411000", 100m, 0m, daysAgo: 5));
            storage.Entries.Add(NewEntry(2, "Manual", 2, "411000", 0m, 100m, daysAgo: 3));
            foreach (var line in storage.Entries.SelectMany(e => e.Lines).Where(l => l.AccountCode == "411000"))
            {
                line.LettrageCode = "LET-2026-0001";
                line.LettrageDate = DateTime.UtcNow;
            }

            var (count, error) = await LettrageService.DeletterAsync(
                storage.Broker.Object, "c1", "LET-2026-0001", "Test");

            Assert.Null(error);
            Assert.Equal(2, count);
            Assert.All(storage.Entries.SelectMany(e => e.Lines), l => Assert.Null(l.LettrageCode));
            Assert.All(storage.Entries.SelectMany(e => e.Lines), l => Assert.Null(l.LettrageDate));
        }
    }
}
