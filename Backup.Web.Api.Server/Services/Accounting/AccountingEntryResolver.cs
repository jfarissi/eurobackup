using System;
using System.Linq;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Models.Entities.Accounting;

namespace Backup.Web.Api.Server.Services.Accounting
{
    /// <summary>
    /// Résolution des paramètres de génération automatique des écritures (Phase 2) :
    /// comptes par défaut de la société, comptes de TVA par taux, journal structuré et
    /// période fiscale. En l'absence de paramétrage (settings, mappings, journaux, exercices),
    /// chaque méthode retombe sur le comportement historique (comptes PCG en dur, RG-CO3 legacy).
    /// </summary>
    public static class AccountingEntryResolver
    {
        /// <summary>Résultat de la résolution de période fiscale : période trouvée (nullable) et erreur éventuelle.</summary>
        public sealed class FiscalPeriodResolution
        {
            public FiscalPeriod? Period { get; set; }
            public string? Error { get; set; }
        }

        /// <summary>
        /// Paramètres comptables de la société ; si absents, retourne une instance portant les
        /// valeurs par défaut (= les comptes historiques en dur d'AccountingLedger).
        /// </summary>
        public static Task<CompanyAccountingSettings> ResolveSettingsAsync(IStorageBroker storage, string? companyId)
        {
            // LINQ synchrone volontaire : permet les tests sur IQueryable en mémoire (Moq).
            var settings = string.IsNullOrWhiteSpace(companyId)
                ? null
                : storage.SelectAllCompanyAccountingSettings().FirstOrDefault(s => s.CompanyId == companyId);
            return Task.FromResult(settings ?? new CompanyAccountingSettings { CompanyId = companyId ?? string.Empty });
        }

        /// <summary>
        /// Compte de TVA pour un taux donné : mapping CompanyVatRateAccount (CompanyId, Rate) s'il existe,
        /// sinon compte collecté/déductible par défaut des paramètres de la société.
        /// </summary>
        public static async Task<string> ResolveVatAccountsAsync(
            IStorageBroker storage,
            string? companyId,
            decimal rate,
            bool isCollected)
        {
            if (!string.IsNullOrWhiteSpace(companyId))
            {
                var mapping = storage.SelectAllCompanyVatRateAccounts()
                    .FirstOrDefault(v => v.CompanyId == companyId && v.Rate == rate);
                var mapped = mapping == null
                    ? null
                    : (isCollected ? mapping.CollectedAccountCode : mapping.DeductibleAccountCode);
                if (!string.IsNullOrWhiteSpace(mapped)) return mapped.Trim();
            }

            var settings = await ResolveSettingsAsync(storage, companyId);
            return isCollected ? settings.VatCollectedAccountCode : settings.VatDeductibleAccountCode;
        }

        /// <summary>
        /// Journal structuré par code (ACH/VEN/BAN/CAIS/OD/AN) ; null si absent —
        /// la génération d'écriture ne doit jamais être bloquée pour un journal manquant.
        /// </summary>
        public static Task<Journal?> ResolveJournalAsync(IStorageBroker storage, string? companyId, string journalCode)
        {
            Journal? journal = null;
            if (!string.IsNullOrWhiteSpace(companyId) && !string.IsNullOrWhiteSpace(journalCode))
            {
                var code = journalCode.Trim();
                journal = storage.SelectAllJournals()
                    .FirstOrDefault(j => j.CompanyId == companyId && j.Code == code);
            }
            return Task.FromResult(journal);
        }

        /// <summary>
        /// Validation et résolution de la période fiscale de comptabilisation :
        /// - si la société a au moins un exercice : la date doit être couverte par un exercice
        ///   ouvert, et la période mensuelle correspondante ne doit pas être verrouillée ;
        /// - sinon (legacy RG-CO3) : délègue aux bornes Company.OpenFiscalPeriodStart/End
        ///   via AccountingLedger.ValidateOpenFiscalPeriodAsync (période null, comportement inchangé).
        /// </summary>
        public static async Task<FiscalPeriodResolution> ResolvePeriodAsync(
            IStorageBroker storage,
            string? companyId,
            DateTime entryDate)
        {
            if (!string.IsNullOrWhiteSpace(companyId))
            {
                var hasFiscalYears = storage.SelectAllFiscalYears().Any(f => f.CompanyId == companyId);
                if (hasFiscalYears)
                {
                    var date = entryDate.Date;
                    var year = storage.SelectAllFiscalYears()
                        .Where(f => f.CompanyId == companyId && f.Status == "Open")
                        .AsEnumerable()
                        .FirstOrDefault(f => f.StartDate.Date <= date && date <= f.EndDate.Date);
                    if (year == null)
                    {
                        return new FiscalPeriodResolution
                        {
                            Error = $"La date comptable ({entryDate:dd/MM/yyyy}) n'est couverte par aucun exercice ouvert de la société."
                        };
                    }

                    var period = storage.SelectAllFiscalPeriods()
                        .FirstOrDefault(p => p.FiscalYearId == year.Id && p.Year == date.Year && p.Month == date.Month);
                    if (period is { IsLocked: true })
                    {
                        return new FiscalPeriodResolution
                        {
                            Error = $"La période comptable {period.Month:00}/{period.Year} est verrouillée : aucune écriture ne peut y être postée."
                        };
                    }

                    return new FiscalPeriodResolution { Period = period };
                }
            }

            // Aucun exercice structuré : comportement legacy inchangé (bornes de la société).
            var legacyError = await AccountingLedger.ValidateOpenFiscalPeriodAsync(storage, companyId, entryDate);
            return new FiscalPeriodResolution { Error = legacyError };
        }
    }
}
