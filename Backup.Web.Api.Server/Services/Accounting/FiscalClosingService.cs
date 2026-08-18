using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Models.Entities;
using Backup.Web.Api.Server.Models.Entities.Accounting;
using Backup.Web.Api.Server.Services.Numbering;
using Backup.Web.Api.Server.Services.Sales;
using Backup.Web.Api.Server.Services.Tenancy;

namespace Backup.Web.Api.Server.Services.Accounting
{
    /// <summary>
    /// Clôture mensuelle (verrouillage) et annuelle (solde 6/7 → résultat, à-nouveaux bilan, exercice suivant).
    /// Rapprochement bancaire non implémenté : avertissement, pas de blocage.
    /// </summary>
    public static class FiscalClosingService
    {
        public const string RefYearClose = "FiscalYearClose";
        public const string RefCarryForward = "FiscalYearCarryForward";
        public const string SeverityBlocking = "Blocking";
        public const string SeverityWarning = "Warning";

        public sealed class CheckItemDto
        {
            public string Code { get; set; } = string.Empty;
            public string Severity { get; set; } = SeverityWarning;
            public string Message { get; set; } = string.Empty;
        }

        public sealed class ClosingPreviewDto
        {
            public int FiscalYearId { get; set; }
            public string YearName { get; set; } = string.Empty;
            public string Status { get; set; } = "Open";
            public DateTime StartDate { get; set; }
            public DateTime EndDate { get; set; }
            public bool CanClose { get; set; }
            public decimal Profit { get; set; }
            public string ResultAccountCode { get; set; } = string.Empty;
            public int ResultAccountsToClose { get; set; }
            public int BilanAccountsToCarry { get; set; }
            public int? NextYearId { get; set; }
            public string? NextYearName { get; set; }
            public List<CheckItemDto> Checks { get; set; } = new();
        }

        public sealed class CloseResultDto
        {
            public bool Success { get; set; }
            public string? Error { get; set; }
            public string? CloseEntryNumber { get; set; }
            public string? CarryForwardEntryNumber { get; set; }
            public int? NextYearId { get; set; }
            public ClosingPreviewDto? Preview { get; set; }
        }

        public static ClosingPreviewDto PreviewYear(IStorageBroker storage, string? companyId, int fiscalYearId)
        {
            var year = FindYear(storage, companyId, fiscalYearId);
            if (year == null)
            {
                return new ClosingPreviewDto
                {
                    FiscalYearId = fiscalYearId,
                    CanClose = false,
                    Checks = { Blocking("E000", "Exercice introuvable.") }
                };
            }

            var start = year.StartDate.Date;
            var end = year.EndDate.Date;
            var booked = LoadBooked(storage, companyId, start, end);
            var drafts = LoadDrafts(storage, companyId, start, end);
            var balances = SumBalances(booked);
            var profit = ProfitOf(balances);
            var resultAccount = ResolveResultAccount(storage, companyId, profit);
            var resultCount = balances.Count(kv => IsResultClass(kv.Key) && kv.Value != 0m);
            var bilanCount = balances.Count(kv => IsBilanClass(kv.Key) && kv.Value != 0m);
            var next = FindNextYear(storage, companyId, end);
            var checks = new List<CheckItemDto>();

            if (string.Equals(year.Status, "Closed", StringComparison.OrdinalIgnoreCase))
                checks.Add(Blocking("E000", "Cet exercice est déjà clôturé."));

            if (drafts.Count > 0)
                checks.Add(Blocking("E005", $"{drafts.Count} brouillon(s) restent dans l'exercice : comptabilisez-les ou supprimez-les."));

            var totalDebit = booked.SelectMany(e => e.Lines).Sum(l => l.Debit);
            var totalCredit = booked.SelectMany(e => e.Lines).Sum(l => l.Credit);
            if (Math.Abs(totalDebit - totalCredit) > 0.01m)
                checks.Add(Blocking("E002", $"Les écritures de l'exercice ne sont pas équilibrées : débit {totalDebit:0.##} ≠ crédit {totalCredit:0.##}."));

            var unlocked = (year.Periods ?? new List<FiscalPeriod>()).Count(p => !p.IsLocked);
            if (unlocked > 0)
                checks.Add(Warning("E001", $"{unlocked} période(s) encore ouverte(s) : elles seront verrouillées à la clôture annuelle."));

            var unlettered = CountUnlettered(storage, companyId, booked);
            if (unlettered > 0)
                checks.Add(Warning("E004", $"{unlettered} ligne(s) lettrable(s) non lettrée(s)."));

            var undeclaredVat = (year.Periods ?? new List<FiscalPeriod>()).Count(p => !p.IsVatDeclared);
            if (undeclaredVat > 0)
                checks.Add(Warning("E006", $"{undeclaredVat} période(s) sans déclaration TVA."));

            var unreconciled = (year.Periods ?? new List<FiscalPeriod>()).Count(p => !p.IsBankReconciled);
            if (unreconciled > 0)
                checks.Add(Warning("E007", $"{unreconciled} période(s) sans rapprochement bancaire."));

            return new ClosingPreviewDto
            {
                FiscalYearId = year.Id,
                YearName = year.Name,
                Status = year.Status,
                StartDate = start,
                EndDate = end,
                CanClose = checks.All(c => c.Severity != SeverityBlocking),
                Profit = profit,
                ResultAccountCode = resultAccount,
                ResultAccountsToClose = resultCount,
                BilanAccountsToCarry = bilanCount,
                NextYearId = next?.Id,
                NextYearName = next?.Name,
                Checks = checks
            };
        }

