using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Models.Entities;
using Backup.Web.Api.Server.Models.Entities.Accounting;
using Backup.Web.Api.Server.Models.Entities.SaaS;
using Backup.Web.Api.Server.Services.Tenancy;

namespace Backup.Web.Api.Server.Services.Accounting
{
    /// <summary>
    /// Exports légaux : FEC (18 colonnes, séparateur |) et CSV générique (mêmes colonnes, ;).
    /// Écritures Posted / Validated de l'exercice uniquement.
    /// </summary>
    public static class AccountingExportsService
    {
        public const string FecHeader =
            "JournalCode|JournalLib|EcritureNum|EcritureDate|CompteNum|CompteLib|" +
            "CompAuxNum|CompAuxLib|PieceRef|PieceDate|EcritureLib|Debit|Credit|" +
            "EcritureLet|DateLet|ValidDate|Montantdevise|Idevise";

        public sealed class ExportPreviewDto
        {
            public int FiscalYearId { get; set; }
            public string YearName { get; set; } = string.Empty;
            public DateTime From { get; set; }
            public DateTime To { get; set; }
            public int EntryCount { get; set; }
            public int LineCount { get; set; }
        }

        public sealed class ExportFile
        {
            public byte[] Content { get; set; } = Array.Empty<byte>();
            public string FileName { get; set; } = string.Empty;
            public string ContentType { get; set; } = "text/plain";
            public int LineCount { get; set; }
        }

        public static Task<(ExportPreviewDto? Preview, string? Error)> PreviewAsync(
            IStorageBroker storage, string? companyId, int yearId)
        {
            var (year, error) = ResolveYear(storage, companyId, yearId);
            if (error != null) return Task.FromResult<(ExportPreviewDto?, string?)>((null, error));

            var rows = LoadRows(storage, companyId, year!, "EUR");
            return Task.FromResult<(ExportPreviewDto?, string?)>((new ExportPreviewDto
            {
                FiscalYearId = year!.Id,
                YearName = year.Name,
                From = year.StartDate.Date,
                To = year.EndDate.Date,
                EntryCount = rows.Select(r => r.Entry.Id).Distinct().Count(),
                LineCount = rows.Count
            }, null));
        }

        public static async Task<(ExportFile? File, string? Error)> ExportFecAsync(
            IStorageBroker storage, string? companyId, int yearId)
        {
            var (year, error) = ResolveYear(storage, companyId, yearId);
            if (error != null) return (null, error);

            var company = await LoadCompanyAsync(storage, companyId);
            var currency = string.IsNullOrWhiteSpace(company?.DefaultCurrencyCode)
                ? "EUR"
                : company!.DefaultCurrencyCode.Trim();
            var rows = LoadRows(storage, companyId, year!, currency);

            var sb = new StringBuilder();
            sb.Append(FecHeader).Append('\n');
            foreach (var row in rows)
                sb.Append(FormatFecLine(row)).Append('\n');

            return (new ExportFile
            {
                Content = Encoding.UTF8.GetBytes(sb.ToString()),
                FileName = $"FEC_{FileStamp(company?.Name, year!)}.txt",
                ContentType = "text/plain; charset=utf-8",
                LineCount = rows.Count
            }, null);
        }

        public static async Task<(ExportFile? File, string? Error)> ExportCsvAsync(
            IStorageBroker storage, string? companyId, int yearId)
        {
            var (year, error) = ResolveYear(storage, companyId, yearId);
            if (error != null) return (null, error);

            var company = await LoadCompanyAsync(storage, companyId);
            var currency = string.IsNullOrWhiteSpace(company?.DefaultCurrencyCode)
                ? "EUR"
                : company!.DefaultCurrencyCode.Trim();
            var rows = LoadRows(storage, companyId, year!, currency);

            var sb = new StringBuilder();
            sb.Append(FecHeader.Replace('|', ';')).Append('\n');
            foreach (var row in rows)
                sb.Append(FormatCsvLine(row)).Append('\n');

            var bom = Encoding.UTF8.GetPreamble();
            var body = Encoding.UTF8.GetBytes(sb.ToString());
            var content = new byte[bom.Length + body.Length];
            Buffer.BlockCopy(bom, 0, content, 0, bom.Length);
            Buffer.BlockCopy(body, 0, content, bom.Length, body.Length);

            return (new ExportFile
            {
                Content = content,
                FileName = $"ECRITURES_{FileStamp(company?.Name, year!)}.csv",
                ContentType = "text/csv; charset=utf-8",
                LineCount = rows.Count
            }, null);
        }

        private static async Task<Company?> LoadCompanyAsync(IStorageBroker storage, string? companyId)
        {
            if (string.IsNullOrWhiteSpace(companyId)) return null;
            return await storage.SelectCompanyByIdAsync(companyId);
        }

        private static (FiscalYear? Year, string? Error) ResolveYear(
            IStorageBroker storage, string? companyId, int yearId)
        {
            var year = storage.SelectAllFiscalYears()
                .ForCompany(companyId)
                .FirstOrDefault(y => y.Id == yearId);
            return year == null ? (null, "Exercice introuvable.") : (year, null);
        }

        private sealed class ExportRow
        {
            public AccountingEntry Entry { get; set; } = null!;
            public AccountingEntryLine Line { get; set; } = null!;
            public string JournalCode { get; set; } = string.Empty;
            public string JournalLib { get; set; } = string.Empty;
            public string AccountLib { get; set; } = string.Empty;
            public string Currency { get; set; } = "EUR";
        }

