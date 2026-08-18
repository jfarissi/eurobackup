using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Models.Entities;
using Backup.Web.Api.Server.Models.Entities.Accounting;
using Backup.Web.Api.Server.Services.Tenancy;

namespace Backup.Web.Api.Server.Services.Accounting
{
    /// <summary>
    /// Rapprochement bancaire : import CSV, matching 3 passes, pointage manuel, clôture de période.
    /// </summary>
    public static class BankReconciliationService
    {
        public const string StatusOpen = "Open";
        public const string StatusBalanced = "Balanced";
        public const string MatchReference = "Reference";
        public const string MatchAmountDate = "AmountDate";
        public const string MatchAmount = "Amount";
        public const string MatchManual = "Manual";

        public sealed class StatementLineDto
        {
            public int Id { get; set; }
            public DateTime OperationDate { get; set; }
            public string Label { get; set; } = string.Empty;
            public string? Reference { get; set; }
            public decimal Debit { get; set; }
            public decimal Credit { get; set; }
            public decimal? RunningBalance { get; set; }
            public bool IsMatched { get; set; }
            public string? MatchMethod { get; set; }
            public int? AccountingEntryId { get; set; }
            public int? AccountingEntryLineId { get; set; }
            public string? EntryNumber { get; set; }
        }

        public sealed class LedgerCandidateDto
        {
            public int LineId { get; set; }
            public int EntryId { get; set; }
            public string EntryNumber { get; set; } = string.Empty;
            public DateTime EntryDate { get; set; }
            public string Description { get; set; } = string.Empty;
            public decimal Debit { get; set; }
            public decimal Credit { get; set; }
        }

        public sealed class ReconciliationDto
        {
            public int Id { get; set; }
            public string AccountCode { get; set; } = string.Empty;
            public string? FileName { get; set; }
            public DateTime StatementDate { get; set; }
            public DateTime FromDate { get; set; }
            public DateTime ToDate { get; set; }
            public decimal StatementBalance { get; set; }
            public decimal BookBalance { get; set; }
            public decimal Difference { get; set; }
            public string Status { get; set; } = StatusOpen;
            public int LineCount { get; set; }
            public int MatchedCount { get; set; }
            public DateTime? CompletedAt { get; set; }
            public string? CompletedBy { get; set; }
            public List<StatementLineDto> Lines { get; set; } = new();
            public List<LedgerCandidateDto> UnmatchedLedger { get; set; } = new();
        }

        public sealed class MatchResultDto
        {
            public int Matched { get; set; }
            public int Remaining { get; set; }
            public ReconciliationDto? Reconciliation { get; set; }
        }

        public static List<ReconciliationDto> List(IStorageBroker storage, string? companyId) =>
            storage.SelectAllBankReconciliations()
                .ForCompany(companyId)
                .AsEnumerable()
                .OrderByDescending(r => r.StatementDate)
                .ThenByDescending(r => r.Id)
                .Select(r => ToSummary(r))
                .ToList();

        public static ReconciliationDto? Get(IStorageBroker storage, string? companyId, int id)
        {
            var rec = Find(storage, companyId, id);
            return rec == null ? null : ToDetail(storage, rec);
        }

        public static async Task<(ReconciliationDto? Dto, string? Error)> ImportAsync(
            IStorageBroker storage,
            string? companyId,
            string csvContent,
            string? fileName,
            string? accountCode,
            string? actor)
        {
            List<BankStatementCsvParser.ParsedLine> parsed;
            try
            {
                parsed = BankStatementImport.Parse(csvContent, fileName);
            }
            catch (InvalidOperationException ex)
            {
                return (null, ex.Message);
            }

            var settings = await AccountingEntryResolver.ResolveSettingsAsync(storage, companyId);
            var account = string.IsNullOrWhiteSpace(accountCode) ? settings.BankAccountCode : accountCode.Trim();
            var from = parsed.Min(l => l.OperationDate.Date);
            var to = parsed.Max(l => l.OperationDate.Date);
            var statementBalance = parsed.Last().RunningBalance
                ?? parsed.Sum(l => l.Credit - l.Debit);
            var bookBalance = BookBalance(storage, companyId, account, to);

            var rec = new BankReconciliation
            {
                AccountCode = account,
                FileName = string.IsNullOrWhiteSpace(fileName) ? null : fileName.Trim(),
                StatementDate = to,
                FromDate = from,
                ToDate = to,
                StatementBalance = Round(statementBalance),
                BookBalance = bookBalance,
                Status = StatusOpen,
                CompanyId = companyId,
                CreatedBy = actor,
                UpdatedBy = actor,
                Lines = parsed.Select(l => new BankStatementLine
                {
                    OperationDate = l.OperationDate.Date,
                    Label = l.Label,
                    Reference = l.Reference,
                    Debit = l.Debit,
                    Credit = l.Credit,
                    RunningBalance = l.RunningBalance
                }).ToList()
            };

            var saved = await storage.InsertBankReconciliationAsync(rec);
            return (ToDetail(storage, saved), null);
        }