        public static async Task<(FiscalPeriod? Period, string? Error)> ClosePeriodAsync(
            IStorageBroker storage,
            string? companyId,
            int periodId,
            string? actor)
        {
            var period = storage.SelectAllFiscalPeriods()
                .ForCompany(companyId)
                .FirstOrDefault(p => p.Id == periodId);
            if (period == null) return (null, "Période introuvable.");
            if (period.IsLocked) return (null, "Cette période est déjà verrouillée.");

            var from = new DateTime(period.Year, period.Month, 1);
            var to = from.AddMonths(1).AddDays(-1);
            var drafts = LoadDrafts(storage, companyId, from, to);
            if (drafts.Count > 0)
                return (null, $"{drafts.Count} brouillon(s) dans la période : comptabilisez-les ou supprimez-les avant clôture.");

            period.IsLocked = true;
            period.UpdatedAt = DateTime.UtcNow;
            period.UpdatedBy = actor;
            var updated = await storage.UpdateFiscalPeriodAsync(period);
            return (updated, null);
        }

        public static async Task<CloseResultDto> CloseYearAsync(
            IStorageBroker storage,
            INumberingSequenceService numbering,
            string? companyId,
            int fiscalYearId,
            string? actor)
        {
            var preview = PreviewYear(storage, companyId, fiscalYearId);
            if (!preview.CanClose)
            {
                return new CloseResultDto
                {
                    Success = false,
                    Error = preview.Checks.FirstOrDefault(c => c.Severity == SeverityBlocking)?.Message
                        ?? "La clôture est bloquée.",
                    Preview = preview
                };
            }

            var year = FindYear(storage, companyId, fiscalYearId)!;
            var alreadyClosed = storage.SelectAllAccountingEntries()
                .ForCompany(companyId)
                .Any(e => e.ReferenceType == RefYearClose && e.ReferenceId == year.Id
                    && (e.Status == "Posted" || e.Status == "Validated"));
            if (alreadyClosed)
                return new CloseResultDto { Success = false, Error = "Une pièce de clôture existe déjà pour cet exercice.", Preview = preview };

            var nextYear = await EnsureNextYearAsync(storage, companyId, year, actor);
            var booked = LoadBooked(storage, companyId, year.StartDate.Date, year.EndDate.Date);
            var balances = SumBalances(booked);
            var labels = LoadLabels(storage, companyId);
            var lastPeriod = (year.Periods ?? new List<FiscalPeriod>())
                .OrderBy(p => p.Year).ThenBy(p => p.Month)
                .LastOrDefault();
            var firstNextPeriod = (nextYear.Periods ?? new List<FiscalPeriod>())
                .OrderBy(p => p.Year).ThenBy(p => p.Month)
                .FirstOrDefault();

            string? closeNumber = null;
            var odLines = BuildResultClosingLines(balances, preview.ResultAccountCode, labels);
            if (odLines.Count >= 2)
            {
                var od = await InsertGeneratedEntryAsync(
                    storage, numbering, companyId, actor,
                    year.EndDate.Date,
                    lastPeriod?.Id,
                    "OD",
                    RefYearClose,
                    year.Id,
                    $"Clôture — solde des comptes de résultat ({year.Name})",
                    odLines);
                closeNumber = od.EntryNumber;
                foreach (var line in od.Lines)
                    balances[line.AccountCode] = balances.GetValueOrDefault(line.AccountCode) + line.Debit - line.Credit;
            }

            string? anNumber = null;
            var anLines = BuildCarryForwardLines(balances, labels);
            if (anLines.Count >= 2)
            {
                var an = await InsertGeneratedEntryAsync(
                    storage, numbering, companyId, actor,
                    year.EndDate.Date.AddDays(1),
                    firstNextPeriod?.Id,
                    "AN",
                    RefCarryForward,
                    year.Id,
                    $"À-nouveaux — report des soldes bilan ({year.Name})",
                    anLines);
                anNumber = an.EntryNumber;
            }

            foreach (var period in year.Periods ?? new List<FiscalPeriod>())
            {
                if (period.IsLocked) continue;
                period.IsLocked = true;
                period.UpdatedAt = DateTime.UtcNow;
                period.UpdatedBy = actor;
                await storage.UpdateFiscalPeriodAsync(period);
            }

            year.Status = "Closed";
            year.UpdatedAt = DateTime.UtcNow;
            year.UpdatedBy = actor;
            await storage.UpdateFiscalYearAsync(year);

            return new CloseResultDto
            {
                Success = true,
                CloseEntryNumber = closeNumber,
                CarryForwardEntryNumber = anNumber,
                NextYearId = nextYear.Id,
                Preview = PreviewYear(storage, companyId, fiscalYearId)
            };
        }

