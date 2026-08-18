using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Models.Entities;
using Backup.Web.Api.Server.Services.Numbering;
using Backup.Web.Api.Server.Services.Sales;
using Backup.Web.Api.Server.Services.Tenancy;

namespace Backup.Web.Api.Server.Services.Accounting
{
    /// <summary>
    /// Phase 3 — cycle de vie des écritures comptables :
    /// Draft (brouillon) → Posted (comptabilisée) → Validated (validée, immuable),
    /// et Reversed (extournée) via une écriture inverse (extourne).
    /// Les écritures automatiques existantes restent "Posted" : rien ne change pour elles.
    /// </summary>
    public static class AccountingEntryLifecycleService
    {
        /// <summary>Convention d'extourne : ReferenceType = "Reversal", ReferenceId = id de l'écriture d'origine.</summary>
        public const string RefReversal = "Reversal";

        private const string JournalCodeOperationsDiverses = "OD";

        /// <summary>Posted → Validated. Refus si introuvable, pas Posted, ou période verrouillée / hors exercice.</summary>
        public static async Task<(AccountingEntry? Entry, string? Error)> ValidateAsync(
            IStorageBroker storage,
            int entryId,
            string? companyId,
            string? user)
        {
            var entry = await storage.SelectAccountingEntryByIdAsync(entryId);
            if (entry == null || !entry.BelongsToCompany(companyId))
                return (null, "Écriture introuvable.");

            if (!string.Equals(entry.Status, "Posted", StringComparison.OrdinalIgnoreCase))
                return (null, $"Seule une écriture comptabilisée (Posted) peut être validée (statut actuel : {entry.Status}).");

            // Si la période est déjà renseignée, son verrouillage suffit ; sinon résolution complète
            // sur la date de l'écriture (exercice ouvert + période non verrouillée, fallback legacy).
            if (entry.FiscalPeriodId.HasValue)
            {
                var period = await storage.SelectFiscalPeriodByIdAsync(entry.FiscalPeriodId.Value);
                if (period is { IsLocked: true })
                {
                    return (null, $"La période comptable {period.Month:00}/{period.Year} est verrouillée : validation impossible.");
                }
            }
            else
            {
                var resolution = await AccountingEntryResolver.ResolvePeriodAsync(storage, companyId, entry.EntryDate);
                if (resolution.Error != null) return (null, resolution.Error);
            }

            entry.Status = "Validated";
            entry.UpdatedAt = DateTime.UtcNow;
            entry.UpdatedBy = SalesDocumentAudit.IsReadableActor(user) ? user!.Trim() : null;
            var updated = await storage.UpdateAccountingEntryAsync(entry);
            return (updated, null);
        }

