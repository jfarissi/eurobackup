using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Models.Entities;
using Backup.Web.Api.Server.Models.Entities.Accounting;
using Backup.Web.Api.Server.Models.Entities.SaaS;
using Backup.Web.Api.Server.Services.Accounting;
using Moq;

namespace Backup.Web.Api.Tests.Business
{
    public class CabinetServiceTests
    {
        private sealed class FakeCabinetStorage
        {
            public List<Company> Companies { get; } = new();
            public List<AccountingFirm> Firms { get; } = new();
            public List<AccountingAnnotation> Notes { get; } = new();
            public List<AccountingEntry> Entries { get; } = new();
            public List<FiscalPeriod> Periods { get; } = new();
            public List<BankReconciliation> Recs { get; } = new();
            public Mock<IStorageBroker> Broker { get; }

            public FakeCabinetStorage()
            {
                this.Broker = new Mock<IStorageBroker>();
                this.Broker.Setup(s => s.SelectAllCompanies()).Returns(() => this.Companies.AsQueryable());
                this.Broker.Setup(s => s.SelectAllAccountingFirms()).Returns(() => this.Firms.AsQueryable());
                this.Broker.Setup(s => s.SelectAllAccountingAnnotations()).Returns(() => this.Notes.AsQueryable());
                this.Broker.Setup(s => s.SelectAllAccountingEntries()).Returns(() => this.Entries.AsQueryable());
                this.Broker.Setup(s => s.SelectAllFiscalPeriods()).Returns(() => this.Periods.AsQueryable());
                this.Broker.Setup(s => s.SelectAllBankReconciliations()).Returns(() => this.Recs.AsQueryable());
                this.Broker.Setup(s => s.InsertAccountingFirmAsync(It.IsAny<AccountingFirm>()))
                    .ReturnsAsync((AccountingFirm f) =>
                    {
                        f.Id = this.Firms.Count + 1;
                        this.Firms.Add(f);
                        return f;
                    });
                this.Broker.Setup(s => s.UpdateAccountingFirmAsync(It.IsAny<AccountingFirm>()))
                    .ReturnsAsync((AccountingFirm f) => f);
                this.Broker.Setup(s => s.InsertAccountingAnnotationAsync(It.IsAny<AccountingAnnotation>()))
                    .ReturnsAsync((AccountingAnnotation a) =>
                    {
                        a.Id = this.Notes.Count + 1;
                        this.Notes.Add(a);
                        return a;
                    });
                this.Broker.Setup(s => s.UpdateAccountingAnnotationAsync(It.IsAny<AccountingAnnotation>()))
                    .ReturnsAsync((AccountingAnnotation a) => a);
                this.Broker.Setup(s => s.UpdateFiscalPeriodAsync(It.IsAny<FiscalPeriod>()))
                    .ReturnsAsync((FiscalPeriod p) => p);
            }
        }

        private static FakeCabinetStorage Seed()
        {
            var storage = new FakeCabinetStorage();
            storage.Companies.Add(new Company { Id = "firm", Name = "Cabinet Atlas", IsActive = true });
            storage.Companies.Add(new Company { Id = "client", Name = "Euro Brico", IsActive = true });
            storage.Periods.Add(new FiscalPeriod
            {
                Id = 1,
                CompanyId = "client",
                Year = 2026,
                Month = 3,
                IsLocked = false
            });
            return storage;
        }

        [Fact]
        public async Task Link_ThenAnnotate_ThenCloseBlocksUnlettered()
        {
            var storage = Seed();
            var (dossier, linkError) = await CabinetService.LinkClientAsync(
                storage.Broker.Object, "firm", "client", "Revue", "Alice");
            Assert.Null(linkError);
            Assert.Equal("Euro Brico", dossier!.Name);

            var (note, noteError) = await CabinetService.AddAnnotationAsync(
                storage.Broker.Object, "firm", "client", "Question", "Justifier le 411", null, "Alice");
            Assert.Null(noteError);
            Assert.False(note!.IsResolved);

            storage.Entries.Add(new AccountingEntry
            {
                Id = 1,
                CompanyId = "client",
                EntryDate = new DateTime(2026, 3, 10),
                EntryNumber = "EC-1",
                Status = "Posted",
                Lines = new List<AccountingEntryLine>
                {
                    new() { AccountCode = "411000", Debit = 1200m, LettrageCode = null }
                }
            });

            var blocked = await CabinetService.ValidateCloseAsync(
                storage.Broker.Object, "firm", "client", 2026, 3, force: false, "Alice");
            Assert.Null(blocked.Message);
            Assert.Contains("lettrées", blocked.Error);

            var forced = await CabinetService.ValidateCloseAsync(
                storage.Broker.Object, "firm", "client", 2026, 3, force: true, "Alice");
            Assert.Null(forced.Error);
            Assert.Contains("clôturée", forced.Message);
            Assert.True(storage.Periods.Single().IsLocked);
        }
    }
}
