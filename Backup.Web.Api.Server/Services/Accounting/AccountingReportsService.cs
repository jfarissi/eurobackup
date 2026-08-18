using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Models.Entities;
using Backup.Web.Api.Server.Services.Tenancy;

namespace Backup.Web.Api.Server.Services.Accounting
{
    /// <summary>
    /// Rapports de lecture : balance des comptes et grand livre.
    /// Uniquement les écritures Posted / Validated (brouillons et extournes exclus).
    /// </summary>
    public static class AccountingReportsService
    {
        public sealed class BalanceRowDto
        {
            public string AccountCode { get; set; } = string.Empty;
            public string AccountLabel { get; set; } = string.Empty;
            public decimal OpeningDebit { get; set; }
            public decimal OpeningCredit { get; set; }
            public decimal PeriodDebit { get; set; }
            public decimal PeriodCredit { get; set; }
            public decimal ClosingDebit { get; set; }
            public decimal ClosingCredit { get; set; }
        }

        public sealed class BalanceReportDto
        {
            public DateTime? From { get; set; }
            public DateTime? To { get; set; }
            public List<BalanceRowDto> Rows { get; set; } = new();
            public decimal TotalOpeningDebit { get; set; }
            public decimal TotalOpeningCredit { get; set; }
            public decimal TotalPeriodDebit { get; set; }
            public decimal TotalPeriodCredit { get; set; }
            public decimal TotalClosingDebit { get; set; }
            public decimal TotalClosingCredit { get; set; }
        }

        public sealed class LedgerMovementDto
        {
            public DateTime EntryDate { get; set; }
            public string EntryNumber { get; set; } = string.Empty;
            public string JournalType { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public int LineNumber { get; set; }
            public decimal Debit { get; set; }
            public decimal Credit { get; set; }
            public decimal RunningBalance { get; set; }
        }

        public sealed class LedgerReportDto
        {
            public string AccountCode { get; set; } = string.Empty;
            public string AccountLabel { get; set; } = string.Empty;
            public DateTime? From { get; set; }
            public DateTime? To { get; set; }
            public decimal OpeningDebit { get; set; }
            public decimal OpeningCredit { get; set; }
            public decimal OpeningBalance { get; set; }
            public List<LedgerMovementDto> Movements { get; set; } = new();
            public decimal PeriodDebit { get; set; }
            public decimal PeriodCredit { get; set; }
            public decimal ClosingDebit { get; set; }
            public decimal ClosingCredit { get; set; }
            public decimal ClosingBalance { get; set; }
        }

        private sealed class AccountAccumulator
        {
            public string AccountCode { get; set; } = string.Empty;
            public string AccountLabel { get; set; } = string.Empty;
            public decimal OpeningNet { get; set; }
            public decimal PeriodDebit { get; set; }
            public decimal PeriodCredit { get; set; }
        }

        /// <summary>Balance des comptes : ouverture (avant from), mouvements [from, to], clôture.</summary>
        public static Task<BalanceReportDto> GetBalanceAsync(
            IStorageBroker storage,
            string? companyId,
            DateTime? from,
            DateTime? to)
        {
            var fromDate = from?.Date;
            var toDate = to?.Date;
            var labels = LoadAccountLabels(storage, companyId);
            var accumulators = new Dictionary<string, AccountAccumulator>(StringComparer.Ordinal);

            foreach (var (entry, line) in LoadBookedLines(storage, companyId, toDate))
            {
                if (string.IsNullOrWhiteSpace(line.AccountCode)) continue;

                var bucket = Classify(entry.EntryDate, fromDate, toDate);
                if (bucket == MovementBucket.Excluded) continue;

                var acc = GetOrAdd(accumulators, line, labels);
                if (bucket == MovementBucket.Opening)
                    acc.OpeningNet += line.Debit - line.Credit;
                else
                {
                    acc.PeriodDebit += line.Debit;
                    acc.PeriodCredit += line.Credit;
                }
            }

            var rows = accumulators.Values
                .OrderBy(a => a.AccountCode, StringComparer.Ordinal)
                .Select(a =>
                {
                    var (openD, openC) = ToSides(a.OpeningNet);
                    var closeNet = a.OpeningNet + a.PeriodDebit - a.PeriodCredit;
                    var (closeD, closeC) = ToSides(closeNet);
                    return new BalanceRowDto
                    {
                        AccountCode = a.AccountCode,
                        AccountLabel = a.AccountLabel,
                        OpeningDebit = openD,
                        OpeningCredit = openC,
                        PeriodDebit = a.PeriodDebit,
                        PeriodCredit = a.PeriodCredit,
                        ClosingDebit = closeD,
                        ClosingCredit = closeC
                    };
                })
                .ToList();

            var report = new BalanceReportDto
            {
                From = fromDate,
                To = toDate,
                Rows = rows,
                TotalOpeningDebit = rows.Sum(r => r.OpeningDebit),
                TotalOpeningCredit = rows.Sum(r => r.OpeningCredit),
                TotalPeriodDebit = rows.Sum(r => r.PeriodDebit),
                TotalPeriodCredit = rows.Sum(r => r.PeriodCredit),
                TotalClosingDebit = rows.Sum(r => r.ClosingDebit),
                TotalClosingCredit = rows.Sum(r => r.ClosingCredit)
            };
            return Task.FromResult(report);
        }

