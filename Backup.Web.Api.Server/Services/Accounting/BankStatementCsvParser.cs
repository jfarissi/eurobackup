using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Backup.Web.Api.Server.Services.Accounting
{
    /// <summary>
    /// Parser CSV générique de relevé bancaire (séparateur ; ou ,).
    /// Colonnes reconnues : date, libellé, référence, débit, crédit, solde.
    /// </summary>
    public static class BankStatementCsvParser
    {
        public sealed class ParsedLine
        {
            public DateTime OperationDate { get; set; }
            public string Label { get; set; } = string.Empty;
            public string? Reference { get; set; }
            public decimal Debit { get; set; }
            public decimal Credit { get; set; }
            public decimal? RunningBalance { get; set; }
        }

        public static List<ParsedLine> Parse(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
                throw new InvalidOperationException("Le fichier CSV est vide.");

            var lines = content.Replace("\r\n", "\n").Replace('\r', '\n')
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim())
                .Where(l => l.Length > 0)
                .ToList();
            if (lines.Count == 0)
                throw new InvalidOperationException("Le fichier CSV est vide.");

            var delimiter = DetectDelimiter(lines[0]);
            var header = Split(lines[0], delimiter).Select(NormalizeHeader).ToList();
            var hasHeader = header.Any(IsKnownHeader);
            var start = hasHeader ? 1 : 0;
            var map = hasHeader ? BuildMap(header) : DefaultMap(Split(lines[0], delimiter).Count);

            var result = new List<ParsedLine>();
            for (var i = start; i < lines.Count; i++)
            {
                var cols = Split(lines[i], delimiter);
                if (cols.All(string.IsNullOrWhiteSpace)) continue;
                var dateRaw = Get(cols, map, "date");
                if (!TryParseDate(dateRaw, out var date))
                    throw new InvalidOperationException($"Date invalide à la ligne {i + 1} : « {dateRaw} ».");

                var debit = ParseAmount(Get(cols, map, "debit"));
                var credit = ParseAmount(Get(cols, map, "credit"));
                var amount = ParseAmount(Get(cols, map, "amount"));
                if (debit == 0 && credit == 0 && amount != 0)
                {
                    if (amount < 0) debit = Math.Abs(amount);
                    else credit = amount;
                }

                if (debit == 0 && credit == 0) continue;

                decimal? solde = null;
                var soldeRaw = Get(cols, map, "solde");
                if (!string.IsNullOrWhiteSpace(soldeRaw))
                    solde = ParseAmount(soldeRaw);

                result.Add(new ParsedLine
                {
                    OperationDate = date,
                    Label = Get(cols, map, "label")?.Trim() ?? string.Empty,
                    Reference = EmptyToNull(Get(cols, map, "reference")),
                    Debit = Math.Abs(debit),
                    Credit = Math.Abs(credit),
                    RunningBalance = solde
                });
            }

            if (result.Count == 0)
                throw new InvalidOperationException("Aucune ligne exploitable dans le relevé.");
            return result;
        }

        private static char DetectDelimiter(string firstLine)
        {
            var commas = firstLine.Count(c => c == ',');
            var semis = firstLine.Count(c => c == ';');
            var tabs = firstLine.Count(c => c == '\t');
            if (tabs > semis && tabs > commas) return '\t';
            return semis >= commas ? ';' : ',';
        }

        private static List<string> Split(string line, char delimiter)
        {
            var result = new List<string>();
            var current = new System.Text.StringBuilder();
            var inQuotes = false;
            foreach (var ch in line)
            {
                if (ch == '"') { inQuotes = !inQuotes; continue; }
                if (ch == delimiter && !inQuotes)
                {
                    result.Add(current.ToString());
                    current.Clear();
                    continue;
                }
                current.Append(ch);
            }
            result.Add(current.ToString());
            return result;
        }

        private static string NormalizeHeader(string value) =>
            (value ?? string.Empty).Trim().Trim('"').ToLowerInvariant()
                .Replace("é", "e").Replace("è", "e").Replace("ê", "e");

        private static bool IsKnownHeader(string header)
        {
            var key = Classify(header);
            return key is "date" or "label" or "reference" or "debit" or "credit" or "solde" or "amount";
        }

        private static Dictionary<string, int> BuildMap(List<string> headers)
        {
            var map = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var i = 0; i < headers.Count; i++)
            {
                var key = Classify(headers[i]);
                if (key != null && !map.ContainsKey(key)) map[key] = i;
            }
            return map;
        }

        private static Dictionary<string, int> DefaultMap(int count)
        {
            var map = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["date"] = 0,
                ["label"] = 1
            };
            if (count > 2) map["reference"] = 2;
            if (count > 3) map["debit"] = 3;
            if (count > 4) map["credit"] = 4;
            if (count > 5) map["solde"] = 5;
            return map;
        }

        private static string? Classify(string header) => header switch
        {
            "date" or "dateop" or "dateoperation" or "operationdate"
                or "date operation" or "date d operation" or "datecomptable" => "date",
            "libelle" or "label" or "description" or "intitule"
                or "intitule operation" or "libelle operation" or "designation" => "label",
            "reference" or "ref" or "nref" or "n piece" or "piece" or "fitid" or "cheque" => "reference",
            "debit" or "withdrawal" or "sortie" => "debit",
            "credit" or "deposit" or "entree" => "credit",
            "solde" or "balance" or "running" or "soldedisponible" => "solde",
            "montant" or "amount" => "amount",
            _ => null
        };

        private static string Get(List<string> cols, Dictionary<string, int> map, string key) =>
            map.TryGetValue(key, out var i) && i >= 0 && i < cols.Count ? cols[i] : string.Empty;

        private static string? EmptyToNull(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private static bool TryParseDate(string raw, out DateTime date)
        {
            raw = (raw ?? string.Empty).Trim();
            var formats = new[] { "dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy", "yyyy-MM-dd", "dd/MM/yy" };
            if (DateTime.TryParseExact(raw, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
                return true;
            return DateTime.TryParse(raw, CultureInfo.GetCultureInfo("fr-FR"), DateTimeStyles.None, out date);
        }

        private static decimal ParseAmount(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return 0m;
            var clean = raw.Trim().Replace(" ", "").Replace("\u00A0", "");
            if (clean.Contains(',') && clean.Contains('.'))
            {
                if (clean.LastIndexOf(',') > clean.LastIndexOf('.'))
                    clean = clean.Replace(".", "").Replace(',', '.');
                else
                    clean = clean.Replace(",", "");
            }
            else if (clean.Contains(','))
            {
                clean = clean.Replace(',', '.');
            }
            return decimal.TryParse(clean, NumberStyles.Any, CultureInfo.InvariantCulture, out var value) ? value : 0m;
        }
    }
}