        public static async Task<(FiscalYear? Year, string? Error)> OpenNextYearAsync(
            IStorageBroker storage,
            string? companyId,
            int fiscalYearId,
            string? actor)
        {
            var year = FindYear(storage, companyId, fiscalYearId);
            if (year == null) return (null, "Exercice introuvable.");
            var next = await EnsureNextYearAsync(storage, companyId, year, actor);
            return (next, null);
        }

        private static async Task<FiscalYear> EnsureNextYearAsync(
            IStorageBroker storage,
            string? companyId,
            FiscalYear year,
            string? actor)
        {
            var existing = FindNextYear(storage, companyId, year.EndDate.Date);
            if (existing != null) return existing;

            var length = (year.EndDate.Date - year.StartDate.Date).Days + 1;
            var start = year.EndDate.Date.AddDays(1);
            var end = start.AddDays(length - 1);
            var created = new FiscalYear
            {
                Name = FiscalYearCalendar.BuildYearName(start, end),
                StartDate = start,
                EndDate = end,
                Status = "Open",
                CompanyId = companyId,
                CreatedBy = actor,
                UpdatedBy = actor,
                Periods = FiscalYearCalendar.BuildMonthlyPeriods(start, end, companyId)
            };
            return await storage.InsertFiscalYearAsync(created);
        }

        private static async Task<AccountingEntry> InsertGeneratedEntryAsync(
            IStorageBroker storage,
            INumberingSequenceService numbering,
            string? companyId,
            string? actor,
            DateTime entryDate,
            int? fiscalPeriodId,
            string journalCode,
            string referenceType,
            int referenceId,
            string description,
            List<AccountingEntryLine> lines)
        {
            var journal = await AccountingEntryResolver.ResolveJournalAsync(storage, companyId, journalCode);
            for (var i = 0; i < lines.Count; i++)
                lines[i].LineNumber = i + 1;

            var entry = new AccountingEntry
            {
                EntryNumber = await numbering.GetNextNumberAsync("AccountingEntry", companyId),
                EntryDate = entryDate,
                JournalType = journalCode,
                JournalId = journal?.Id,
                FiscalPeriodId = fiscalPeriodId,
                ReferenceType = referenceType,
                ReferenceId = referenceId,
                Description = description,
                Status = "Posted",
                CompanyId = companyId,
                CreatedBy = SalesDocumentAudit.IsReadableActor(actor) ? actor!.Trim() : null,
                CreatedAt = DateTime.UtcNow,
                Lines = lines
            };
            return await storage.InsertAccountingEntryAsync(entry);
        }