        private static List<ExportRow> LoadRows(
            IStorageBroker storage, string? companyId, FiscalYear year, string currency)
        {
            var from = year.StartDate.Date;
            var to = year.EndDate.Date;
            var journals = storage.SelectAllJournals()
                .ForCompany(companyId)
                .AsEnumerable()
                .ToList();
            var journalsById = journals.Where(j => j.Id > 0).ToDictionary(j => j.Id);
            var journalsByCode = journals
                .GroupBy(j => j.Code, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
            var accounts = storage.SelectAllChartOfAccounts()
                .ForCompany(companyId)
                .AsEnumerable()
                .GroupBy(a => a.AccountNumber, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.First().Label, StringComparer.Ordinal);

            return storage.SelectAllAccountingEntries()
                .ForCompany(companyId)
                .Where(e => e.Status == "Posted" || e.Status == "Validated")
                .AsEnumerable()
                .Where(e => e.EntryDate.Date >= from && e.EntryDate.Date <= to)
                .SelectMany(e => (e.Lines ?? new List<AccountingEntryLine>())
                    .OrderBy(l => l.LineNumber)
                    .Select(l =>
                    {
                        var (code, lib) = ResolveJournal(e, journalsById, journalsByCode);
                        var accountLib = !string.IsNullOrWhiteSpace(l.AccountLabel)
                            ? l.AccountLabel
                            : (accounts.TryGetValue(l.AccountCode, out var chart) ? chart : string.Empty);
                        return new ExportRow
                        {
                            Entry = e,
                            Line = l,
                            JournalCode = code,
                            JournalLib = lib,
                            AccountLib = accountLib,
                            Currency = currency
                        };
                    }))
                .OrderBy(r => r.JournalCode, StringComparer.Ordinal)
                .ThenBy(r => r.Entry.EntryNumber, StringComparer.Ordinal)
                .ThenBy(r => r.Line.LineNumber)
                .ToList();
        }

        private static (string Code, string Lib) ResolveJournal(
            AccountingEntry entry,
            Dictionary<int, Journal> byId,
            Dictionary<string, Journal> byCode)
        {
            if (entry.JournalId is int jid && byId.TryGetValue(jid, out var linked))
                return (linked.Code, linked.Label);

            var mapped = entry.JournalType switch
            {
                "SalesInvoice" or "CreditNote" => ("VEN", "Journal des ventes"),
                "SupplierInvoice" or "SupplierCreditNote" => ("ACH", "Journal des achats"),
                "Payment" => ("BAN", "Journal de banque"),
                "Cash" => ("CAIS", "Journal de caisse"),
                "AN" => ("AN", "Journal des à-nouveaux"),
                "OD" or "Reversal" => ("OD", "Journal des opérations diverses"),
                _ => (string.IsNullOrWhiteSpace(entry.JournalType) ? "OD" : entry.JournalType.Trim(),
                    entry.JournalType ?? "OD")
            };

            if (byCode.TryGetValue(mapped.Item1, out var named))
                return (named.Code, named.Label);
            return mapped;
        }

        private static string FormatFecLine(ExportRow row) =>
            string.Join("|", Fields(row).Select(SanitizeFec));

        private static string FormatCsvLine(ExportRow row) =>
            string.Join(";", Fields(row).Select(CsvField));

        private static IEnumerable<string> Fields(ExportRow row)
        {
            var entry = row.Entry;
            var line = row.Line;
            var pieceRef = !string.IsNullOrWhiteSpace(entry.ReferenceType) && entry.ReferenceId > 0
                ? $"{entry.ReferenceType}-{entry.ReferenceId}"
                : entry.EntryNumber;
            var validDate = string.Equals(entry.Status, "Validated", StringComparison.OrdinalIgnoreCase)
                ? entry.UpdatedAt
                : entry.EntryDate;
            var isForeign = !string.Equals(row.Currency, "EUR", StringComparison.OrdinalIgnoreCase);
            var amount = Math.Max(line.Debit, line.Credit);

            yield return row.JournalCode;
            yield return row.JournalLib;
            yield return entry.EntryNumber;
            yield return DateFec(entry.EntryDate);
            yield return line.AccountCode;
            yield return row.AccountLib;
            yield return string.Empty;
            yield return string.Empty;
            yield return pieceRef;
            yield return DateFec(entry.EntryDate);
            yield return entry.Description;
            yield return Amount(line.Debit);
            yield return Amount(line.Credit);
            yield return line.LettrageCode ?? string.Empty;
            yield return line.LettrageDate == null ? string.Empty : DateFec(line.LettrageDate.Value);
            yield return DateFec(validDate);
            yield return isForeign ? Amount(amount) : string.Empty;
            yield return isForeign ? row.Currency : string.Empty;
        }

        private static string DateFec(DateTime date) => date.ToString("yyyyMMdd", CultureInfo.InvariantCulture);

        private static string Amount(decimal value) =>
            value.ToString("0.00", CultureInfo.InvariantCulture);

        private static string SanitizeFec(string value) =>
            (value ?? string.Empty).Replace('|', ' ').Replace('\r', ' ').Replace('\n', ' ').Trim();

        private static string CsvField(string value)
        {
            var clean = (value ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Trim();
            if (clean.Contains(';') || clean.Contains('"'))
                return "\"" + clean.Replace("\"", "\"\"") + "\"";
            return clean;
        }

        private static string FileStamp(string? companyName, FiscalYear year)
        {
            var slug = new string((companyName ?? "SOCIETE")
                .Where(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_')
                .ToArray());
            if (string.IsNullOrWhiteSpace(slug)) slug = "SOCIETE";
            return $"{slug}_{year.StartDate:yyyyMMdd}_{year.EndDate:yyyyMMdd}";
        }
    }
}