        public static async Task<(MatchResultDto? Result, string? Error)> AutoMatchAsync(
            IStorageBroker storage,
            string? companyId,
            int id)
        {
            var rec = Find(storage, companyId, id);
            if (rec == null) return (null, "Rapprochement introuvable.");
            if (rec.Status == StatusBalanced) return (null, "Ce rapprochement est déjà clôturé.");

            var candidates = LoadUnmatchedLedger(storage, companyId, rec);
            var matched = 0;
            matched += RunPass(rec, candidates, MatchReference, 3650, requireReference: true);
            matched += RunPass(rec, candidates, MatchAmountDate, 3, requireReference: false);
            matched += RunPass(rec, candidates, MatchAmount, 5, requireReference: false);

            rec.UpdatedAt = DateTime.UtcNow;
            rec.BookBalance = BookBalance(storage, companyId, rec.AccountCode, rec.ToDate);
            await storage.UpdateBankReconciliationAsync(rec);

            return (new MatchResultDto
            {
                Matched = matched,
                Remaining = rec.Lines.Count(l => !l.IsMatched),
                Reconciliation = ToDetail(storage, rec)
            }, null);
        }

        public static async Task<(ReconciliationDto? Dto, string? Error)> ManualMatchAsync(
            IStorageBroker storage,
            string? companyId,
            int reconciliationId,
            int statementLineId,
            int accountingEntryLineId)
        {
            var rec = Find(storage, companyId, reconciliationId);
            if (rec == null) return (null, "Rapprochement introuvable.");
            if (rec.Status == StatusBalanced) return (null, "Ce rapprochement est déjà clôturé.");

            var line = rec.Lines.FirstOrDefault(l => l.Id == statementLineId);
            if (line == null) return (null, "Ligne de relevé introuvable.");
            if (line.IsMatched) return (null, "Cette ligne est déjà pointée.");

            var candidate = LoadUnmatchedLedger(storage, companyId, rec)
                .FirstOrDefault(c => c.Line.Id == accountingEntryLineId);
            if (candidate == null) return (null, "Écriture introuvable ou déjà pointée.");
            if (!AmountMatches(line, candidate.Line))
                return (null, "Les montants (débit/crédit) ne correspondent pas.");

            Pointer(line, candidate, MatchManual);
            rec.UpdatedAt = DateTime.UtcNow;
            await storage.UpdateBankReconciliationAsync(rec);
            return (ToDetail(storage, rec), null);
        }

        public static async Task<(ReconciliationDto? Dto, string? Error)> UnmatchAsync(
            IStorageBroker storage,
            string? companyId,
            int reconciliationId,
            int statementLineId)
        {
            var rec = Find(storage, companyId, reconciliationId);
            if (rec == null) return (null, "Rapprochement introuvable.");
            if (rec.Status == StatusBalanced) return (null, "Ce rapprochement est déjà clôturé.");

            var line = rec.Lines.FirstOrDefault(l => l.Id == statementLineId);
            if (line == null) return (null, "Ligne de relevé introuvable.");

            line.IsMatched = false;
            line.MatchMethod = null;
            line.AccountingEntryId = null;
            line.AccountingEntryLineId = null;
            rec.UpdatedAt = DateTime.UtcNow;
            await storage.UpdateBankReconciliationAsync(rec);
            return (ToDetail(storage, rec), null);
        }