        private static List<AccountingEntryLine> BuildResultClosingLines(
            Dictionary<string, decimal> balances,
            string resultAccount,
            IReadOnlyDictionary<string, string> labels)
        {
            var lines = new List<AccountingEntryLine>();
            foreach (var (account, net) in balances.Where(kv => IsResultClass(kv.Key) && kv.Value != 0m)
                         .OrderBy(kv => kv.Key, StringComparer.Ordinal))
            {
                var abs = Math.Abs(net);
                lines.Add(net > 0
                    ? EntryLine(account, LabelOf(labels, account, $"Solde compte {account}"), 0, abs)
                    : EntryLine(account, LabelOf(labels, account, $"Solde compte {account}"), abs, 0));
            }

            if (lines.Count == 0) return lines;

            var delta = Round(lines.Sum(l => l.Debit) - lines.Sum(l => l.Credit));
            if (delta != 0m)
            {
                lines.Add(delta > 0
                    ? EntryLine(resultAccount, LabelOf(labels, resultAccount, "Résultat de l'exercice"), 0, delta)
                    : EntryLine(resultAccount, LabelOf(labels, resultAccount, "Résultat de l'exercice"), -delta, 0));
            }

            return lines;
        }

        private static List<AccountingEntryLine> BuildCarryForwardLines(
            Dictionary<string, decimal> balances,
            IReadOnlyDictionary<string, string> labels)
        {
            var lines = new List<AccountingEntryLine>();
            foreach (var (account, net) in balances.Where(kv => IsBilanClass(kv.Key) && kv.Value != 0m)
                         .OrderBy(kv => kv.Key, StringComparer.Ordinal))
            {
                var abs = Math.Abs(net);
                lines.Add(net > 0
                    ? EntryLine(account, LabelOf(labels, account, $"À-nouveau {account}"), abs, 0)
                    : EntryLine(account, LabelOf(labels, account, $"À-nouveau {account}"), 0, abs));
            }

            var delta = Round(lines.Sum(l => l.Debit) - lines.Sum(l => l.Credit));
            if (delta != 0m && lines.Count > 0)
            {
                var last = lines[^1];
                if (delta > 0) last.Credit += delta;
                else last.Debit += -delta;
            }

            return lines.Count >= 2 ? lines : new List<AccountingEntryLine>();
        }

        private static AccountingEntryLine EntryLine(string code, string label, decimal debit, decimal credit) => new()
        {
            AccountCode = code,
            AccountLabel = label,
            Debit = Round(debit),
            Credit = Round(credit)
        };

        private static decimal ProfitOf(Dictionary<string, decimal> balances)
        {
            decimal charges = 0m, products = 0m;
            foreach (var (account, net) in balances)
            {
                var cls = ClassOf(account);
                if (cls == 6) charges += net;
                if (cls == 7) products += -net;
            }
            return Round(products - charges);
        }

        private static string ResolveResultAccount(IStorageBroker storage, string? companyId, decimal profit)
        {
            var settings = storage.SelectAllCompanyAccountingSettings()
                .FirstOrDefault(s => s.CompanyId == companyId);
            var pcm = settings != null
                && string.Equals(settings.PlanType, AccountingSeedService.PlanTypePcmMaroc, StringComparison.OrdinalIgnoreCase);
            var preferred = profit >= 0
                ? (pcm ? "129000" : "120000")
                : (pcm ? "129100" : "129000");
            var fallback = profit >= 0 ? "129000" : "129000";
            var numbers = storage.SelectAllChartOfAccounts()
                .ForCompany(companyId)
                .Select(a => a.AccountNumber)
                .ToHashSet(StringComparer.Ordinal);
            if (numbers.Count == 0 || numbers.Contains(preferred)) return preferred;
            if (numbers.Contains(fallback)) return fallback;
            return preferred;
        }

