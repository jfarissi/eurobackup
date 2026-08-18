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
    public class VatDeclarationServiceTests
    {
        private sealed class FakeVatStorage
        {
            public List<AccountingEntry> Entries { get; } = new();
            public List<CompanyAccountingSettings> Settings { get; } = new();
            public List<CompanyVatRateAccount> VatMaps { get; } = new();
            public List<FiscalPeriod> Periods { get; } = new();
            public List<VatDeclaration> Declarations { get; } = new();
            public Mock<IStorageBroker> Broker { get; }

            public FakeVatStorage()
            {
                this.Broker = new Mock<IStorageBroker>();
                this.Broker.Setup(s => s.SelectAllAccountingEntries()).Returns(() => this.Entries.AsQueryable());
                this.Broker.Setup(s => s.SelectAllCompanyAccountingSettings()).Returns(() => this.Settings.AsQueryable());
                this.Broker.Setup(s => s.SelectAllCompanyVatRateAccounts()).Returns(() => this.VatMaps.AsQueryable());
                this.Broker.Setup(s => s.SelectAllFiscalPeriods()).Returns(() => this.Periods.AsQueryable());
                this.Broker.Setup(s => s.SelectAllVatDeclarations()).Returns(() => this.Declarations.AsQueryable());
                this.Broker.Setup(s => s.SelectAllCompanies()).Returns(() => Enumerable.Empty<Backup.Web.Api.Server.Models.Entities.SaaS.Company>().AsQueryable());
                this.Broker.Setup(s => s.InsertVatDeclarationAsync(It.IsAny<VatDeclaration>()))
                    .ReturnsAsync((VatDeclaration d) =>
                    {
                        d.Id = this.Declarations.Count + 1;
                        this.Declarations.Add(d);
                        return d;
                    });
                this.Broker.Setup(s => s.DeleteVatDeclarationAsync(It.IsAny<VatDeclaration>()))
                    .Returns((VatDeclaration d) =>
                    {
                        this.Declarations.Remove(d);
                        return ValueTask.CompletedTask;
                    });
                this.Broker.Setup(s => s.UpdateFiscalPeriodAsync(It.IsAny<FiscalPeriod>()))
                    .ReturnsAsync((FiscalPeriod p) => p);
            }
        }

        private static AccountingEntry VatEntry(
            int id,
            DateTime date,
            string status,
            string companyId,
            string vatAccount,
            decimal debit,
            decimal credit,
            string counterpart = "411000") => new()
        {
            Id = id,
            EntryNumber = $"EC-{id:D4}",
            EntryDate = date,
            Status = status,
            CompanyId = companyId,
            JournalType = "VEN",
            Description = $"Pièce {id}",
            Lines = new List<AccountingEntryLine>
            {
                new() { AccountCode = vatAccount, AccountLabel = "TVA", Debit = debit, Credit = credit, LineNumber = 1 },
                new() { AccountCode = counterpart, AccountLabel = "Contrepartie", Debit = credit, Credit = debit, LineNumber = 2 }
            }
        };

        [Fact]
        public async Task Calculate_Collected21Percent_InfersBase()
        {
            var storage = new FakeVatStorage();
            storage.VatMaps.Add(new CompanyVatRateAccount
            {
                CompanyId = "c1",
                Rate = 21m,
                CollectedAccountCode = "445710",
                DeductibleAccountCode = "445660"
            });
            storage.Entries.Add(VatEntry(1, new DateTime(2026, 3, 10), "Posted", "c1", "445710", 0m, 210m));

            var dto = await VatDeclarationService.GetAsync(storage.Broker.Object, "c1", 2026, 3);

            Assert.Equal("Draft", dto.Status);
            var row = Assert.Single(dto.Rates);
            Assert.Equal(21m, row.Rate);
            Assert.Equal(210m, row.CollectedVat);
            Assert.Equal(1000m, row.CollectedBase);
            Assert.Equal(210m, dto.TotalCollected);
            Assert.Equal(210m, dto.NetToPay);
        }

        [Fact]
        public async Task Calculate_PurchaseDeductible_ReducesNet()
        {
            var storage = new FakeVatStorage();
            storage.VatMaps.Add(new CompanyVatRateAccount
            {
                CompanyId = "c1",
                Rate = 21m,
                CollectedAccountCode = "445710",
                DeductibleAccountCode = "445660"
            });
            storage.Entries.Add(VatEntry(1, new DateTime(2026, 3, 5), "Posted", "c1", "445710", 0m, 210m));
            storage.Entries.Add(VatEntry(2, new DateTime(2026, 3, 8), "Validated", "c1", "445660", 42m, 0m, "401000"));

            var dto = await VatDeclarationService.GetAsync(storage.Broker.Object, "c1", 2026, 3);

            Assert.Equal(210m, dto.TotalCollected);
            Assert.Equal(42m, dto.TotalDeductible);
            Assert.Equal(168m, dto.NetToPay);
        }

        [Fact]
        public async Task Calculate_CarriesPreviousCredit()
        {
            var storage = new FakeVatStorage();
            storage.Declarations.Add(new VatDeclaration
            {
                Year = 2026,
                Month = 2,
                Status = "Declared",
                CompanyId = "c1",
                NetToPay = -300m
            });
            storage.VatMaps.Add(new CompanyVatRateAccount
            {
                CompanyId = "c1",
                Rate = 21m,
                CollectedAccountCode = "445710",
                DeductibleAccountCode = "445660"
            });
            storage.Entries.Add(VatEntry(1, new DateTime(2026, 3, 10), "Posted", "c1", "445710", 0m, 210m));

            var dto = await VatDeclarationService.GetAsync(storage.Broker.Object, "c1", 2026, 3);

            Assert.Equal(300m, dto.PreviousCredit);
            Assert.Equal(-90m, dto.NetToPay);
        }

        [Fact]
        public async Task Calculate_IgnoresDraftReversedAndOtherMonth()
        {
            var storage = new FakeVatStorage();
            storage.Entries.Add(VatEntry(1, new DateTime(2026, 3, 10), "Posted", "c1", "445710", 0m, 21m));
            storage.Entries.Add(VatEntry(2, new DateTime(2026, 3, 10), "Draft", "c1", "445710", 0m, 999m));
            storage.Entries.Add(VatEntry(3, new DateTime(2026, 3, 10), "Reversed", "c1", "445710", 0m, 888m));
            storage.Entries.Add(VatEntry(4, new DateTime(2026, 2, 10), "Posted", "c1", "445710", 0m, 50m));
            storage.Entries.Add(VatEntry(5, new DateTime(2026, 3, 10), "Posted", "other", "445710", 0m, 70m));

            var dto = await VatDeclarationService.GetAsync(storage.Broker.Object, "c1", 2026, 3);

            Assert.Equal(21m, dto.TotalCollected);
        }

        [Fact]
        public async Task Declare_PersistsSnapshotAndFlagsPeriod()
        {
            var storage = new FakeVatStorage();
            storage.Periods.Add(new FiscalPeriod { Id = 9, Year = 2026, Month = 3, CompanyId = "c1" });
            storage.VatMaps.Add(new CompanyVatRateAccount
            {
                CompanyId = "c1",
                Rate = 21m,
                CollectedAccountCode = "445710",
                DeductibleAccountCode = "445660"
            });
            storage.Entries.Add(VatEntry(1, new DateTime(2026, 3, 10), "Posted", "c1", "445710", 0m, 210m));

            var (dto, error) = await VatDeclarationService.DeclareAsync(
                storage.Broker.Object, "c1", 2026, 3, "Alice");

            Assert.Null(error);
            Assert.NotNull(dto);
            Assert.Equal("Declared", dto!.Status);
            Assert.Equal(210m, dto.NetToPay);
            Assert.True(storage.Periods.Single().IsVatDeclared);
            Assert.Single(storage.Declarations);

            var again = await VatDeclarationService.DeclareAsync(
                storage.Broker.Object, "c1", 2026, 3, "Alice");
            Assert.Null(again.Dto);
            Assert.Contains("déjà déclarée", again.Error);
        }

        [Fact]
        public async Task Undeclare_LockedPeriod_Refused()
        {
            var storage = new FakeVatStorage();
            storage.Periods.Add(new FiscalPeriod
            {
                Id = 9, Year = 2026, Month = 3, CompanyId = "c1", IsLocked = true, IsVatDeclared = true
            });
            storage.Declarations.Add(new VatDeclaration
            {
                Year = 2026, Month = 3, Status = "Declared", CompanyId = "c1", NetToPay = 10m
            });

            var (ok, error) = await VatDeclarationService.UndeclareAsync(
                storage.Broker.Object, "c1", 2026, 3, "Alice");

            Assert.False(ok);
            Assert.Contains("verrouillée", error);
            Assert.Single(storage.Declarations);
        }

        [Fact]
        public void BuildDgiXml_IncludesMoroccanRatesAndNet()
        {
            var dto = new VatDeclarationService.VatDeclarationDto
            {
                Year = 2026,
                Month = 3,
                Status = "Declared",
                TotalCollected = 200m,
                TotalDeductible = 50m,
                PreviousCredit = 10m,
                NetToPay = 140m,
                Rates =
                {
                    new VatDeclarationService.VatRateRowDto
                    {
                        Rate = 20m, CollectedBase = 1000m, CollectedVat = 200m,
                        DeductibleBase = 250m, DeductibleVat = 50m
                    }
                }
            };

            var xml = VatDeclarationService.BuildDgiXml(dto, "c1", "Euro Brico");
            Assert.Contains("<DeclarationTVA", xml);
            Assert.Contains("<Taux20>", xml);
            Assert.Contains("<Base>1000.00</Base>", xml);
            Assert.Contains("<TVA_Nette>140.00</TVA_Nette>", xml);
            Assert.Contains("<Nom>Euro Brico</Nom>", xml);
        }

        [Fact]
        public async Task ExportEdi_ReturnsXmlFile()
        {
            var storage = new FakeVatStorage();
            storage.VatMaps.Add(new CompanyVatRateAccount
            {
                CompanyId = "c1", Rate = 21m, CollectedAccountCode = "445710", DeductibleAccountCode = "445660"
            });
            storage.Entries.Add(VatEntry(1, new DateTime(2026, 3, 10), "Posted", "c1", "445710", 0m, 210m));

            var (file, error) = await VatDeclarationService.ExportEdiAsync(storage.Broker.Object, "c1", 2026, 3);
            Assert.Null(error);
            Assert.EndsWith(".xml", file!.FileName);
            var xml = System.Text.Encoding.UTF8.GetString(file.Content);
            Assert.Contains("<TVA_Collectee>", xml);
            Assert.Contains("<Taux21>", xml);
        }
    }
}