        public static async Task<(ReconciliationDto? Dto, string? Error)> CompleteAsync(
            IStorageBroker storage,
            string? companyId,
            int id,
            string? actor)
        {
            var rec = Find(storage, companyId, id);
            if (rec == null) return (null, "Rapprochement introuvable.");
            if (rec.Status == StatusBalanced) return (null, "Ce rapprochement est déjà clôturé.");
            if (rec.Lines.Any(l => !l.IsMatched))
                return (null, "Toutes les lignes du relevé doivent être pointées avant clôture.");

            rec.Status = StatusBalanced;
            rec.CompletedAt = DateTime.UtcNow;
            rec.CompletedBy = actor;
            rec.BookBalance = BookBalance(storage, companyId, rec.AccountCode, rec.ToDate);
            rec.UpdatedAt = DateTime.UtcNow;
            rec.UpdatedBy = actor;
            await storage.UpdateBankReconciliationAsync(rec);

            foreach (var period in OverlappingPeriods(storage, companyId, rec.FromDate, rec.ToDate))
            {
                if (period.IsBankReconciled) continue;
                period.IsBankReconciled = true;
                period.UpdatedAt = DateTime.UtcNow;
                period.UpdatedBy = actor;
                await storage.UpdateFiscalPeriodAsync(period);
            }

            return (ToDetail(storage, rec), null);
        }

        private static int RunPass(
            BankReconciliation rec,
            List<LedgerHit> candidates,
            string method,
            int dayWindow,
            bool requireReference)
        {
            var count = 0;
            foreach (var line in rec.Lines.Where(l => !l.IsMatched).ToList())
            {
                if (requireReference && string.IsNullOrWhiteSpace(line.Reference)
                    && string.IsNullOrWhiteSpace(line.Label))
                    continue;

                var matches = candidates.Where(c =>
                    AmountMatches(line, c.Line)
                    && Math.Abs((c.Entry.EntryDate.Date - line.OperationDate.Date).Days) <= dayWindow
                    && (!requireReference || ReferenceMatches(line, c.Entry)))
                    .ToList();
                if (matches.Count != 1) continue;

                Pointer(line, matches[0], method);
                candidates.Remove(matches[0]);
                count++;
            }
            return count;
        }

        private static bool ReferenceMatches(BankStatementLine line, AccountingEntry entry)
        {
            var hay = $"{entry.EntryNumber} {entry.Description}";
            if (!string.IsNullOrWhiteSpace(line.Reference)
                && line.Reference.Trim().Length >= 3
                && hay.Contains(line.Reference.Trim(), StringComparison.OrdinalIgnoreCase))
                return true;
            if (!string.IsNullOrWhiteSpace(entry.EntryNumber)
                && !string.IsNullOrWhiteSpace(line.Label)
                && line.Label.Contains(entry.EntryNumber, StringComparison.OrdinalIgnoreCase))
                return true;
            return false;
        }

        private static bool AmountMatches(BankStatementLine statement, AccountingEntryLine gl)
        {
            if (statement.Credit > 0)
                return Math.Abs(statement.Credit - gl.Debit) < 0.01m;
            if (statement.Debit > 0)
                return Math.Abs(statement.Debit - gl.Credit) < 0.01m;
            return false;
        }

        private static void Pointer(BankStatementLine line, LedgerHit hit, string method)
        {
            line.IsMatched = true;
            line.MatchMethod = method;
            line.AccountingEntryId = hit.Entry.Id;
            line.AccountingEntryLineId = hit.Line.Id;
        }

        private sealed class LedgerHit
        {
            public AccountingEntry Entry { get; set; } = null!;
            public AccountingEntryLine Line { get; set; } = null!;
        }

        private static List<LedgerHit> LoadUnmatchedLedger(
            IStorageBroker storage,
            string? companyId,
            BankReconciliation rec)
        {
            var taken = TakenLineIds(storage, companyId);
            return storage.SelectAllAccountingEntries()
                .ForCompany(companyId)
                .Where(e => e.Status == "Posted" || e.Status == "Validated")
                .AsEnumerable()
                .SelectMany(e => (e.Lines ?? new List<AccountingEntryLine>())
                    .Where(l => string.Equals(l.AccountCode, rec.AccountCode, StringComparison.Ordinal)
                        && (l.Debit > 0 || l.Credit > 0)
                        && !taken.Contains(l.Id))
                    .Select(l => new LedgerHit { Entry = e, Line = l }))
                .ToList();
        }

