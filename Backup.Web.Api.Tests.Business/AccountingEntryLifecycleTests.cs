using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Controllers;
using Backup.Web.Api.Server.Models.Entities;
using Backup.Web.Api.Server.Models.Entities.Accounting;
using Backup.Web.Api.Server.Models.Entities.SaaS;
using Backup.Web.Api.Server.Models.Users;
using Backup.Web.Api.Server.Services.Accounting;
using Backup.Web.Api.Server.Services.Numbering;
using Backup.Web.Api.Server.Services.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace Backup.Web.Api.Tests.Business
{
    /// <summary>
    /// Phase 3 — cycle de vie des écritures : validation (Posted → Validated), extourne (Reversed),
    /// comptabilisation d'un brouillon (Draft → Posted) et gardes brouillon du contrôleur.
    /// </summary>
    public class AccountingEntryLifecycleTests
    {
        private sealed class FakeLifecycleStorage
        {
            public List<AccountingEntry> Entries = new();
            public List<Journal> Journals = new();
            public List<FiscalYear> FiscalYears = new();
            public List<FiscalPeriod> FiscalPeriods = new();
            public List<Company> Companies = new();
            public List<CompanyAccountingSettings> Settings = new();

            public Mock<IStorageBroker> Broker { get; }

            public FakeLifecycleStorage()
            {
                this.Broker = new Mock<IStorageBroker>();
                // Queryables évalués paresseusement pour refléter les insertions successives.
                this.Broker.Setup(s => s.SelectAllAccountingEntries()).Returns(() => this.Entries.AsQueryable());
                this.Broker.Setup(s => s.SelectAllJournals()).Returns(() => this.Journals.AsQueryable());
                this.Broker.Setup(s => s.SelectAllFiscalYears()).Returns(() => this.FiscalYears.AsQueryable());
                this.Broker.Setup(s => s.SelectAllFiscalPeriods()).Returns(() => this.FiscalPeriods.AsQueryable());
                this.Broker.Setup(s => s.SelectAllCompanyAccountingSettings()).Returns(() => this.Settings.AsQueryable());

                this.Broker.Setup(s => s.SelectAccountingEntryByIdAsync(It.IsAny<int>()))
                    .ReturnsAsync((int id) => this.Entries.FirstOrDefault(e => e.Id == id));
                this.Broker.Setup(s => s.SelectFiscalPeriodByIdAsync(It.IsAny<int>()))
                    .ReturnsAsync((int id) => this.FiscalPeriods.FirstOrDefault(p => p.Id == id));
                this.Broker.Setup(s => s.SelectCompanyByIdAsync(It.IsAny<string>()))
                    .ReturnsAsync((string id) => this.Companies.FirstOrDefault(c => c.Id == id));
                this.Broker.Setup(s => s.InsertAccountingEntryAsync(It.IsAny<AccountingEntry>()))
                    .ReturnsAsync((AccountingEntry e) => { this.Entries.Add(e); return e; });
                this.Broker.Setup(s => s.UpdateAccountingEntryAsync(It.IsAny<AccountingEntry>()))
                    .ReturnsAsync((AccountingEntry e) => e);
                this.Broker.Setup(s => s.DeleteAccountingEntryAsync(It.IsAny<AccountingEntry>()))
                    .Returns((AccountingEntry e) => { this.Entries.Remove(e); return ValueTask.CompletedTask; });
            }
        }

        private static Mock<INumberingSequenceService> NewNumbering(string number = "EC-2026-0002")
        {
            var numbering = new Mock<INumberingSequenceService>();
            numbering.Setup(n => n.GetNextNumberAsync(It.IsAny<string>(), It.IsAny<string?>()))
                .ReturnsAsync(number);
            return numbering;
        }

        /// <summary>Écriture équilibrée de test : débit 411000 / crédit 701000 pour 120.</summary>
        private static AccountingEntry NewEntry(string status, int id = 1, string companyId = "c1") => new()
        {
            Id = id,
            EntryNumber = $"EC-2026-{id:D4}",
            EntryDate = DateTime.UtcNow,
            JournalType = "Manual",
            ReferenceType = "Manual",
            ReferenceId = id,
            Description = "Écriture de test",
            Status = status,
            CompanyId = companyId,
            Lines = new List<AccountingEntryLine>
            {
                new() { Id = id * 10 + 1, AccountingEntryId = id, AccountCode = "411000", AccountLabel = "Clients", Debit = 120m, Credit = 0m, LineNumber = 1 },
                new() { Id = id * 10 + 2, AccountingEntryId = id, AccountCode = "701000", AccountLabel = "Ventes", Debit = 0m, Credit = 120m, LineNumber = 2 }
            }
        };

        // --- Validation (Posted → Validated) ---

        [Fact]
        public async Task Validate_PostedEntry_BecomesValidated()
        {
            var storage = new FakeLifecycleStorage();
            storage.Entries.Add(NewEntry("Posted"));

            var (entry, error) = await AccountingEntryLifecycleService.ValidateAsync(
                storage.Broker.Object, 1, "c1", "Test");

            Assert.Null(error);
            Assert.NotNull(entry);
            Assert.Equal("Validated", entry!.Status);
            Assert.Equal("Validated", storage.Entries.Single().Status);
        }

        [Fact]
        public async Task Validate_AlreadyValidated_Refused()
        {
            var storage = new FakeLifecycleStorage();
            storage.Entries.Add(NewEntry("Validated"));

            var (entry, error) = await AccountingEntryLifecycleService.ValidateAsync(
                storage.Broker.Object, 1, "c1", "Test");

            Assert.Null(entry);
            Assert.NotNull(error);
            Assert.Contains("Posted", error);
            Assert.Equal("Validated", storage.Entries.Single().Status);
        }

        [Fact]
        public async Task Validate_LockedPeriod_Refused()
        {
            var storage = new FakeLifecycleStorage();
            storage.FiscalPeriods.Add(new FiscalPeriod
            {
                Id = 10, FiscalYearId = 1, Year = 2026, Month = 3, IsLocked = true, CompanyId = "c1"
            });
            var entry = NewEntry("Posted");
            entry.FiscalPeriodId = 10;
            storage.Entries.Add(entry);

            var (validated, error) = await AccountingEntryLifecycleService.ValidateAsync(
                storage.Broker.Object, 1, "c1", "Test");

            Assert.Null(validated);
            Assert.NotNull(error);
            Assert.Contains("verrouillée", error);
            Assert.Equal("Posted", storage.Entries.Single().Status);
        }

        // --- Extourne (Posted → Reversed) ---

        [Fact]
        public async Task Reverse_PostedEntry_CreatesBalancedReversal_AndReversesOriginal()
        {
            var storage = new FakeLifecycleStorage();
            storage.Entries.Add(NewEntry("Posted"));

            var (reversal, error) = await AccountingEntryLifecycleService.ReverseAsync(
                storage.Broker.Object, NewNumbering().Object, 1, "c1", "Test");

            Assert.Null(error);
            Assert.NotNull(reversal);
            Assert.Equal("Reversal", reversal!.ReferenceType);
            Assert.Equal("Reversal", reversal.JournalType);
            Assert.Equal(1, reversal.ReferenceId);
            Assert.Equal("Extourne EC-2026-0001", reversal.Description);
            Assert.Equal("Posted", reversal.Status);
            Assert.Equal("EC-2026-0002", reversal.EntryNumber);

            // Lignes inversées débit ↔ crédit, équilibrée par construction.
            Assert.Equal(2, reversal.Lines.Count);
            Assert.Contains(reversal.Lines, l => l.AccountCode == "411000" && l.Debit == 0m && l.Credit == 120m);
            Assert.Contains(reversal.Lines, l => l.AccountCode == "701000" && l.Debit == 120m && l.Credit == 0m);
            Assert.Equal(reversal.Lines.Sum(l => l.Debit), reversal.Lines.Sum(l => l.Credit));

            Assert.Equal("Reversed", storage.Entries.Single(e => e.Id == 1).Status);
        }

        [Fact]
        public async Task Reverse_AlreadyReversed_Refused()
        {
            var storage = new FakeLifecycleStorage();
            storage.Entries.Add(NewEntry("Posted"));
            await AccountingEntryLifecycleService.ReverseAsync(
                storage.Broker.Object, NewNumbering().Object, 1, "c1", "Test");

            var (reversal, error) = await AccountingEntryLifecycleService.ReverseAsync(
                storage.Broker.Object, NewNumbering().Object, 1, "c1", "Test");

            Assert.Null(reversal);
            Assert.NotNull(error);
            Assert.Contains("déjà extournée", error);
            Assert.Equal(2, storage.Entries.Count); // pas de seconde extourne créée
        }

        [Fact]
        public async Task Reverse_ExistingReversalEntry_Refused()
        {
            var storage = new FakeLifecycleStorage();
            storage.Entries.Add(NewEntry("Posted"));
            storage.Entries.Add(new AccountingEntry
            {
                Id = 2,
                EntryNumber = "EC-2026-0002",
                ReferenceType = AccountingEntryLifecycleService.RefReversal,
                ReferenceId = 1,
                Status = "Posted",
                CompanyId = "c1"
            });

            var (reversal, error) = await AccountingEntryLifecycleService.ReverseAsync(
                storage.Broker.Object, NewNumbering().Object, 1, "c1", "Test");

            Assert.Null(reversal);
            Assert.NotNull(error);
            Assert.Contains("extourne existe déjà", error);
            Assert.Equal("Posted", storage.Entries.Single(e => e.Id == 1).Status);
        }

        [Fact]
        public async Task Reverse_ValidatedEntry_Refused()
        {
            var storage = new FakeLifecycleStorage();
            storage.Entries.Add(NewEntry("Validated"));

            var (reversal, error) = await AccountingEntryLifecycleService.ReverseAsync(
                storage.Broker.Object, NewNumbering().Object, 1, "c1", "Test");

            Assert.Null(reversal);
            Assert.NotNull(error);
            Assert.Contains("validée", error);
            Assert.Equal("Validated", storage.Entries.Single().Status);
        }

        // --- Anti-double poste ---

        [Fact]
        public void HasPostedEntry_ValidatedCountsAsPosted_ReversedDoesNot()
        {
            var storage = new FakeLifecycleStorage();
            storage.Entries.Add(NewEntry("Validated")); // ReferenceType "Manual", ReferenceId 1

            Assert.True(AccountingLedger.HasPostedEntry(storage.Broker.Object, "Manual", 1, "c1"));

            storage.Entries.Single().Status = "Reversed";
            Assert.False(AccountingLedger.HasPostedEntry(storage.Broker.Object, "Manual", 1, "c1"));
        }

        // --- Brouillon (Draft → Posted) ---

        [Fact]
        public async Task PostDraft_AssignsEntryNumber_AndBecomesPosted()
        {
            var storage = new FakeLifecycleStorage();
            var draft = NewEntry("Draft");
            draft.EntryNumber = "DRAFT-abcd1234";
            storage.Entries.Add(draft);

            var (posted, error) = await AccountingEntryLifecycleService.PostDraftAsync(
                storage.Broker.Object, NewNumbering("EC-2026-0042").Object, 1, "c1", "Test");

            Assert.Null(error);
            Assert.NotNull(posted);
            Assert.Equal("Posted", posted!.Status);
            Assert.Equal("EC-2026-0042", posted.EntryNumber);
        }

        [Fact]
        public async Task PostDraft_NonDraft_Refused()
        {
            var storage = new FakeLifecycleStorage();
            storage.Entries.Add(NewEntry("Posted"));

            var (posted, error) = await AccountingEntryLifecycleService.PostDraftAsync(
                storage.Broker.Object, NewNumbering().Object, 1, "c1", "Test");

            Assert.Null(posted);
            Assert.NotNull(error);
            Assert.Contains("brouillon", error);
        }

        // --- Gardes brouillon du contrôleur (PUT / DELETE réservés au Draft) ---

        private static AccountingEntriesController NewController(FakeLifecycleStorage storage)
        {
            var companyContext = new Mock<ICompanyContextService>();
            companyContext.Setup(c => c.GetCurrentCompanyId()).Returns("c1");
            var userManager = new Mock<UserManager<User>>(
                Mock.Of<IUserStore<User>>(), null, null, null, null, null, null, null, null);
            var controller = new AccountingEntriesController(
                storage.Broker.Object, companyContext.Object, NewNumbering().Object, userManager.Object);
            controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
            return controller;
        }

        private static AccountingEntriesController.ManualEntryRequest BalancedRequest() => new()
        {
            Description = "Brouillon modifié",
            Lines = new List<AccountingEntriesController.ManualEntryLineRequest>
            {
                new() { AccountCode = "411000", Debit = 100m },
                new() { AccountCode = "701000", Credit = 100m }
            }
        };

        [Fact]
        public async Task Put_NonDraftEntry_Refused()
        {
            var storage = new FakeLifecycleStorage();
            storage.Entries.Add(NewEntry("Posted"));

            var result = await NewController(storage).Put(1, BalancedRequest());

            Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("Écriture de test", storage.Entries.Single().Description); // inchangée
        }

        [Fact]
        public async Task Put_DraftEntry_ReplacesLabelAndLines()
        {
            var storage = new FakeLifecycleStorage();
            storage.Entries.Add(NewEntry("Draft"));

            var result = await NewController(storage).Put(1, BalancedRequest());

            Assert.IsType<OkObjectResult>(result);
            var entry = storage.Entries.Single();
            Assert.Equal("Brouillon modifié", entry.Description);
            Assert.Equal(2, entry.Lines.Count);
            Assert.Equal(100m, entry.Lines.Sum(l => l.Debit));
            Assert.Equal("Draft", entry.Status); // reste brouillon
        }

        [Fact]
        public async Task Delete_NonDraftEntry_Refused()
        {
            var storage = new FakeLifecycleStorage();
            storage.Entries.Add(NewEntry("Posted"));

            var result = await NewController(storage).Delete(1);

            Assert.IsType<BadRequestObjectResult>(result);
            Assert.Single(storage.Entries);
        }

        [Fact]
        public async Task Delete_DraftEntry_Removed()
        {
            var storage = new FakeLifecycleStorage();
            storage.Entries.Add(NewEntry("Draft"));

            var result = await NewController(storage).Delete(1);

            Assert.IsType<NoContentResult>(result);
            Assert.Empty(storage.Entries);
        }
    }
}
