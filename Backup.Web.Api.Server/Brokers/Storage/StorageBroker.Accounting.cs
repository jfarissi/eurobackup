using System.Linq;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Models.Entities;
using Backup.Web.Api.Server.Models.Entities.Accounting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Backup.Web.Api.Server.Brokers.Storage
{
    public partial class StorageBroker
    {
        public DbSet<ChartOfAccount> ChartOfAccounts { get; set; } = null!;
        public DbSet<Journal> Journals { get; set; } = null!;
        public DbSet<FiscalYear> FiscalYears { get; set; } = null!;
        public DbSet<FiscalPeriod> FiscalPeriods { get; set; } = null!;
        public DbSet<CompanyAccountingSettings> CompanyAccountingSettings { get; set; } = null!;
        public DbSet<CompanyVatRateAccount> CompanyVatRateAccounts { get; set; } = null!;
        public DbSet<VatDeclaration> VatDeclarations { get; set; } = null!;
        public DbSet<BankReconciliation> BankReconciliations { get; set; } = null!;
        public DbSet<FixedAsset> FixedAssets { get; set; } = null!;
        public DbSet<Employee> Employees { get; set; } = null!;
        public DbSet<Payslip> Payslips { get; set; } = null!;
        public DbSet<AccountingFirm> AccountingFirms { get; set; } = null!;
        public DbSet<AccountingAnnotation> AccountingAnnotations { get; set; } = null!;

        // Lignes d'écritures (contrôles Phase 1 : compte utilisé, lettrage)
        public IQueryable<AccountingEntryLine> SelectAllAccountingEntryLines() =>
            this.AccountingEntryLines.AsQueryable();

        // Phase 3 : mise à jour d'une ligne (lettrage comptable).
        public async ValueTask<AccountingEntryLine> UpdateAccountingEntryLineAsync(AccountingEntryLine line)
        {
            EntityEntry<AccountingEntryLine> entry = this.AccountingEntryLines.Update(line);
            await this.SaveChangesAsync();
            return entry.Entity;
        }

        // Plan comptable (Phase 1)
        public async ValueTask<ChartOfAccount> InsertChartOfAccountAsync(ChartOfAccount account)
        {
            EntityEntry<ChartOfAccount> entry = await this.ChartOfAccounts.AddAsync(account);
            await this.SaveChangesAsync();
            return entry.Entity;
        }

        public IQueryable<ChartOfAccount> SelectAllChartOfAccounts() => this.ChartOfAccounts.AsQueryable();

        public async ValueTask<ChartOfAccount?> SelectChartOfAccountByIdAsync(int id) =>
            await this.ChartOfAccounts.FindAsync(id);

        public async ValueTask<ChartOfAccount> UpdateChartOfAccountAsync(ChartOfAccount account)
        {
            EntityEntry<ChartOfAccount> entry = this.ChartOfAccounts.Update(account);
            await this.SaveChangesAsync();
            return entry.Entity;
        }

        public async ValueTask DeleteChartOfAccountAsync(ChartOfAccount account)
        {
            this.ChartOfAccounts.Remove(account);
            await this.SaveChangesAsync();
        }

        // Journaux comptables
        public async ValueTask<Journal> InsertJournalAsync(Journal journal)
        {
            EntityEntry<Journal> entry = await this.Journals.AddAsync(journal);
            await this.SaveChangesAsync();
            return entry.Entity;
        }

        public IQueryable<Journal> SelectAllJournals() => this.Journals.AsQueryable();

        public async ValueTask<Journal?> SelectJournalByIdAsync(int id) =>
            await this.Journals.FindAsync(id);

        public async ValueTask<Journal> UpdateJournalAsync(Journal journal)
        {
            EntityEntry<Journal> entry = this.Journals.Update(journal);
            await this.SaveChangesAsync();
            return entry.Entity;
        }

        public async ValueTask DeleteJournalAsync(Journal journal)
        {
            this.Journals.Remove(journal);
            await this.SaveChangesAsync();
        }

        // Exercices & périodes fiscales
        public async ValueTask<FiscalYear> InsertFiscalYearAsync(FiscalYear fiscalYear)
        {
            EntityEntry<FiscalYear> entry = await this.FiscalYears.AddAsync(fiscalYear);
            await this.SaveChangesAsync();
            return entry.Entity;
        }

        public IQueryable<FiscalYear> SelectAllFiscalYears() =>
            this.FiscalYears.Include(f => f.Periods).AsQueryable();

        public async ValueTask<FiscalYear?> SelectFiscalYearByIdAsync(int id) =>
            await this.FiscalYears.Include(f => f.Periods).FirstOrDefaultAsync(f => f.Id == id);

        public async ValueTask<FiscalYear> UpdateFiscalYearAsync(FiscalYear fiscalYear)
        {
            EntityEntry<FiscalYear> entry = this.FiscalYears.Update(fiscalYear);
            await this.SaveChangesAsync();
            return entry.Entity;
        }

        public IQueryable<FiscalPeriod> SelectAllFiscalPeriods() => this.FiscalPeriods.AsQueryable();

        public async ValueTask<FiscalPeriod?> SelectFiscalPeriodByIdAsync(int id) =>
            await this.FiscalPeriods.FindAsync(id);

        public async ValueTask<FiscalPeriod> UpdateFiscalPeriodAsync(FiscalPeriod period)
        {
            EntityEntry<FiscalPeriod> entry = this.FiscalPeriods.Update(period);
            await this.SaveChangesAsync();
            return entry.Entity;
        }

        // Paramètres comptables par société
        public async ValueTask<CompanyAccountingSettings> InsertCompanyAccountingSettingsAsync(CompanyAccountingSettings settings)
        {
            EntityEntry<CompanyAccountingSettings> entry = await this.CompanyAccountingSettings.AddAsync(settings);
            await this.SaveChangesAsync();
            return entry.Entity;
        }

        public IQueryable<CompanyAccountingSettings> SelectAllCompanyAccountingSettings() =>
            this.CompanyAccountingSettings.AsQueryable();

        public async ValueTask<CompanyAccountingSettings> UpdateCompanyAccountingSettingsAsync(CompanyAccountingSettings settings)
        {
            EntityEntry<CompanyAccountingSettings> entry = this.CompanyAccountingSettings.Update(settings);
            await this.SaveChangesAsync();
            return entry.Entity;
        }

        // Mapping TVA par taux (Phase 2)
        public async ValueTask<CompanyVatRateAccount> InsertCompanyVatRateAccountAsync(CompanyVatRateAccount mapping)
        {
            EntityEntry<CompanyVatRateAccount> entry = await this.CompanyVatRateAccounts.AddAsync(mapping);
            await this.SaveChangesAsync();
            return entry.Entity;
        }

        public IQueryable<CompanyVatRateAccount> SelectAllCompanyVatRateAccounts() =>
            this.CompanyVatRateAccounts.AsQueryable();

        public async ValueTask<CompanyVatRateAccount?> SelectCompanyVatRateAccountByIdAsync(int id) =>
            await this.CompanyVatRateAccounts.FindAsync(id);

        public async ValueTask<CompanyVatRateAccount> UpdateCompanyVatRateAccountAsync(CompanyVatRateAccount mapping)
        {
            EntityEntry<CompanyVatRateAccount> entry = this.CompanyVatRateAccounts.Update(mapping);
            await this.SaveChangesAsync();
            return entry.Entity;
        }

        public async ValueTask DeleteCompanyVatRateAccountAsync(CompanyVatRateAccount mapping)
        {
            this.CompanyVatRateAccounts.Remove(mapping);
            await this.SaveChangesAsync();
        }

        // Déclarations TVA
        public async ValueTask<VatDeclaration> InsertVatDeclarationAsync(VatDeclaration declaration)
        {
            EntityEntry<VatDeclaration> entry = await this.VatDeclarations.AddAsync(declaration);
            await this.SaveChangesAsync();
            return entry.Entity;
        }

        public IQueryable<VatDeclaration> SelectAllVatDeclarations() =>
            this.VatDeclarations.Include(d => d.Lines).AsQueryable();

        public async ValueTask DeleteVatDeclarationAsync(VatDeclaration declaration)
        {
            this.VatDeclarations.Remove(declaration);
            await this.SaveChangesAsync();
        }

        // Rapprochements bancaires
        public async ValueTask<BankReconciliation> InsertBankReconciliationAsync(BankReconciliation reconciliation)
        {
            EntityEntry<BankReconciliation> entry = await this.BankReconciliations.AddAsync(reconciliation);
            await this.SaveChangesAsync();
            return entry.Entity;
        }

        public IQueryable<BankReconciliation> SelectAllBankReconciliations() =>
            this.BankReconciliations.Include(r => r.Lines).AsQueryable();

        public async ValueTask<BankReconciliation> UpdateBankReconciliationAsync(BankReconciliation reconciliation)
        {
            EntityEntry<BankReconciliation> entry = this.BankReconciliations.Update(reconciliation);
            await this.SaveChangesAsync();
            return entry.Entity;
        }

        // Immobilisations
        public async ValueTask<FixedAsset> InsertFixedAssetAsync(FixedAsset asset)
        {
            EntityEntry<FixedAsset> entry = await this.FixedAssets.AddAsync(asset);
            await this.SaveChangesAsync();
            return entry.Entity;
        }

        public IQueryable<FixedAsset> SelectAllFixedAssets() =>
            this.FixedAssets.Include(a => a.Schedule).AsQueryable();

        public async ValueTask<FixedAsset> UpdateFixedAssetAsync(FixedAsset asset)
        {
            EntityEntry<FixedAsset> entry = this.FixedAssets.Update(asset);
            await this.SaveChangesAsync();
            return entry.Entity;
        }

        // Paie
        public async ValueTask<Employee> InsertEmployeeAsync(Employee employee)
        {
            EntityEntry<Employee> entry = await this.Employees.AddAsync(employee);
            await this.SaveChangesAsync();
            return entry.Entity;
        }

        public IQueryable<Employee> SelectAllEmployees() => this.Employees.AsQueryable();

        public async ValueTask<Employee> UpdateEmployeeAsync(Employee employee)
        {
            EntityEntry<Employee> entry = this.Employees.Update(employee);
            await this.SaveChangesAsync();
            return entry.Entity;
        }

        public async ValueTask<Payslip> InsertPayslipAsync(Payslip payslip)
        {
            EntityEntry<Payslip> entry = await this.Payslips.AddAsync(payslip);
            await this.SaveChangesAsync();
            return entry.Entity;
        }

        public IQueryable<Payslip> SelectAllPayslips() =>
            this.Payslips.Include(p => p.Employee).AsQueryable();

        public async ValueTask<Payslip> UpdatePayslipAsync(Payslip payslip)
        {
            EntityEntry<Payslip> entry = this.Payslips.Update(payslip);
            await this.SaveChangesAsync();
            return entry.Entity;
        }

        public async ValueTask<AccountingFirm> InsertAccountingFirmAsync(AccountingFirm firm)
        {
            EntityEntry<AccountingFirm> entry = await this.AccountingFirms.AddAsync(firm);
            await this.SaveChangesAsync();
            return entry.Entity;
        }

        public IQueryable<AccountingFirm> SelectAllAccountingFirms() =>
            this.AccountingFirms.Include(f => f.Clients).AsQueryable();

        public async ValueTask<AccountingFirm> UpdateAccountingFirmAsync(AccountingFirm firm)
        {
            EntityEntry<AccountingFirm> entry = this.AccountingFirms.Update(firm);
            await this.SaveChangesAsync();
            return entry.Entity;
        }

        public async ValueTask<AccountingAnnotation> InsertAccountingAnnotationAsync(AccountingAnnotation annotation)
        {
            EntityEntry<AccountingAnnotation> entry = await this.AccountingAnnotations.AddAsync(annotation);
            await this.SaveChangesAsync();
            return entry.Entity;
        }

        public IQueryable<AccountingAnnotation> SelectAllAccountingAnnotations() =>
            this.AccountingAnnotations.AsQueryable();

        public async ValueTask<AccountingAnnotation> UpdateAccountingAnnotationAsync(AccountingAnnotation annotation)
        {
            EntityEntry<AccountingAnnotation> entry = this.AccountingAnnotations.Update(annotation);
            await this.SaveChangesAsync();
            return entry.Entity;
        }
    }
}