        private static HashSet<int> TakenLineIds(IStorageBroker storage, string? companyId) =>
            storage.SelectAllBankReconciliations()
                .ForCompany(companyId)
                .AsEnumerable()
                .SelectMany(r => r.Lines ?? new List<BankStatementLine>())
                .Where(l => l.IsMatched && l.AccountingEntryLineId != null)
                .Select(l => l.AccountingEntryLineId!.Value)
                .ToHashSet();

        private static decimal BookBalance(IStorageBroker storage, string? companyId, string account, DateTime to) =>
            Round(storage.SelectAllAccountingEntries()
                .ForCompany(companyId)
                .Where(e => e.Status == "Posted" || e.Status == "Validated")
                .AsEnumerable()
                .Where(e => e.EntryDate.Date <= to)
                .SelectMany(e => e.Lines ?? new List<AccountingEntryLine>())
                .Where(l => string.Equals(l.AccountCode, account, StringComparison.Ordinal))
                .Sum(l => l.Debit - l.Credit));

        private static IEnumerable<FiscalPeriod> OverlappingPeriods(
            IStorageBroker storage, string? companyId, DateTime from, DateTime to) =>
            storage.SelectAllFiscalPeriods()
                .ForCompany(companyId)
                .AsEnumerable()
                .Where(p =>
                {
                    var start = new DateTime(p.Year, p.Month, 1);
                    var end = start.AddMonths(1).AddDays(-1);
                    return start <= to && end >= from;
                });

        private static BankReconciliation? Find(IStorageBroker storage, string? companyId, int id) =>
            storage.SelectAllBankReconciliations()
                .ForCompany(companyId)
                .FirstOrDefault(r => r.Id == id);

        private static ReconciliationDto ToSummary(BankReconciliation rec) => new()
        {
            Id = rec.Id,
            AccountCode = rec.AccountCode,
            FileName = rec.FileName,
            StatementDate = rec.StatementDate,
            FromDate = rec.FromDate,
            ToDate = rec.ToDate,
            StatementBalance = rec.StatementBalance,
            BookBalance = rec.BookBalance,
            Difference = Round(rec.StatementBalance - rec.BookBalance),
            Status = rec.Status,
            LineCount = rec.Lines?.Count ?? 0,
            MatchedCount = rec.Lines?.Count(l => l.IsMatched) ?? 0,
            CompletedAt = rec.CompletedAt,
            CompletedBy = rec.CompletedBy
        };

        private static ReconciliationDto ToDetail(IStorageBroker storage, BankReconciliation rec)
        {
            var dto = ToSummary(rec);
            var entries = storage.SelectAllAccountingEntries()
                .ForCompany(rec.CompanyId)
                .AsEnumerable()
                .ToDictionary(e => e.Id);
            dto.Lines = (rec.Lines ?? new List<BankStatementLine>())
                .OrderBy(l => l.OperationDate)
                .ThenBy(l => l.Id)
                .Select(l => new StatementLineDto
                {
                    Id = l.Id,
                    OperationDate = l.OperationDate,
                    Label = l.Label,
                    Reference = l.Reference,
                    Debit = l.Debit,
                    Credit = l.Credit,
                    RunningBalance = l.RunningBalance,
                    IsMatched = l.IsMatched,
                    MatchMethod = l.MatchMethod,
                    AccountingEntryId = l.AccountingEntryId,
                    AccountingEntryLineId = l.AccountingEntryLineId,
                    EntryNumber = l.AccountingEntryId != null && entries.TryGetValue(l.AccountingEntryId.Value, out var e)
                        ? e.EntryNumber
                        : null
                })
                .ToList();
            dto.UnmatchedLedger = LoadUnmatchedLedger(storage, rec.CompanyId, rec)
                .OrderBy(c => c.Entry.EntryDate)
                .ThenBy(c => c.Line.LineNumber)
                .Select(c => new LedgerCandidateDto
                {
                    LineId = c.Line.Id,
                    EntryId = c.Entry.Id,
                    EntryNumber = c.Entry.EntryNumber,
                    EntryDate = c.Entry.EntryDate,
                    Description = c.Entry.Description,
                    Debit = c.Line.Debit,
                    Credit = c.Line.Credit
                })
                .ToList();
            return dto;
        }

        private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
    }
}