        private static Dictionary<string, decimal> SumBalances(List<AccountingEntry> entries)
        {
            var map = new Dictionary<string, decimal>(StringComparer.Ordinal);
            foreach (var line in entries.SelectMany(e => e.Lines ?? new List<AccountingEntryLine>()))
            {
                if (string.IsNullOrWhiteSpace(line.AccountCode)) continue;
                var code = line.AccountCode.Trim();
                map[code] = map.GetValueOrDefault(code) + line.Debit - line.Credit;
            }

            foreach (var key in map.Keys.ToList())
                map[key] = Round(map[key]);
            return map;
        }

        private static List<AccountingEntry> LoadBooked(IStorageBroker storage, string? companyId, DateTime from, DateTime to) =>
            storage.SelectAllAccountingEntries()
                .ForCompany(companyId)
                .Where(e => e.Status == "Posted" || e.Status == "Validated")
                .AsEnumerable()
                .Where(e => e.EntryDate.Date >= from && e.EntryDate.Date <= to)
                .ToList();

        private static List<AccountingEntry> LoadDrafts(IStorageBroker storage, string? companyId, DateTime from, DateTime to) =>
            storage.SelectAllAccountingEntries()
                .ForCompany(companyId)
                .Where(e => e.Status == "Draft")
                .AsEnumerable()
                .Where(e => e.EntryDate.Date >= from && e.EntryDate.Date <= to)
                .ToList();

        private static int CountUnlettered(IStorageBroker storage, string? companyId, List<AccountingEntry> booked)
        {
            var lettrable = storage.SelectAllChartOfAccounts()
                .ForCompany(companyId)
                .Where(a => a.IsLettrable)
                .Select(a => a.AccountNumber)
                .ToHashSet(StringComparer.Ordinal);
            if (lettrable.Count == 0)
            {
                var settings = storage.SelectAllCompanyAccountingSettings().FirstOrDefault(s => s.CompanyId == companyId);
                lettrable.Add(settings?.CustomerAccountCode ?? "411000");
                lettrable.Add(settings?.SupplierAccountCode ?? "401000");
            }

            return booked.SelectMany(e => e.Lines ?? new List<AccountingEntryLine>())
                .Count(l => lettrable.Contains(l.AccountCode) && string.IsNullOrWhiteSpace(l.LettrageCode));
        }

        private static Dictionary<string, string> LoadLabels(IStorageBroker storage, string? companyId) =>
            storage.SelectAllChartOfAccounts()
                .ForCompany(companyId)
                .AsEnumerable()
                .GroupBy(a => a.AccountNumber)
                .ToDictionary(g => g.Key, g => g.First().Label, StringComparer.Ordinal);

        private static FiscalYear? FindYear(IStorageBroker storage, string? companyId, int id) =>
            storage.SelectAllFiscalYears()
                .ForCompany(companyId)
                .FirstOrDefault(y => y.Id == id);

        private static FiscalYear? FindNextYear(IStorageBroker storage, string? companyId, DateTime currentEnd) =>
            storage.SelectAllFiscalYears()
                .ForCompany(companyId)
                .AsEnumerable()
                .FirstOrDefault(y => y.StartDate.Date == currentEnd.AddDays(1));

        private static bool IsResultClass(string account)
        {
            var cls = ClassOf(account);
            return cls is 6 or 7;
        }

        private static bool IsBilanClass(string account)
        {
            var cls = ClassOf(account);
            return cls is >= 1 and <= 5;
        }

        private static int? ClassOf(string account)
        {
            var code = account.Trim();
            if (code.Length == 0 || !char.IsDigit(code[0])) return null;
            return code[0] - '0';
        }

        private static string LabelOf(IReadOnlyDictionary<string, string> labels, string account, string fallback) =>
            labels.TryGetValue(account, out var label) && !string.IsNullOrWhiteSpace(label) ? label : fallback;

        private static CheckItemDto Blocking(string code, string message) => new()
        {
            Code = code, Severity = SeverityBlocking, Message = message
        };

        private static CheckItemDto Warning(string code, string message) => new()
        {
            Code = code, Severity = SeverityWarning, Message = message
        };

        private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
    }
}
