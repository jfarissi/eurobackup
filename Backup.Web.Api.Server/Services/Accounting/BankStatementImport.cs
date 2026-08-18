using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace Backup.Web.Api.Server.Services.Accounting
{
    /// <summary>Import relevé : CSV générique, formats CIH/BMCE/Attijari/BOA, OFX.</summary>
    public static class BankStatementImport
    {
        public static List<BankStatementCsvParser.ParsedLine> Parse(string content, string? fileName = null)
        {
            if (IsOfx(content, fileName))
                return Enrich(BankStatementOfxParser.Parse(content));

            var lines = BankStatementCsvParser.Parse(content);
            return Enrich(lines);
        }

        public static string? DetectBank(string? fileName, string? content)
        {
            var hay = $"{fileName} {content}".ToUpperInvariant();
            if (hay.Contains("CIH")) return "CIH";
            if (hay.Contains("ATTIJARI") || hay.Contains("WAFABANK") || hay.Contains("AWB")) return "ATTIJARI";
            if (hay.Contains("BMCE") || hay.Contains("BANK OF AFRICA") || hay.Contains("BOA")) return "BMCE";
            if (IsOfx(content, fileName)) return "OFX";
            return null;
        }

        private static bool IsOfx(string? content, string? fileName)
        {
            var name = fileName ?? "";
            if (name.EndsWith(".ofx", StringComparison.OrdinalIgnoreCase)
                || name.EndsWith(".qfx", StringComparison.OrdinalIgnoreCase))
                return true;
            var head = (content ?? string.Empty).TrimStart();
            return head.StartsWith("OFXHEADER", StringComparison.OrdinalIgnoreCase)
                || head.StartsWith("<OFX", StringComparison.OrdinalIgnoreCase);
        }

        private static List<BankStatementCsvParser.ParsedLine> Enrich(
            List<BankStatementCsvParser.ParsedLine> lines)
        {
            foreach (var line in lines)
            {
                var cheque = ExtractCheque(line.Label);
                var invoice = ExtractInvoice(line.Label);
                if (cheque != null || invoice != null)
                    line.Reference = cheque ?? invoice;
            }
            return lines;
        }

        private static string? ExtractCheque(string label)
        {
            var match = Regex.Match(label ?? "", @"(?:CHQ|CHEQUE|N°)\s*(\d{3,})", RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value : null;
        }

        private static string? ExtractInvoice(string label)
        {
            var match = Regex.Match(label ?? "", @"(?:FACT(?:URE)?|F-)[\s:]*([A-Z0-9][A-Z0-9\-/]{2,})", RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value.Trim() : null;
        }
    }

    /// <summary>Parser OFX/QFX SGML (relevés bancaires marocains et européens).</summary>
    public static class BankStatementOfxParser
    {
        public static List<BankStatementCsvParser.ParsedLine> Parse(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
                throw new InvalidOperationException("Le fichier OFX est vide.");

            var blocks = Regex.Matches(content, @"<STMTTRN>(.*?)</STMTTRN>",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (blocks.Count == 0)
            {
                blocks = Regex.Matches(content, @"<STMTTRN>(.*?)(?=<STMTTRN>|</BANKTRANLIST>|$)",
                    RegexOptions.IgnoreCase | RegexOptions.Singleline);
            }

            var result = new List<BankStatementCsvParser.ParsedLine>();
            foreach (Match block in blocks)
            {
                var body = block.Groups[1].Value;
                var posted = Tag(body, "DTPOSTED");
                if (!TryParseOfxDate(posted, out var date)) continue;
                var amount = ParseAmount(Tag(body, "TRNAMT"));
                if (amount == 0m) continue;
                var name = Tag(body, "NAME");
                var memo = Tag(body, "MEMO");
                var check = Tag(body, "CHECKNUM");
                var fitid = Tag(body, "FITID");
                var label = string.Join(" ", new[] { name, memo }.Where(s => !string.IsNullOrWhiteSpace(s))).Trim();
                result.Add(new BankStatementCsvParser.ParsedLine
                {
                    OperationDate = date,
                    Label = string.IsNullOrWhiteSpace(label) ? (fitid ?? "OFX") : label,
                    Reference = EmptyToNull(check) ?? EmptyToNull(fitid),
                    Debit = amount < 0 ? Math.Abs(amount) : 0,
                    Credit = amount > 0 ? amount : 0
                });
            }

            if (result.Count == 0)
                throw new InvalidOperationException("Aucune opération STMTTRN dans le fichier OFX.");
            return result;
        }

        private static string Tag(string body, string name)
        {
            var match = Regex.Match(body, $@"<{name}>([^<\r\n]+)", RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value.Trim() : string.Empty;
        }

        private static bool TryParseOfxDate(string raw, out DateTime date)
        {
            date = default;
            var digits = new string((raw ?? "").TakeWhile(char.IsDigit).ToArray());
            if (digits.Length < 8) return false;
            return DateTime.TryParseExact(digits[..8], "yyyyMMdd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out date);
        }

        private static decimal ParseAmount(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return 0m;
            var clean = raw.Trim().Replace(" ", "").Replace(",", ".");
            return decimal.TryParse(clean, NumberStyles.Any, CultureInfo.InvariantCulture, out var value) ? value : 0m;
        }

        private static string? EmptyToNull(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
