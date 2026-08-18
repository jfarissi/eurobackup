using System;
using System.Linq;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Models.Entities.Accounting;
using Backup.Web.Api.Server.Models.Entities.SaaS;
using Microsoft.Extensions.Logging;

namespace Backup.Web.Api.Server.Services.Accounting
{
    /// <summary>
    /// Seed comptable idempotent par société (Phase 1) : paramètres par défaut, 7 journaux,
    /// plan comptable (PCM Maroc ou PCG Europe selon PlanType) et exercice courant issu des
    /// bornes legacy Company.OpenFiscalPeriodStart/End (conservées pendant la transition).
    /// </summary>
    public class AccountingSeedService
    {
        public const string PlanTypePcmMaroc = "PcmMaroc";
        public const string PlanTypePcgEurope = "PcgEurope";

        private readonly IStorageBroker storage;
        private readonly ILogger<AccountingSeedService> logger;

        public AccountingSeedService(IStorageBroker storage, ILogger<AccountingSeedService> logger)
        {
            this.storage = storage;
            this.logger = logger;
        }

        /// <summary>Seed de toutes les sociétés existantes.</summary>
        public async Task EnsureDefaultsAsync()
        {
            // LINQ synchrone volontaire : permet les tests sur IQueryable en mémoire (Moq).
            var companies = this.storage.SelectAllCompanies().ToList();
            foreach (var company in companies)
            {
                await this.EnsureCompanyAsync(company);
            }
        }

        /// <summary>Seed d'une société : settings → journaux → plan comptable → exercice.</summary>
        public async Task EnsureCompanyAsync(Company company)
        {
            var settings = await this.EnsureSettingsAsync(company);
            await this.EnsureJournalsAsync(company, settings);
            await this.EnsureChartOfAccountsAsync(company, settings);
            await this.EnsureDefaultVatRateAsync(company, settings);
            await this.EnsureFiscalYearFromLegacyBoundsAsync(company);
        }

        private async Task<CompanyAccountingSettings> EnsureSettingsAsync(Company company)
        {
            var existing = this.storage.SelectAllCompanyAccountingSettings()
                .FirstOrDefault(s => s.CompanyId == company.Id);
            if (existing != null) return existing;

            var settings = new CompanyAccountingSettings
            {
                CompanyId = company.Id,
                PlanType = PlanTypePcgEurope
            };
            var created = await this.storage.InsertCompanyAccountingSettingsAsync(settings);
            this.logger.LogInformation("Accounting seed: paramètres comptables créés pour la société {CompanyId}", company.Id);
            return created;
        }

        private async Task EnsureJournalsAsync(Company company, CompanyAccountingSettings settings)
        {
            var existing = this.storage.SelectAllJournals().Where(j => j.CompanyId == company.Id).ToList();
            if (existing.Count > 0)
            {
                if (!existing.Any(j => j.Code == "SAL"))
                {
                    await this.storage.InsertJournalAsync(new Journal
                    {
                        Code = "SAL",
                        Label = "Journal de paie",
                        CompanyId = company.Id
                    });
                }
                return;
            }

            var journals = new[]
            {
                new Journal { Code = "ACH", Label = "Journal des achats", CompanyId = company.Id },
                new Journal { Code = "VEN", Label = "Journal des ventes", CompanyId = company.Id },
                new Journal { Code = "BAN", Label = "Journal de banque", CounterpartAccountCode = settings.BankAccountCode, CompanyId = company.Id },
                new Journal { Code = "CAIS", Label = "Journal de caisse", CounterpartAccountCode = settings.CashAccountCode, CompanyId = company.Id },
                new Journal { Code = "OD", Label = "Journal des opérations diverses", CompanyId = company.Id },
                new Journal { Code = "AN", Label = "Journal des à-nouveaux", CompanyId = company.Id },
                new Journal { Code = "SAL", Label = "Journal de paie", CompanyId = company.Id },
            };

            foreach (var journal in journals)
            {
                await this.storage.InsertJournalAsync(journal);
            }
            this.logger.LogInformation("Accounting seed: 7 journaux créés pour la société {CompanyId}", company.Id);
        }