        /// <summary>
        /// Extourne une écriture Posted : crée l'écriture inverse (lignes débit↔crédit, datée du jour,
        /// journal OD, ReferenceType "Reversal" → id de l'originale), puis passe l'originale à Reversed.
        /// Refus si introuvable, déjà Validated/Reversed, extourne déjà existante ou période du jour verrouillée.
        /// </summary>
        public static async Task<(AccountingEntry? Entry, string? Error)> ReverseAsync(
            IStorageBroker storage,
            INumberingSequenceService numbering,
            int entryId,
            string? companyId,
            string? user)
        {
            var entry = await storage.SelectAccountingEntryByIdAsync(entryId);
            if (entry == null || !entry.BelongsToCompany(companyId))
                return (null, "Écriture introuvable.");

            if (string.Equals(entry.Status, "Validated", StringComparison.OrdinalIgnoreCase))
                return (null, "Une écriture validée est immuable : extourne impossible.");
            if (string.Equals(entry.Status, "Reversed", StringComparison.OrdinalIgnoreCase))
                return (null, "Cette écriture est déjà extournée.");
            if (!string.Equals(entry.Status, "Posted", StringComparison.OrdinalIgnoreCase))
                return (null, $"Seule une écriture comptabilisée (Posted) peut être extournée (statut actuel : {entry.Status}).");

            // Anti-doublon : une seule extourne active par écriture (même convention ReferenceType/ReferenceId).
            var reversalExists = storage.SelectAllAccountingEntries()
                .ForCompany(companyId)
                .Any(e => e.ReferenceType == RefReversal && e.ReferenceId == entryId
                    && (e.Status == "Posted" || e.Status == "Validated"));
            if (reversalExists)
                return (null, $"Une extourne existe déjà pour l'écriture {entry.EntryNumber}.");

            var entryDate = DateTime.UtcNow;
            var period = await AccountingEntryResolver.ResolvePeriodAsync(storage, companyId, entryDate);
            if (period.Error != null) return (null, period.Error);

            // Journal des opérations diverses (null si absent : ne bloque jamais l'extourne).
            var journal = await AccountingEntryResolver.ResolveJournalAsync(storage, companyId, JournalCodeOperationsDiverses);

            var reversal = new AccountingEntry
            {
                EntryNumber = await numbering.GetNextNumberAsync("AccountingEntry", companyId),
                EntryDate = entryDate,
                JournalType = RefReversal,
                JournalId = journal?.Id,
                FiscalPeriodId = period.Period?.Id,
                ReferenceType = RefReversal,
                ReferenceId = entry.Id,
                Description = $"Extourne {entry.EntryNumber}",
                Status = "Posted",
                CompanyId = companyId,
                CreatedBy = SalesDocumentAudit.IsReadableActor(user) ? user!.Trim() : null,
                CreatedAt = DateTime.UtcNow,
                // Lignes inversées : équilibrée par construction (l'originale est équilibrée).
                Lines = entry.Lines
                    .OrderBy(l => l.LineNumber)
                    .Select(l => new AccountingEntryLine
                    {
                        AccountCode = l.AccountCode,
                        AccountLabel = l.AccountLabel,
                        Debit = l.Credit,
                        Credit = l.Debit,
                        LineNumber = l.LineNumber,
                        ChartOfAccountId = l.ChartOfAccountId
                    })
                    .ToList()
            };

            var saved = await storage.InsertAccountingEntryAsync(reversal);

            entry.Status = "Reversed";
            entry.UpdatedAt = DateTime.UtcNow;
            entry.UpdatedBy = SalesDocumentAudit.IsReadableActor(user) ? user!.Trim() : null;
            await storage.UpdateAccountingEntryAsync(entry);

            return (saved, null);
        }

        /// <summary>
        /// Draft → Posted : attribution du numéro EC- définitif, validation de période (resolver)
        /// et revérification de l'équilibre débit = crédit.
        /// </summary>
        public static async Task<(AccountingEntry? Entry, string? Error)> PostDraftAsync(
            IStorageBroker storage,
            INumberingSequenceService numbering,
            int entryId,
            string? companyId,
            string? user)
        {
            var entry = await storage.SelectAccountingEntryByIdAsync(entryId);
            if (entry == null || !entry.BelongsToCompany(companyId))
                return (null, "Écriture introuvable.");

            if (!string.Equals(entry.Status, "Draft", StringComparison.OrdinalIgnoreCase))
                return (null, $"Seul un brouillon (Draft) peut être comptabilisé (statut actuel : {entry.Status}).");

            var totalDebit = entry.Lines.Sum(l => l.Debit);
            var totalCredit = entry.Lines.Sum(l => l.Credit);
            if (entry.Lines.Count < 2 || Math.Abs(totalDebit - totalCredit) > 0.01m)
                return (null, $"Écriture non équilibrée : débit {totalDebit:0.##} ≠ crédit {totalCredit:0.##}.");

            var period = await AccountingEntryResolver.ResolvePeriodAsync(storage, companyId, entry.EntryDate);
            if (period.Error != null) return (null, period.Error);

            entry.EntryNumber = await numbering.GetNextNumberAsync("AccountingEntry", companyId);
            entry.FiscalPeriodId = period.Period?.Id;
            entry.Status = "Posted";
            entry.UpdatedAt = DateTime.UtcNow;
            entry.UpdatedBy = SalesDocumentAudit.IsReadableActor(user) ? user!.Trim() : null;
            var updated = await storage.UpdateAccountingEntryAsync(entry);
            return (updated, null);
        }

        /// <summary>Garde brouillon : modification/suppression réservées aux écritures Draft.</summary>
        public static string? RejectIfNotDraft(string? status)
        {
            if (string.Equals(status, "Draft", StringComparison.OrdinalIgnoreCase)) return null;
            return $"Seules les écritures au brouillon (Draft) peuvent être modifiées ou supprimées (statut actuel : {status}).";
        }
    }
}
