using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace Backup.Web.Api.Server.Services.Accounting
{
    /// <summary>Extraction regex des factures marocaines (ICE / IF / RC / TVA) et des relevés OCR.</summary>
    public static class MoroccanDocumentParser
    {
        public sealed class InvoiceExtraction
        {
            public string DocumentType { get; set; } = "facture";
            public string? Ice { get; set; }
            public string? TaxId { get; set; }
            public string? TradeRegister { get; set; }
            public string? InvoiceNumber { get; set; }
            public DateTime? InvoiceDate { get; set; }
            public string? PartyName { get; set; }
            public decimal? AmountHt { get; set; }
            public decimal? VatAmount { get; set; }
            public decimal? AmountTtc { get; set; }
            public decimal? VatRate { get; set; }
            public double Confidence { get; set; }
        }

        public static InvoiceExtraction ParseInvoice(string text)
        {
            var raw = text ?? string.Empty;
            var ice = Match(raw, @"(?:ICE|I\.C\.E\.?)[\s:.-]*([0-9]{15})");
            var taxId = Match(raw, @"(?:IF|I\.F\.|Identifiant\s+fiscal)[\s:.-]*([0-9]{7,10})");
            var rc = Match(raw, @"(?:R\.?C\.?|Registre\s+(?:de\s+)?commerce)[\s:.-]*([A-Z0-9\-/]{4,})");
            var number = Match(raw, @"(?:Facture|FACTURE|N[°o]|Invoice)[\s:.-]*([A-Z]{0,4}[-/]?\d{2,}[-/]?\d*)");
            var date = ParseFirstDate(raw);
            var ht = ParseLabeledAmount(raw, @"(?:Total\s*HT|Montant\s*HT|H\.?T\.?)");
            var ttc = ParseLabeledAmount(raw, @"(?:Total\s*TTC|Montant\s*TTC|T\.?T\.?C\.?)");
            var vat = ParseLabeledAmount(raw, @"(?:TVA|VAT)");
            var rate = ParseVatRate(raw);
            if (ht == null && ttc != null && vat != null) ht = ttc - vat;
            if (ttc == null && ht != null && vat != null) ttc = ht + vat;
            if (vat == null && ht != null && ttc != null) vat = ttc - ht;
            if (rate == null && ht is > 0 && vat != null) rate = Math.Round(vat.Value / ht.Value * 100, 2);

            var party = Match(raw, @"(?:Client|Fournisseur|Ste|Société)[\s:]+([A-Za-zÀ-ÿ0-9 .,'-]{3,80})");
            var hits = new[] { ice, taxId, rc, number }.Count(v => v != null) + (date != null ? 1 : 0) + (ttc != null || ht != null ? 1 : 0);
            return new InvoiceExtraction
            {
                Ice = ice,
                TaxId = taxId,
                TradeRegister = rc,
                InvoiceNumber = number,
                InvoiceDate = date,
                PartyName = party?.Trim(),
                AmountHt = ht,
                VatAmount = vat,
                AmountTtc = ttc,
                VatRate = rate,
                Confidence = Math.Min(1, hits / 6.0)
            };
        }

        /// <summary>
        /// Même logique que le classifieur Python : facture / BL / relevé bancaire.
        /// </summary>
        public static (string Type, double Confidence) Classify(string text, string? fileName = null)
        {
            var name = fileName ?? "";
            if (name.EndsWith(".ofx", StringComparison.OrdinalIgnoreCase)
                || name.EndsWith(".qfx", StringComparison.OrdinalIgnoreCase)
                || name.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                return ("releve_bancaire", 0.95);

            var t = (text ?? "").ToLowerInvariant();
            if (LooksLikeOfx(text ?? "", fileName) || LooksLikeDelimitedStatement(text ?? ""))
                return ("releve_bancaire", 0.9);

            int Score(params string[] terms) => terms.Count(term => t.Contains(term));
            var invoice = Score("invoice", "facture", "factuur", "faktuur", "total ht", "total ttc", "montant ht");
            var delivery = Score("delivery note", "bon de livraison", "leveringsbon", "leveringsbevestiging", "verzendbon", "pakbon");
            var bank = Score(
                "relevé bancaire", "releve bancaire", "relevé de compte", "releve de compte",
                "extrait de compte", "bank statement", "ofxheader", "<ofx",
                "solde précédent", "solde precedent", "ancien solde", "nouveau solde",
                "cih bank", "attijariwafa", "mouvements du compte");
            if (t.Contains("bmce") && (t.Contains("solde") || t.Contains("vir ") || t.Contains("debit")))
                bank += 2;

            var best = invoice;
            var type = "facture";
            if (delivery > best) { best = delivery; type = "bon_livraison"; }
            if (bank > best) { best = bank; type = "releve_bancaire"; }
            var total = invoice + delivery + bank;
            var conf = total == 0 ? 0 : Math.Round(best / (double)total, 3);
            return (type, conf);
        }

        public static List<BankStatementCsvParser.ParsedLine> ParseBankStatement(string text, string? bank = null)
        {
            var raw = text ?? string.Empty;
            if (LooksLikeDelimitedStatement(raw) || LooksLikeOfx(raw, bank))
            {
                try { return BankStatementImport.Parse(raw, bank); }
                catch (InvalidOperationException) { /* fallback OCR lines */ }
            }

            var result = new List<BankStatementCsvParser.ParsedLine>();
            foreach (var line in raw.Replace("\r\n", "\n").Split('\n'))
            {
                var trimmed = line.Trim();
                if (trimmed.Length < 8) continue;
                var dateMatch = Regex.Match(trimmed, @"\b(\d{1,2}[/\-]\d{1,2}[/\-]\d{2,4})\b");
                if (!dateMatch.Success) continue;
                if (!TryParseDate(dateMatch.Groups[1].Value, out var date)) continue;
                var amounts = Regex.Matches(trimmed, @"-?\d{1,3}(?:[ \u00A0]?\d{3})*[.,]\d{2}");
                if (amounts.Count == 0) continue;
                var last = ParseAmount(amounts[^1].Value);
                if (last == 0m) continue;
                var creditHint = Regex.IsMatch(trimmed, @"\b(C|CR|CREDIT|VIR)\b", RegexOptions.IgnoreCase);
                var debitHint = Regex.IsMatch(trimmed, @"\b(D|DB|DEBIT|CHQ|PREL)\b", RegexOptions.IgnoreCase);
                decimal debit = 0, credit = 0;
                if (creditHint && !debitHint) credit = Math.Abs(last);
                else if (debitHint && !creditHint) debit = Math.Abs(last);
                else if (last < 0) debit = Math.Abs(last);
                else credit = last;
                var label = trimmed.Replace(dateMatch.Value, "").Trim();
                label = Regex.Replace(label, @"-?\d{1,3}(?:[ \u00A0]?\d{3})*[.,]\d{2}", "").Trim();
                result.Add(new BankStatementCsvParser.ParsedLine
                {
                    OperationDate = date,
                    Label = string.IsNullOrWhiteSpace(label) ? trimmed : label,
                    Debit = debit,
                    Credit = credit
                });
            }

            if (result.Count == 0)
                throw new InvalidOperationException("Aucune ligne de relevé reconnue.");
            return result;
        }

        private static bool LooksLikeOfx(string raw, string? fileName)
        {
            var name = fileName ?? "";
            if (name.EndsWith(".ofx", StringComparison.OrdinalIgnoreCase)
                || name.EndsWith(".qfx", StringComparison.OrdinalIgnoreCase))
                return true;
            var head = raw.TrimStart();
            return head.StartsWith("OFXHEADER", StringComparison.OrdinalIgnoreCase)
                || head.StartsWith("<OFX", StringComparison.OrdinalIgnoreCase);
        }

        private static bool LooksLikeDelimitedStatement(string raw)
        {
            var first = raw.Replace("\r\n", "\n").Split('\n').FirstOrDefault(l => l.Trim().Length > 0) ?? "";
            var n = first.ToLowerInvariant();
            return (first.Contains(';') || first.Contains('\t'))
                && (n.Contains("date") || n.Contains("libelle") || n.Contains("debit"));
        }

        private static string? Match(string text, string pattern)
        {
            var m = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
            return m.Success ? m.Groups[1].Value.Trim() : null;
        }

        private static decimal? ParseLabeledAmount(string text, string label)
        {
            var m = Regex.Match(text, label + @"[\s:]*(-?\d{1,3}(?:[ \u00A0]?\d{3})*[.,]\d{2}|\d+[.,]\d{2})", RegexOptions.IgnoreCase);
            return m.Success ? ParseAmount(m.Groups[1].Value) : null;
        }

        private static decimal? ParseVatRate(string text)
        {
            var m = Regex.Match(text, @"TVA[^\d]{0,12}(20|14|10|7)\s*%", RegexOptions.IgnoreCase);
            return m.Success ? decimal.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) : null;
        }

        private static DateTime? ParseFirstDate(string text)
        {
            foreach (Match m in Regex.Matches(text, @"\b(\d{1,2}[/\-]\d{1,2}[/\-]\d{2,4})\b"))
            {
                if (TryParseDate(m.Groups[1].Value, out var date)) return date;
            }
            return null;
        }

        private static bool TryParseDate(string raw, out DateTime date)
        {
            var formats = new[] { "dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy", "dd/MM/yy" };
            return DateTime.TryParseExact(raw.Trim(), formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out date)
                || DateTime.TryParse(raw, CultureInfo.GetCultureInfo("fr-FR"), DateTimeStyles.None, out date);
        }

        private static decimal ParseAmount(string raw)
        {
            var clean = raw.Trim().Replace(" ", "").Replace("\u00A0", "");
            if (clean.Contains(',') && clean.Contains('.'))
            {
                if (clean.LastIndexOf(',') > clean.LastIndexOf('.'))
                    clean = clean.Replace(".", "").Replace(',', '.');
                else
                    clean = clean.Replace(",", "");
            }
            else if (clean.Contains(','))
                clean = clean.Replace(',', '.');
            return decimal.TryParse(clean, NumberStyles.Any, CultureInfo.InvariantCulture, out var value) ? value : 0m;
        }
    }
}
