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
    /// Phase 2 — génération automatique des écritures : comptes issus des paramètres de la société,
    /// TVA ventilée par taux (CompanyVatRateAccount), journal structuré, période fiscale,
    /// et comportement legacy garanti sans paramétrage.
    /// </summary>
    public class AccountingEntryGeneratorTests
    {
        private sealed class FakeLedgerStorage
        {
            public List<Company> Companies = new();
            public List<Customer> Customers = new();
            public List<Supplier> Suppliers = new();
            public List<AccountingEntry> Entries = new();
            public List<CompanyAccountingSettings> Settings = new();
            public List<CompanyVatRateAccount> VatRateAccounts = new();
            public List<Journal> Journals = new();
            public List<FiscalYear> FiscalYears = new();
            public List<FiscalPeriod> FiscalPeriods = new();

            public Mock<IStorageBroker> Broker { get; }

            public FakeLedgerStorage()
            {
                this.Broker = new Mock<IStorageBroker>();
                // Queryables évalués paresseusement pour refléter les insertions successives.
                this.Broker.Setup(s => s.SelectAllAccountingEntries()).Returns(() => this.Entries.AsQueryable());
                this.Broker.Setup(s => s.SelectAllCompanyAccountingSettings()).Returns(() => this.Settings.AsQueryable());
                this.Broker.Setup(s => s.SelectAllCompanyVatRateAccounts()).Returns(() => this.VatRateAccounts.AsQueryable());
                this.Broker.Setup(s => s.SelectAllJournals()).Returns(() => this.Journals.AsQueryable());
                this.Broker.Setup(s => s.SelectAllFiscalYears()).Returns(() => this.FiscalYears.AsQueryable());
                this.Broker.Setup(s => s.SelectAllFiscalPeriods()).Returns(() => this.FiscalPeriods.AsQueryable());

                this.Broker.Setup(s => s.SelectCompanyByIdAsync(It.IsAny<string>()))
                    .ReturnsAsync((string id) => this.Companies.FirstOrDefault(c => c.Id == id));
                this.Broker.Setup(s => s.SelectCustomerByIdAsync(It.IsAny<int>()))
                    .ReturnsAsync((int id) => this.Customers.FirstOrDefault(c => c.Id == id));
                this.Broker.Setup(s => s.SelectSupplierByIdAsync(It.IsAny<int>()))
                    .ReturnsAsync((int id) => this.Suppliers.FirstOrDefault(x => x.Id == id));
                this.Broker.Setup(s => s.UpdateCustomerAsync(It.IsAny<Customer>()))
                    .ReturnsAsync((Customer c) => c);
                this.Broker.Setup(s => s.UpdateSupplierAsync(It.IsAny<Supplier>()))
                    .ReturnsAsync((Supplier x) => x);
                this.Broker.Setup(s => s.InsertAccountingEntryAsync(It.IsAny<AccountingEntry>()))
                    .ReturnsAsync((AccountingEntry e) => { this.Entries.Add(e); return e; });
            }
        }

        private static Mock<INumberingSequenceService> NewNumbering()
        {
            var numbering = new Mock<INumberingSequenceService>();
            numbering.Setup(n => n.GetNextNumberAsync(It.IsAny<string>(), It.IsAny<string?>()))
                .ReturnsAsync("AC-000001");
            return numbering;
        }

        /// <summary>Exercice ouvert couvrant la date du jour, avec la période mensuelle courante.</summary>
        private static FiscalPeriod AddOpenFiscalYearCoveringToday(
            FakeLedgerStorage storage, string companyId, bool lockCurrentPeriod = false)
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
                IsLocked = lockCurrentPeriod,
                CompanyId = companyId
            };
            year.Periods.Add(period);
            storage.FiscalYears.Add(year);
            storage.FiscalPeriods.Add(period);
            return period;
        }

        private static SalesInvoice NewInvoice(string companyId, params SalesInvoiceLine[] lines)
        {
            var invoice = new SalesInvoice
            {
                Id = 100,
                InvoiceNumber = "FA-0001",
                CustomerId = 1,
                CompanyId = companyId,
                Status = "Validated",
                Lines = lines.ToList()
            };
            invoice.TotalHT = invoice.Lines.Sum(l => l.TotalHT);
            invoice.TotalVat = invoice.Lines.Sum(l => l.TotalTTC - l.TotalHT);
            invoice.TotalTTC = invoice.TotalHT + invoice.TotalVat;
            return invoice;
        }

        [Fact]
        public async Task PostSalesInvoice_UsesCompanySettings_JournalAndPeriod()
        {
            var storage = new FakeLedgerStorage();
            storage.Customers.Add(new Customer { Id = 1, Name = "Client A", CompanyId = "c1" });
            storage.Settings.Add(new CompanyAccountingSettings
            {
                CompanyId = "c1",
                CustomerAccountCode = "411100",
                SalesAccountCode = "701100",
                VatCollectedAccountCode = "445711"
            });
            storage.Journals.Add(new Journal { Id = 7, Code = "VEN", CompanyId = "c1" });
            var period = AddOpenFiscalYearCoveringToday(storage, "c1");

            var invoice = NewInvoice("c1", new SalesInvoiceLine { VatRate = 20m, TotalHT = 100m, TotalTTC = 120m });

            var (entry, error) = await AccountingLedger.PostSalesInvoiceAsync(
                storage.Broker.Object, NewNumbering().Object, invoice, "Test");

            Assert.Null(error);
            Assert.NotNull(entry);
            // Comptes des paramètres (pas les codes en dur), journal VEN et période renseignés.
            Assert.Equal(7, entry!.JournalId);
            Assert.Equal(period.Id, entry.FiscalPeriodId);
            Assert.Equal("SalesInvoice", entry.JournalType); // compatibilité des filtres existants
            Assert.Contains(entry.Lines, l => l.AccountCode == "411100" && l.Debit == 120m && l.Credit == 0m);
            Assert.Contains(entry.Lines, l => l.AccountCode == "701100" && l.Credit == 100m && l.Debit == 0m);
            Assert.Contains(entry.Lines, l => l.AccountCode == "445711" && l.Credit == 20m && l.Debit == 0m);
            Assert.Equal(entry.Lines.Sum(l => l.Debit), entry.Lines.Sum(l => l.Credit));
        }

        [Fact]
        public async Task PostSalesInvoice_SplitsVatPerRate_WithMappedAccounts()
        {
            var storage = new FakeLedgerStorage();
            storage.Customers.Add(new Customer { Id = 1, Name = "Client A", CompanyId = "c1" });
            storage.Settings.Add(new CompanyAccountingSettings { CompanyId = "c1" });
            storage.VatRateAccounts.Add(new CompanyVatRateAccount
            {
                CompanyId = "c1", Rate = 20m, CollectedAccountCode = "445720", DeductibleAccountCode = "445620"
            });
            storage.VatRateAccounts.Add(new CompanyVatRateAccount
            {
                CompanyId = "c1", Rate = 10m, CollectedAccountCode = "445721", DeductibleAccountCode = "445621"
            });

            var invoice = NewInvoice(
                "c1",
                new SalesInvoiceLine { VatRate = 20m, TotalHT = 100m, TotalTTC = 120m }, // TVA 20
                new SalesInvoiceLine { VatRate = 10m, TotalHT = 50m, TotalTTC = 55m });   // TVA 5

            var (entry, error) = await AccountingLedger.PostSalesInvoiceAsync(
                storage.Broker.Object, NewNumbering().Object, invoice, "Test");

            Assert.Null(error);
            Assert.NotNull(entry);
            var vatLines = entry!.Lines.Where(l => l.AccountCode.StartsWith("4457")).ToList();
            Assert.Equal(2, vatLines.Count);
            Assert.Contains(vatLines, l => l.AccountCode == "445720" && l.Credit == 20m);
            Assert.Contains(vatLines, l => l.AccountCode == "445721" && l.Credit == 5m);
            // La somme des TVA ventilées égale exactement TotalVat et la pièce reste équilibrée.
            Assert.Equal(invoice.TotalVat, vatLines.Sum(l => l.Credit));
            Assert.Equal(entry.Lines.Sum(l => l.Debit), entry.Lines.Sum(l => l.Credit));
        }

        [Fact]
        public async Task PostSalesInvoice_RateWithoutMapping_FallsBackToSettingsVatAccount()
        {
            var storage = new FakeLedgerStorage();
            storage.Customers.Add(new Customer { Id = 1, Name = "Client A", CompanyId = "c1" });
            storage.Settings.Add(new CompanyAccountingSettings
            {
                CompanyId = "c1",
                VatCollectedAccountCode = "445719"
            });
            storage.VatRateAccounts.Add(new CompanyVatRateAccount
            {
                CompanyId = "c1", Rate = 20m, CollectedAccountCode = "445720", DeductibleAccountCode = "445620"
            });

            var invoice = NewInvoice(
                "c1",
                new SalesInvoiceLine { VatRate = 20m, TotalHT = 100m, TotalTTC = 120m },  // TVA 20 → mappé
                new SalesInvoiceLine { VatRate = 5.5m, TotalHT = 200m, TotalTTC = 211m }); // TVA 11 → fallback

            var (entry, error) = await AccountingLedger.PostSalesInvoiceAsync(
                storage.Broker.Object, NewNumbering().Object, invoice, "Test");

            Assert.Null(error);
            Assert.NotNull(entry);
            var vatLines = entry!.Lines.Where(l => l.AccountCode.StartsWith("4457")).ToList();
            Assert.Equal(2, vatLines.Count);
            Assert.Contains(vatLines, l => l.AccountCode == "445720" && l.Credit == 20m);
            Assert.Contains(vatLines, l => l.AccountCode == "445719" && l.Credit == 11m);
            Assert.Equal(invoice.TotalVat, vatLines.Sum(l => l.Credit));
            Assert.Equal(entry.Lines.Sum(l => l.Debit), entry.Lines.Sum(l => l.Credit));
        }

        [Fact]
        public async Task PostSalesInvoice_LockedPeriod_ReturnsError_NoEntryInserted()
        {
            var storage = new FakeLedgerStorage();
            storage.Customers.Add(new Customer { Id = 1, Name = "Client A", CompanyId = "c1" });
            AddOpenFiscalYearCoveringToday(storage, "c1", lockCurrentPeriod: true);

            var invoice = NewInvoice("c1", new SalesInvoiceLine { VatRate = 20m, TotalHT = 100m, TotalTTC = 120m });

            var (entry, error) = await AccountingLedger.PostSalesInvoiceAsync(
                storage.Broker.Object, NewNumbering().Object, invoice, "Test");

            Assert.Null(entry);
            Assert.NotNull(error);
            Assert.Contains("verrouillée", error);
            Assert.Empty(storage.Entries);
            Assert.Equal(0m, storage.Customers.Single().Balance); // encours client non touché
        }

        [Fact]
        public async Task PostSalesInvoice_DateNotCoveredByOpenFiscalYear_ReturnsError()
        {
            var storage = new FakeLedgerStorage();
            storage.Customers.Add(new Customer { Id = 1, Name = "Client A", CompanyId = "c1" });
            // Exercice ouvert mais clos avant la date du jour.
            storage.FiscalYears.Add(new FiscalYear
            {
                Id = 1,
                Name = "Exercice 2020",
                StartDate = new DateTime(2020, 1, 1),
                EndDate = new DateTime(2020, 12, 31),
                Status = "Open",
                CompanyId = "c1"
            });

            var invoice = NewInvoice("c1", new SalesInvoiceLine { VatRate = 20m, TotalHT = 100m, TotalTTC = 120m });

            var (entry, error) = await AccountingLedger.PostSalesInvoiceAsync(
                storage.Broker.Object, NewNumbering().Object, invoice, "Test");

            Assert.Null(entry);
            Assert.NotNull(error);
            Assert.Contains("aucun exercice ouvert", error);
            Assert.Empty(storage.Entries);
        }

        [Fact]
        public async Task PostSalesInvoice_LegacyBounds_RejectsDateOutside()
        {
            var storage = new FakeLedgerStorage();
            storage.Customers.Add(new Customer { Id = 1, Name = "Client A", CompanyId = "c1" });
            // Société sans exercice structuré : bornes legacy (RG-CO3) dans le passé → date du jour rejetée.
            storage.Companies.Add(new Company
            {
                Id = "c1",
                OpenFiscalPeriodStart = new DateTime(2020, 1, 1),
                OpenFiscalPeriodEnd = new DateTime(2020, 12, 31)
            });

            var invoice = NewInvoice("c1", new SalesInvoiceLine { VatRate = 20m, TotalHT = 100m, TotalTTC = 120m });

            var (entry, error) = await AccountingLedger.PostSalesInvoiceAsync(
                storage.Broker.Object, NewNumbering().Object, invoice, "Test");

            Assert.Null(entry);
            Assert.NotNull(error);
            Assert.Contains("postérieure", error);
            Assert.Empty(storage.Entries);
        }

        [Fact]
        public async Task PostSalesInvoice_LegacyBounds_AllowsDateInside_PeriodStaysNull()
        {
            var storage = new FakeLedgerStorage();
            storage.Customers.Add(new Customer { Id = 1, Name = "Client A", CompanyId = "c1" });
            storage.Companies.Add(new Company
            {
                Id = "c1",
                OpenFiscalPeriodStart = new DateTime(2020, 1, 1),
                OpenFiscalPeriodEnd = new DateTime(2030, 12, 31)
            });

            var invoice = NewInvoice("c1", new SalesInvoiceLine { VatRate = 20m, TotalHT = 100m, TotalTTC = 120m });

            var (entry, error) = await AccountingLedger.PostSalesInvoiceAsync(
                storage.Broker.Object, NewNumbering().Object, invoice, "Test");

            Assert.Null(error);
            Assert.NotNull(entry);
            // Sans settings / journaux / exercice : comptes historiques, JournalId et FiscalPeriodId null.
            Assert.Null(entry!.JournalId);
            Assert.Null(entry.FiscalPeriodId);
            Assert.Contains(entry.Lines, l => l.AccountCode == "411000" && l.Debit == 120m);
            Assert.Contains(entry.Lines, l => l.AccountCode == "701000" && l.Credit == 100m);
            Assert.Contains(entry.Lines, l => l.AccountCode == "445710" && l.Credit == 20m);
        }

        [Fact]
        public async Task PostSalesPayment_Cash_UsesCashAccountAndCaisseJournal()
        {
            var storage = new FakeLedgerStorage();
            storage.Customers.Add(new Customer { Id = 1, Name = "Client A", CompanyId = "c1" });
            storage.Settings.Add(new CompanyAccountingSettings
            {
                CompanyId = "c1",
                CustomerAccountCode = "411100",
                CashAccountCode = "530100",
                BankAccountCode = "512100"
            });
            storage.Journals.Add(new Journal { Id = 21, Code = "CAIS", CompanyId = "c1" });
            storage.Journals.Add(new Journal { Id = 22, Code = "BAN", CompanyId = "c1" });

            var invoice = NewInvoice("c1", new SalesInvoiceLine { VatRate = 20m, TotalHT = 100m, TotalTTC = 120m });
            var payment = new Payment { Id = 50, Amount = 80m, Method = "Cash", CompanyId = "c1" };

            var (entry, error) = await AccountingLedger.PostSalesPaymentAsync(
                storage.Broker.Object, NewNumbering().Object, invoice, payment, "Test");

            Assert.Null(error);
            Assert.NotNull(entry);
            Assert.Equal(21, entry!.JournalId);
            Assert.Contains(entry.Lines, l => l.AccountCode == "530100" && l.Debit == 80m);
            Assert.Contains(entry.Lines, l => l.AccountCode == "411100" && l.Credit == 80m);
            Assert.Equal(entry.Lines.Sum(l => l.Debit), entry.Lines.Sum(l => l.Credit));
        }

        [Fact]
        public async Task PostSalesPayment_BankTransfer_UsesBankAccountAndBanJournal()
        {
            var storage = new FakeLedgerStorage();
            storage.Customers.Add(new Customer { Id = 1, Name = "Client A", CompanyId = "c1" });
            storage.Settings.Add(new CompanyAccountingSettings
            {
                CompanyId = "c1",
                CustomerAccountCode = "411100",
                CashAccountCode = "530100",
                BankAccountCode = "512100"
            });
            storage.Journals.Add(new Journal { Id = 21, Code = "CAIS", CompanyId = "c1" });
            storage.Journals.Add(new Journal { Id = 22, Code = "BAN", CompanyId = "c1" });

            var invoice = NewInvoice("c1", new SalesInvoiceLine { VatRate = 20m, TotalHT = 100m, TotalTTC = 120m });
            var payment = new Payment { Id = 51, Amount = 80m, Method = "BankTransfer", CompanyId = "c1" };

            var (entry, error) = await AccountingLedger.PostSalesPaymentAsync(
                storage.Broker.Object, NewNumbering().Object, invoice, payment, "Test");

            Assert.Null(error);
            Assert.NotNull(entry);
            Assert.Equal(22, entry!.JournalId);
            Assert.Contains(entry.Lines, l => l.AccountCode == "512100" && l.Debit == 80m);
            Assert.Contains(entry.Lines, l => l.AccountCode == "411100" && l.Credit == 80m);
        }

        [Fact]
        public async Task PostSalesInvoice_ExistingPostedEntry_ShortCircuits()
        {
            var storage = new FakeLedgerStorage();
            storage.Customers.Add(new Customer { Id = 1, Name = "Client A", CompanyId = "c1" });
            storage.Entries.Add(new AccountingEntry
            {
                ReferenceType = AccountingLedger.RefSalesInvoice,
                ReferenceId = 100,
                Status = "Posted",
                CompanyId = "c1"
            });

            var invoice = NewInvoice("c1", new SalesInvoiceLine { VatRate = 20m, TotalHT = 100m, TotalTTC = 120m });

            var (entry, error) = await AccountingLedger.PostSalesInvoiceAsync(
                storage.Broker.Object, NewNumbering().Object, invoice, "Test");

            Assert.Null(entry);
            Assert.Equal("Écriture déjà postée pour cette facture.", error);
            Assert.Single(storage.Entries);
            Assert.Equal(0m, storage.Customers.Single().Balance);
        }

        [Fact]
        public async Task PostDepositInvoice_UsesCustomerDepositAccountFromSettings()
        {
            var storage = new FakeLedgerStorage();
            storage.Customers.Add(new Customer { Id = 1, Name = "Client A", CompanyId = "c1" });
            storage.Settings.Add(new CompanyAccountingSettings
            {
                CompanyId = "c1",
                CustomerAccountCode = "411100",
                CustomerDepositAccountCode = "419100"
            });
            storage.Journals.Add(new Journal { Id = 7, Code = "VEN", CompanyId = "c1" });

            var deposit = new DepositInvoice
            {
                Id = 60,
                DepositNumber = "AAC-0001",
                CustomerId = 1,
                AmountTTC = 300m,
                CompanyId = "c1"
            };

            var (entry, error) = await AccountingLedger.PostDepositInvoiceAsync(
                storage.Broker.Object, NewNumbering().Object, deposit, "Test");

            Assert.Null(error);
            Assert.NotNull(entry);
            Assert.Equal(7, entry!.JournalId);
            Assert.Contains(entry.Lines, l => l.AccountCode == "411100" && l.Debit == 300m);
            Assert.Contains(entry.Lines, l => l.AccountCode == "419100" && l.Credit == 300m);
            Assert.Equal(entry.Lines.Sum(l => l.Debit), entry.Lines.Sum(l => l.Credit));
        }

        [Fact]
        public async Task PostSupplierInvoice_UsesSettings_AchJournal_AndMappedDeductibleVat()
        {
            var storage = new FakeLedgerStorage();
            storage.Suppliers.Add(new Supplier { Id = 2, Name = "Fourn A", CompanyId = "c1" });
            storage.Settings.Add(new CompanyAccountingSettings
            {
                CompanyId = "c1",
                SupplierAccountCode = "401100",
                PurchaseAccountCode = "607100",
                VatDeductibleAccountCode = "445669"
            });
            storage.VatRateAccounts.Add(new CompanyVatRateAccount
            {
                CompanyId = "c1", Rate = 20m, CollectedAccountCode = "445720", DeductibleAccountCode = "445620"
            });
            storage.Journals.Add(new Journal { Id = 30, Code = "ACH", CompanyId = "c1" });

            var invoice = new SupplierInvoiceEntity
            {
                Id = 200,
                InvoiceNumber = "FAF-0001",
                SupplierId = 2,
                CompanyId = "c1",
                Lines = new List<SupplierInvoiceLineEntity>
                {
                    new() { VatRate = 20m, TotalHT = 100m, TotalTTC = 120m },   // TVA 20 → mappé
                    new() { VatRate = 10m, TotalHT = 50m, TotalTTC = 55m }       // TVA 5 → fallback
                }
            };
            invoice.TotalHT = invoice.Lines.Sum(l => l.TotalHT);
            invoice.TotalVat = invoice.Lines.Sum(l => l.TotalTTC - l.TotalHT);
            invoice.TotalTTC = invoice.TotalHT + invoice.TotalVat;

            var (entry, error) = await AccountingLedger.PostSupplierInvoiceAsync(
                storage.Broker.Object, NewNumbering().Object, invoice, "Test");

            Assert.Null(error);
            Assert.NotNull(entry);
            Assert.Equal(30, entry!.JournalId);
            Assert.Contains(entry.Lines, l => l.AccountCode == "607100" && l.Debit == 150m);
            Assert.Contains(entry.Lines, l => l.AccountCode == "445620" && l.Debit == 20m);
            Assert.Contains(entry.Lines, l => l.AccountCode == "445669" && l.Debit == 5m);
            Assert.Contains(entry.Lines, l => l.AccountCode == "401100" && l.Credit == 175m);
            Assert.Equal(entry.Lines.Sum(l => l.Debit), entry.Lines.Sum(l => l.Credit));
        }
    }
}
