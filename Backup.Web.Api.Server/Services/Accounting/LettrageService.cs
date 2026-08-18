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
    /// Phase 3 — lettrage comptable au niveau des lignes d'écritures (LettrageCode/LettrageDate).
    /// Fonctionne pour tout compte lettrable (clients 411000, fournisseurs 401000, …) : couvre
    /// clients ET fournisseurs. Codes LET- via la séquence "Lettering" existante.
    /// Additif par rapport au lettrage métier LetteringsController (documents), qui est conservé.
    /// </summary>
    public static class LettrageService
    {
        /// <summary>Ligne non lettrée avec les infos de son écriture (affichage / sélection).</summary>
        public sealed class LettrageLineDto
        {
            public int LineId { get; set; }
            public int AccountingEntryId { get; set; }
            public string EntryNumber { get; set; } = string.Empty;
            public DateTime EntryDate { get; set; }
            public string Description { get; set; } = string.Empty;
            public string JournalType { get; set; } = string.Empty;
            public int LineNumber { get; set; }
            public string AccountCode { get; set; } = string.Empty;
            public string AccountLabel { get; set; } = string.Empty;
            public decimal Debit { get; set; }
            public decimal Credit { get; set; }
        }

        /// <summary>Groupe de lettrage existant (affichage).</summary>
        public sealed class LettrageGroupDto
        {
            public string Code { get; set; } = string.Empty;
            public DateTime? Date { get; set; }
            public string AccountCode { get; set; } = string.Empty;
            public int LineCount { get; set; }
            public decimal TotalDebit { get; set; }
            public decimal TotalCredit { get; set; }
        }

        /// <summary>Résumé du lettrage automatique pour un compte.</summary>
        public sealed class LettrageAccountSummaryDto
        {
            public string AccountCode { get; set; } = string.Empty;
            public int GroupsCreated { get; set; }
            public List<string> Codes { get; set; } = new();
        }

        /// <summary>Ligne candidate interne : la ligne et son écriture (objets trackés / mutés en place).</summary>
        private sealed class Candidate
        {
            public AccountingEntry Entry { get; set; } = null!;
            public AccountingEntryLine Line { get; set; } = null!;
        }

        /// <summary>Lignes non lettrées d'un compte (écritures Posted/Validated uniquement), triées date puis n° de ligne.</summary>
        public static Task<List<LettrageLineDto>> GetUnletteredLinesAsync(
            IStorageBroker storage,
            string? companyId,
            string accountCode)
        {
            var lines = LoadCandidates(storage, companyId, accountCode)
                .Select(c => new LettrageLineDto
                {
                    LineId = c.Line.Id,
                    AccountingEntryId = c.Entry.Id,
                    EntryNumber = c.Entry.EntryNumber,
                    EntryDate = c.Entry.EntryDate,
                    Description = c.Entry.Description,
                    JournalType = c.Entry.JournalType,
                    LineNumber = c.Line.LineNumber,
                    AccountCode = c.Line.AccountCode,
                    AccountLabel = c.Line.AccountLabel,
                    Debit = c.Line.Debit,
                    Credit = c.Line.Credit
                })
                .ToList();
            return Task.FromResult(lines);
        }

        /// <summary>
        /// Lettrage automatique. Si accountCode est null : traite les comptes clients ET fournisseurs
        /// des paramètres de la société (dédoublonnés). Pour chaque compte :
        /// stratégie 1 (référence exacte facture ↔ règlements liés), puis stratégie 2 (montant FIFO).
        /// </summary>
        public static async Task<List<LettrageAccountSummaryDto>> AutomaticAsync(
            IStorageBroker storage,
            INumberingSequenceService numbering,
            string? companyId,
            string? accountCode,
            string? user)
        {
            var accounts = new List<string>();
            if (!string.IsNullOrWhiteSpace(accountCode))
            {
                accounts.Add(accountCode.Trim());
            }
            else
            {
                var settings = await AccountingEntryResolver.ResolveSettingsAsync(storage, companyId);
                foreach (var code in new[] { settings.CustomerAccountCode, settings.SupplierAccountCode })
                {
                    if (!string.IsNullOrWhiteSpace(code) && !accounts.Contains(code.Trim()))
                        accounts.Add(code.Trim());
                }
            }

            var summaries = new List<LettrageAccountSummaryDto>();
            foreach (var account in accounts)
                summaries.Add(await ProcessAccountAutomaticAsync(storage, numbering, companyId, account, user));
            return summaries;
        }

        /// <summary>
        /// Lettrage manuel d'une sélection de lignes : même compte, non lettrées, équilibrées,
        /// écritures comptabilisées/validées, période du jour ouverte. Retourne le code LET- attribué.
        /// </summary>
        public static async Task<(string? Code, string? Error)> ManualAsync(
            IStorageBroker storage,
            INumberingSequenceService numbering,
            string? companyId,
            IReadOnlyCollection<int>? lineIds,
            string? user)
        {
            if (lineIds == null || lineIds.Count < 2)
                return (null, "Au moins deux lignes sont requises pour un lettrage manuel.");

            var ids = lineIds.Distinct().ToList();
            var rows = LoadRows(storage, companyId)
                .Where(c => ids.Contains(c.Line.Id))
                .ToList();
            if (rows.Count != ids.Count)
                return (null, "Certaines lignes sont introuvables pour cette société.");

            if (rows.Select(c => c.Line.AccountCode).Distinct().Count() > 1)
                return (null, "Toutes les lignes doivent appartenir au même compte comptable.");

            if (rows.Any(c => c.Line.LettrageCode != null))
                return (null, "Une ou plusieurs lignes sont déjà lettrées.");

            if (rows.Any(c => c.Entry.Status != "Posted" && c.Entry.Status != "Validated"))
                return (null, "Seules les lignes d'écritures comptabilisées (Posted) ou validées peuvent être lettrées.");

            var totalDebit = rows.Sum(c => c.Line.Debit);
            var totalCredit = rows.Sum(c => c.Line.Credit);
            if (Math.Abs(totalDebit - totalCredit) > 0.01m)
                return (null, $"Le groupe n'est pas équilibré : débit {totalDebit:0.##} ≠ crédit {totalCredit:0.##}.");

            var periodError = await RejectIfCurrentPeriodClosedAsync(storage, companyId);
            if (periodError != null) return (null, periodError);

            var code = await numbering.GetNextNumberAsync("Lettering", companyId);
            await ApplyCodeAsync(storage, rows, code, user);
            return (code, null);
        }

        /// <summary>Délettrage : efface LettrageCode/LettrageDate des lignes portant ce code (période du jour ouverte requise).</summary>
        public static async Task<(int Count, string? Error)> DeletterAsync(
            IStorageBroker storage,
            string? companyId,
            string? code,
            string? user)
        {
            if (string.IsNullOrWhiteSpace(code))
                return (0, "Code de lettrage requis.");

            var rows = LoadRows(storage, companyId)
                .Where(c => c.Line.LettrageCode == code)
                .ToList();
            if (rows.Count == 0)
                return (0, $"Aucune ligne lettrée avec le code {code}.");

            var periodError = await RejectIfCurrentPeriodClosedAsync(storage, companyId);
            if (periodError != null) return (0, periodError);

            foreach (var row in rows)
            {
                row.Line.LettrageCode = null;
                row.Line.LettrageDate = null;
                Touch(row.Line, user);
                await storage.UpdateAccountingEntryLineAsync(row.Line);
            }
            return (rows.Count, null);
        }

        /// <summary>Groupes de lettrage existants (code, date, compte, nb lignes, totaux), du plus récent au plus ancien.</summary>
        public static Task<List<LettrageGroupDto>> GetLetteringGroupsAsync(
            IStorageBroker storage,
            string? companyId,
            string? accountCode)
        {
            var rows = LoadRows(storage, companyId)
                .Where(c => c.Line.LettrageCode != null);
            if (!string.IsNullOrWhiteSpace(accountCode))
            {
                var code = accountCode.Trim();
                rows = rows.Where(c => c.Line.AccountCode == code);
            }

            var groups = rows
                .GroupBy(c => new { c.Line.LettrageCode, c.Line.AccountCode })
                .Select(g => new LettrageGroupDto
                {
                    Code = g.Key.LettrageCode!,
                    Date = g.Max(c => c.Line.LettrageDate),
                    AccountCode = g.Key.AccountCode,
                    LineCount = g.Count(),
                    TotalDebit = g.Sum(c => c.Line.Debit),
                    TotalCredit = g.Sum(c => c.Line.Credit)
                })
                .OrderByDescending(g => g.Date)
                .ThenBy(g => g.Code)
                .ToList();
            return Task.FromResult(groups);
        }

        // --- Stratégies de lettrage automatique ---

        private static async Task<LettrageAccountSummaryDto> ProcessAccountAutomaticAsync(
            IStorageBroker storage,
            INumberingSequenceService numbering,
            string? companyId,
            string accountCode,
            string? user)
        {
            var summary = new LettrageAccountSummaryDto { AccountCode = accountCode };
            var candidates = LoadCandidates(storage, companyId, accountCode);

            await ApplyExactReferenceAsync(storage, numbering, companyId, candidates, summary, user);
            await ApplyFifoAmountAsync(storage, numbering, companyId, candidates, summary, user);
            return summary;
        }

        /// <summary>
        /// Stratégie 1 — référence exacte : une facture se lettre avec les règlements qui lui sont liés
        /// (Payment.SalesInvoiceId / SupplierPayment.SupplierInvoiceId), uniquement les lignes du compte
        /// courant et uniquement si le groupe est équilibré (Σdébit = Σcrédit ±0,01). Couvre les
        /// règlements partiels multiples : l'ensemble est lettré quand le total lettre exactement la facture.
        /// Sinon les lignes sont laissées à la stratégie 2.
        /// </summary>
        private static async Task ApplyExactReferenceAsync(
            IStorageBroker storage,
            INumberingSequenceService numbering,
            string? companyId,
            List<Candidate> candidates,
            LettrageAccountSummaryDto summary,
            string? user)
        {
            var byReference = candidates
                .GroupBy(c => (c.Entry.ReferenceType, c.Entry.ReferenceId))
                .ToDictionary(g => g.Key, g => g.ToList());

            // Clients : facture vente ↔ règlements liés (hors paiements annulés / remboursés).
            var paymentsByInvoice = storage.SelectAllPayments()
                .ForCompany(companyId)
                .Where(p => p.Status != "Cancelled" && p.Status != "Refunded")
                .AsEnumerable()
                .GroupBy(p => p.SalesInvoiceId);
            foreach (var invoicePayments in paymentsByInvoice)
            {
                if (!byReference.TryGetValue((AccountingLedger.RefSalesInvoice, invoicePayments.Key), out var invoiceRows))
                    continue;

                var group = new List<Candidate>(invoiceRows);
                foreach (var paymentId in invoicePayments.Select(p => p.Id).OrderBy(id => id))
                {
                    if (byReference.TryGetValue((AccountingLedger.RefPayment, paymentId), out var paymentRows))
                        group.AddRange(paymentRows);
                }

                await TryLetterGroupAsync(storage, numbering, companyId, group, summary, user);
            }

            // Fournisseurs : facture d'achat ↔ règlements liés (hors paiements annulés).
            var supplierPaymentsByInvoice = storage.SelectAllSupplierPayments()
                .ForCompany(companyId)
                .Where(p => p.Status != "Cancelled")
                .AsEnumerable()
                .GroupBy(p => p.SupplierInvoiceId);
            foreach (var invoicePayments in supplierPaymentsByInvoice)
            {
                if (!byReference.TryGetValue((AccountingLedger.RefSupplierInvoice, invoicePayments.Key), out var invoiceRows))
                    continue;

                var group = new List<Candidate>(invoiceRows);
                foreach (var paymentId in invoicePayments.Select(p => p.Id).OrderBy(id => id))
                {
                    if (byReference.TryGetValue((AccountingLedger.RefSupplierPayment, paymentId), out var paymentRows))
                        group.AddRange(paymentRows);
                }

                await TryLetterGroupAsync(storage, numbering, companyId, group, summary, user);
            }
        }

        /// <summary>
        /// Stratégie 2 — montant FIFO : sur les lignes restantes (triées par date), solde += débit − crédit ;
        /// quand |solde| ≤ 0,01 et que le groupe accumulé contient au moins une ligne au débit ET une au
        /// crédit, le groupe est lettré et l'accumulateur repart à zéro. Pas de lettrage partiel avec reliquat.
        /// </summary>
        private static async Task ApplyFifoAmountAsync(
            IStorageBroker storage,
            INumberingSequenceService numbering,
            string? companyId,
            List<Candidate> candidates,
            LettrageAccountSummaryDto summary,
            string? user)
        {
            var solde = 0m;
            var group = new List<Candidate>();
            foreach (var candidate in candidates.Where(c => c.Line.LettrageCode == null))
            {
                solde += candidate.Line.Debit - candidate.Line.Credit;
                group.Add(candidate);
                if (Math.Abs(solde) <= 0.01m
                    && group.Any(c => c.Line.Debit > 0)
                    && group.Any(c => c.Line.Credit > 0))
                {
                    await TryLetterGroupAsync(storage, numbering, companyId, group, summary, user);
                    solde = 0m;
                    group.Clear();
                }
            }
        }

        /// <summary>Lettre le groupe s'il est équilibré et mixte (débit + crédit) ; sinon le laisse non lettré.</summary>
        private static async Task TryLetterGroupAsync(
            IStorageBroker storage,
            INumberingSequenceService numbering,
            string? companyId,
            List<Candidate> group,
            LettrageAccountSummaryDto summary,
            string? user)
        {
            var rows = group.Where(c => c.Line.LettrageCode == null).ToList();
            if (rows.Count < 2) return;

            var totalDebit = rows.Sum(c => c.Line.Debit);
            var totalCredit = rows.Sum(c => c.Line.Credit);
            if (Math.Abs(totalDebit - totalCredit) > 0.01m) return;
            if (!rows.Any(c => c.Line.Debit > 0) || !rows.Any(c => c.Line.Credit > 0)) return;

            var code = await numbering.GetNextNumberAsync("Lettering", companyId);
            await ApplyCodeAsync(storage, rows, code, user);
            summary.GroupsCreated++;
            summary.Codes.Add(code);
        }

        // --- Helpers ---

        /// <summary>Toutes les lignes de la société avec leur écriture (LINQ synchrone volontaire : tests Moq en mémoire).</summary>
        private static List<Candidate> LoadRows(IStorageBroker storage, string? companyId) =>
            storage.SelectAllAccountingEntries()
                .ForCompany(companyId)
                .AsEnumerable()
                .SelectMany(e => e.Lines.Select(l => new Candidate { Entry = e, Line = l }))
                .ToList();

        /// <summary>Lignes non lettrées d'un compte, écritures Posted/Validated uniquement (Draft et Reversed exclues).</summary>
        private static List<Candidate> LoadCandidates(IStorageBroker storage, string? companyId, string accountCode) =>
            storage.SelectAllAccountingEntries()
                .ForCompany(companyId)
                .Where(e => e.Status == "Posted" || e.Status == "Validated")
                .AsEnumerable()
                .SelectMany(e => e.Lines.Select(l => new Candidate { Entry = e, Line = l }))
                .Where(c => c.Line.AccountCode == accountCode && c.Line.LettrageCode == null)
                .OrderBy(c => c.Entry.EntryDate)
                .ThenBy(c => c.Line.LineNumber)
                .ToList();

        private static async Task ApplyCodeAsync(
            IStorageBroker storage,
            List<Candidate> rows,
            string code,
            string? user)
        {
            var today = DateTime.UtcNow;
            foreach (var row in rows)
            {
                row.Line.LettrageCode = code;
                row.Line.LettrageDate = today;
                Touch(row.Line, user);
                await storage.UpdateAccountingEntryLineAsync(row.Line);
            }
        }

        private static void Touch(AccountingEntryLine line, string? user)
        {
            line.UpdatedAt = DateTime.UtcNow;
            line.UpdatedBy = SalesDocumentAudit.IsReadableActor(user) ? user!.Trim() : null;
        }

        /// <summary>
        /// Gate période pour le lettrage/délettrage (date du jour) : exercices structurés → la période
        /// du jour doit être ouverte ; sinon même règle legacy que le délettrage métier
        /// (DocumentLifecycleRules.RejectIfCannotUnletter, borne de fin d'exercice).
        /// </summary>
        private static async Task<string?> RejectIfCurrentPeriodClosedAsync(IStorageBroker storage, string? companyId)
        {
            if (string.IsNullOrWhiteSpace(companyId)) return null;

            var today = DateTime.UtcNow;
            var hasFiscalYears = storage.SelectAllFiscalYears().Any(f => f.CompanyId == companyId);
            if (hasFiscalYears)
            {
                var resolution = await AccountingEntryResolver.ResolvePeriodAsync(storage, companyId, today);
                return resolution.Error;
            }

            var company = await storage.SelectCompanyByIdAsync(companyId);
            return DocumentLifecycleRules.RejectIfCannotUnletter(company?.OpenFiscalPeriodEnd, today);
        }
    }
}