        /// <summary>Grand livre d'un compte : solde d'ouverture, mouvements de la période, solde de clôture.</summary>
        public static Task<LedgerReportDto> GetGeneralLedgerAsync(
            IStorageBroker storage,
            string? companyId,
            string accountCode,
            DateTime? from,
            DateTime? to)
        {
            var code = accountCode.Trim();
            var fromDate = from?.Date;
            var toDate = to?.Date;
            var labels = LoadAccountLabels(storage, companyId);

            decimal openingNet = 0m;
            decimal periodDebit = 0m;
            decimal periodCredit = 0m;
            string label = labels.TryGetValue(code, out var chartLabel) ? chartLabel : string.Empty;
            var periodLines = new List<(AccountingEntry Entry, AccountingEntryLine Line)>();

            foreach (var (entry, line) in LoadBookedLines(storage, companyId, toDate))
            {
                if (!string.Equals(line.AccountCode, code, StringComparison.Ordinal)) continue;

                if (string.IsNullOrEmpty(label) && !string.IsNullOrWhiteSpace(line.AccountLabel))
                    label = line.AccountLabel;

                var bucket = Classify(entry.EntryDate, fromDate, toDate);
                if (bucket == MovementBucket.Excluded) continue;

                if (bucket == MovementBucket.Opening)
                    openingNet += line.Debit - line.Credit;
                else
                {
                    periodDebit += line.Debit;
                    periodCredit += line.Credit;
                    periodLines.Add((entry, line));
                }
            }

            var (openD, openC) = ToSides(openingNet);
            var running = openingNet;
            var movements = periodLines
                .OrderBy(x => x.Entry.EntryDate)
                .ThenBy(x => x.Entry.EntryNumber, StringComparer.Ordinal)
                .ThenBy(x => x.Line.LineNumber)
                .Select(x =>
                {
                    running += x.Line.Debit - x.Line.Credit;
                    return new LedgerMovementDto
                    {
                        EntryDate = x.Entry.EntryDate,
                        EntryNumber = x.Entry.EntryNumber,
                        JournalType = x.Entry.JournalType,
                        Description = x.Entry.Description,
                        LineNumber = x.Line.LineNumber,
                        Debit = x.Line.Debit,
                        Credit = x.Line.Credit,
                        RunningBalance = running
                    };
                })
                .ToList();

            var closeNet = openingNet + periodDebit - periodCredit;
            var (closeD, closeC) = ToSides(closeNet);

            var report = new LedgerReportDto
            {
                AccountCode = code,
                AccountLabel = label,
                From = fromDate,
                To = toDate,
                OpeningDebit = openD,
                OpeningCredit = openC,
                OpeningBalance = openingNet,
                Movements = movements,
                PeriodDebit = periodDebit,
                PeriodCredit = periodCredit,
                ClosingDebit = closeD,
                ClosingCredit = closeC,
                ClosingBalance = closeNet
            };
            return Task.FromResult(report);
        }

        private enum MovementBucket { Opening, Period, Excluded }

        private static MovementBucket Classify(DateTime entryDate, DateTime? fromDate, DateTime? toDate)
        {
            var date = entryDate.Date;
            if (toDate != null && date > toDate.Value) return MovementBucket.Excluded;
            if (fromDate != null && date < fromDate.Value) return MovementBucket.Opening;
            return MovementBucket.Period;
        }

        private static (decimal Debit, decimal Credit) ToSides(decimal net) =>
            net >= 0m ? (net, 0m) : (0m, -net);

        private static AccountAccumulator GetOrAdd(
            Dictionary<string, AccountAccumulator> map,
            AccountingEntryLine line,
            IReadOnlyDictionary<string, string> labels)
        {
            if (map.TryGetValue(line.AccountCode, out var existing))
            {
                if (string.IsNullOrEmpty(existing.AccountLabel))
                    existing.AccountLabel = ResolveLabel(line, labels);
                return existing;
            }

            var created = new AccountAccumulator
            {
                AccountCode = line.AccountCode,
                AccountLabel = ResolveLabel(line, labels)
            };
            map[line.AccountCode] = created;
            return created;
        }

        private static string ResolveLabel(AccountingEntryLine line, IReadOnlyDictionary<string, string> labels)
        {
            if (labels.TryGetValue(line.AccountCode, out var chartLabel) && !string.IsNullOrWhiteSpace(chartLabel))
                return chartLabel;
            return line.AccountLabel ?? string.Empty;
        }

        private static Dictionary<string, string> LoadAccountLabels(IStorageBroker storage, string? companyId) =>
            storage.SelectAllChartOfAccounts()
                .ForCompany(companyId)
                .AsEnumerable()
                .GroupBy(a => a.AccountNumber)
                .ToDictionary(g => g.Key, g => g.First().Label, StringComparer.Ordinal);

        /// <summary>
        /// Écritures Posted/Validated jusqu'à <paramref name="toDate"/> inclus (ou toutes si null).
        /// </summary>
        private static IEnumerable<(AccountingEntry Entry, AccountingEntryLine Line)> LoadBookedLines(
            IStorageBroker storage,
            string? companyId,
            DateTime? toDate)
        {
            return storage.SelectAllAccountingEntries()
                .ForCompany(companyId)
                .Where(e => e.Status == "Posted" || e.Status == "Validated")
                .AsEnumerable()
                .Where(e => toDate == null || e.EntryDate.Date <= toDate.Value)
                .SelectMany(e => (e.Lines ?? new List<AccountingEntryLine>())
                    .Select(l => (Entry: e, Line: l)));
        }
    }
}
