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

        // Lignes d'écritures (contrôles Phase 1 : compte utilisé, lettrage)
        public IQueryable<AccountingEntryLine> SelectAllAccountingEntryLines() =>
            this.AccountingEntryLines.AsQueryable();

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
    }
}