        private async Task EnsureChartOfAccountsAsync(Company company, CompanyAccountingSettings settings)
        {
            var hasAny = this.storage.SelectAllChartOfAccounts().Any(a => a.CompanyId == company.Id);
            if (hasAny) return;

            var plan = string.Equals(settings.PlanType, PlanTypePcmMaroc, StringComparison.OrdinalIgnoreCase)
                ? AccountingChartSeedData.PcmMaroc
                : AccountingChartSeedData.PcgEurope;

            foreach (var seed in plan)
            {
                await this.storage.InsertChartOfAccountAsync(new ChartOfAccount
                {
                    AccountNumber = seed.AccountNumber,
                    Label = seed.Label,
                    AccountClass = seed.AccountClass,
                    AccountType = seed.AccountType,
                    IsLettrable = seed.IsLettrable,
                    IsBilan = seed.IsBilan,
                    IsResultat = seed.IsResultat,
                    CompanyId = company.Id
                });
            }
            this.logger.LogInformation(
                "Accounting seed: plan {PlanType} ({Count} comptes) créé pour la société {CompanyId}",
                settings.PlanType, plan.Count, company.Id);
        }

        /// <summary>
        /// Mapping TVA du taux standard du plan (21 % PCG Europe / 20 % PCM Maroc) vers les
        /// comptes collecté/déductible par défaut, pour ventiler la déclaration.
        /// </summary>
        private async Task EnsureDefaultVatRateAsync(Company company, CompanyAccountingSettings settings)
        {
            var hasAny = this.storage.SelectAllCompanyVatRateAccounts().Any(v => v.CompanyId == company.Id);
            if (hasAny) return;

            var rate = string.Equals(settings.PlanType, PlanTypePcmMaroc, StringComparison.OrdinalIgnoreCase)
                ? 20m
                : 21m;
            await this.storage.InsertCompanyVatRateAccountAsync(new CompanyVatRateAccount
            {
                CompanyId = company.Id,
                Rate = rate,
                CollectedAccountCode = settings.VatCollectedAccountCode,
                DeductibleAccountCode = settings.VatDeductibleAccountCode
            });
            this.logger.LogInformation(
                "Accounting seed: mapping TVA {Rate}% créé pour la société {CompanyId}",
                rate, company.Id);
        }

        /// <summary>
        /// Migration de données : crée l'exercice + ses périodes mensuelles à partir des bornes
        /// legacy OpenFiscalPeriodStart/End si la société n'a pas encore d'exercice.
        /// </summary>
        private async Task EnsureFiscalYearFromLegacyBoundsAsync(Company company)
        {
            if (company.OpenFiscalPeriodStart == null || company.OpenFiscalPeriodEnd == null) return;

            var hasAny = this.storage.SelectAllFiscalYears().Any(f => f.CompanyId == company.Id);
            if (hasAny) return;

            var start = company.OpenFiscalPeriodStart.Value.Date;
            var end = company.OpenFiscalPeriodEnd.Value.Date;
            if (end < start) return;

            var fiscalYear = new FiscalYear
            {
                Name = FiscalYearCalendar.BuildYearName(start, end),
                StartDate = start,
                EndDate = end,
                Status = "Open",
                CompanyId = company.Id,
                Periods = FiscalYearCalendar.BuildMonthlyPeriods(start, end, company.Id)
            };
            await this.storage.InsertFiscalYearAsync(fiscalYear);
            this.logger.LogInformation(
                "Accounting seed: exercice {Name} ({PeriodCount} périodes) créé pour la société {CompanyId} depuis les bornes legacy",
                fiscalYear.Name, fiscalYear.Periods.Count, company.Id);
        }
    }
}
