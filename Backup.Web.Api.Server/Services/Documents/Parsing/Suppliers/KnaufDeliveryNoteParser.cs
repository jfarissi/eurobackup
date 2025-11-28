using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Backup.Web.Api.Server.Models;

namespace Backup.Web.Api.Server.Services.Documents.Parsing.Suppliers
{
    public class KnaufFinalParser : IDocumentParser
    {
        private readonly DocumentParserConfig _config;

        public KnaufFinalParser(DocumentParserConfig config)
        {
            _config = config;
        }

        public bool CanParse(DocumentLanguage language, string text)
        {
            if (string.IsNullOrWhiteSpace(text)) 
                return false;

            var lower = text.ToLowerInvariant();
            return lower.Contains("knauf") && lower.Contains("leveringsbevestiging");
        }

        public List<DocumentLine> Parse(DocumentLanguage language, string text)
        {
            var results = new List<DocumentLine>();
            if (string.IsNullOrWhiteSpace(text)) 
                return results;

            Console.WriteLine("=== DÉBUT PARSING KNAUF ===");

            try
            {
                var lines = text.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
                var cleanedLines = CleanLines(lines).ToArray();

                // Stratégie principale: pattern structuré
                var structuredResults = ParseStructuredPattern(cleanedLines);
                Console.WriteLine($"🔍 Pattern structuré: {structuredResults.Count} produits");

                // Vérification qualité
                if (structuredResults.Count >= 5)
                {
                    results = structuredResults;
                }
                else
                {
                    // Fallback: recherche élargie
                    var fallbackResults = ParseFallbackPattern(cleanedLines);
                    Console.WriteLine($"🔄 Fallback: {fallbackResults.Count} produits supplémentaires");
                    
                    results = structuredResults
                        .Concat(fallbackResults)
                        .GroupBy(x => $"{x.ProductCode}-{x.Product}-{x.Quantity}")
                        .Select(g => g.First())
                        .ToList();
                }

                Console.WriteLine($"✅ Parsing terminé: {results.Count} produits au total");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Erreur parsing: {ex.Message}");
            }

            return results
                .OrderBy(x => x.LineNumber)
                .ThenBy(x => x.ProductCode)
                .ToList();
        }

        private List<DocumentLine> ParseStructuredPattern(string[] lines)
        {
            var results = new List<DocumentLine>();
            var seenProducts = new HashSet<string>();

            for (int i = 0; i < lines.Length - 2; i++)
            {
                var line1 = lines[i]; // Ligne code produit
                var line2 = i + 1 < lines.Length ? lines[i + 1] : ""; // Ligne EAN
                var line3 = i + 2 < lines.Length ? lines[i + 2] : ""; // Ligne description + quantité

                // Pattern assoupli: "10 545753" → ligne contenant EAN → dans les 8 lignes suivantes une ligne quantité "… 8 ST"
                if (IsProductCodeLine(line1) && IsEANLine(line2))
                {
                    var codeMatch = Regex.Match(line1, @"^\s*(\d{2,3})\s+(\d{4,})\s*$");
                    if (codeMatch.Success)
                    {
                        var lineNumber = int.Parse(codeMatch.Groups[1].Value);
                        var productCode = codeMatch.Groups[2].Value;

                        int qtyIdx = -1;
                        string qtyLine = string.Empty;
                        for (int j = i + 1; j <= Math.Min(i + 8, lines.Length - 1); j++)
                        {
                            if (HasQuantityInformation(lines[j]))
                            {
                                qtyIdx = j;
                                qtyLine = lines[j];
                                break;
                            }
                        }
                        if (qtyIdx == -1) continue;

                        // Construire la description à partir des lignes entre EAN et quantité (prendre la dernière ligne alphabétique)
                        string pickedDescription = string.Empty;
                        for (int p = qtyIdx - 1; p >= i + 1 && p >= qtyIdx - 3; p--)
                        {
                            var cand = (lines[p] ?? string.Empty).Trim();
                            var low = cand.ToLowerInvariant();
                            if (string.IsNullOrWhiteSpace(cand)) continue;
                            if (IsEANLine(cand)) continue;
                            if (IsNoiseLine(cand)) continue;
                            if (Regex.IsMatch(low, @"^-{3,}\s*$")) continue;
                            // doit contenir des lettres
                            if (Regex.IsMatch(cand, @"[A-Za-z]"))
                            {
                                pickedDescription = cand;
                                break;
                            }
                        }

                        var (description, quantity, unit) = ExtractProductInfo(qtyLine);
                        if (string.IsNullOrWhiteSpace(description)) description = pickedDescription;

                        if (!string.IsNullOrEmpty(description) && quantity > 0)
                        {
                            var key = $"{productCode}-{quantity}";
                            if (!seenProducts.Contains(key))
                            {
                                results.Add(new DocumentLine
                                {
                                    LineNumber = lineNumber,
                                    ProductCode = productCode,
                                    Product = CleanProductName(description),
                                    Quantity = quantity,
                                    Unit = unit,
                                    RawLine = $"{line1} | {line2} | {qtyLine}"
                                });

                                seenProducts.Add(key);
                                Console.WriteLine($"✅ Ajouté: {productCode} - {quantity} {unit}");

                                i = qtyIdx; // sauter directement à la ligne quantité
                            }
                        }
                    }
                }
            }

            return results;
        }

        private List<DocumentLine> ParseFallbackPattern(string[] lines)
        {
            var results = new List<DocumentLine>();
            var seenProducts = new HashSet<string>();

            for (int i = 0; i < lines.Length; i++)
            {
                // Recherche de codes produit isolés
                if (IsProductCodeLine(lines[i]))
                {
                    var codeMatch = Regex.Match(lines[i], @"^\s*(\d{2,3})\s+(\d{4,})\s*$");
                    if (codeMatch.Success)
                    {
                        var lineNumber = int.Parse(codeMatch.Groups[1].Value);
                        var productCode = codeMatch.Groups[2].Value;

                        // Chercher la quantité dans les 8 lignes suivantes (en ignorant lignes tarifaires)
                        for (int j = i + 1; j < Math.Min(i + 9, lines.Length); j++)
                        {
                            var candidate = lines[j];
                            var lower = candidate.Trim().ToLowerInvariant();
                            if (Regex.IsMatch(lower, @"/\s*1\s*st\b") || lower.Contains("%")) continue;
                            if (HasQuantityInformation(candidate))
                            {
                                // Construire la description en regardant les 2 lignes précédentes
                                string pickedDescription = string.Empty;
                                for (int p = j - 1; p >= i + 1 && p >= j - 3; p--)
                                {
                                    var cand = (lines[p] ?? string.Empty).Trim();
                                    var low = cand.ToLowerInvariant();
                                    if (string.IsNullOrWhiteSpace(cand)) continue;
                                    if (IsEANLine(cand)) continue;
                                    if (IsNoiseLine(cand)) continue;
                                    if (Regex.IsMatch(low, @"^-{3,}\s*$")) continue;
                                    if (Regex.IsMatch(cand, @"[A-Za-z]")) { pickedDescription = cand; break; }
                                }

                                var (description, quantity, unit) = ExtractProductInfo(candidate);
                                if (string.IsNullOrWhiteSpace(description)) description = pickedDescription;
                                
                                if (quantity > 0)
                                {
                                    var key = $"{productCode}-{quantity}";
                                    if (!seenProducts.Contains(key))
                                    {
                                        results.Add(new DocumentLine
                                        {
                                            LineNumber = lineNumber,
                                            ProductCode = productCode,
                                            Product = CleanProductName(description),
                                            Quantity = quantity,
                                            Unit = unit,
                                            RawLine = $"{lines[i]} | {lines[j]}"
                                        });

                                        seenProducts.Add(key);
                                    }
                                    break;
                                }
                            }
                        }
                    }
                }
            }

            return results;
        }

        private bool IsProductCodeLine(string line)
        {
            return Regex.IsMatch(line, @"^\s*\d{2,3}\s+\d{4,}\s*$");
        }

        private bool IsEANLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return false;
            return Regex.IsMatch(line, @"\b\d{13}\b");
        }

        private bool HasQuantityInformation(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return false;
            var m = Regex.Matches(line, @"\b(\d+)\s*(ST|PAK)\b", RegexOptions.IgnoreCase);
            if (m.Count == 0) return false;
            // reject price-like tokens such as "/1 ST"
            foreach (Match match in m)
            {
                int start = match.Index - 1;
                while (start >= 0 && start >= match.Index - 3 && char.IsWhiteSpace(line[start])) start--;
                if (start >= 0 && line[start] == '/') continue;
                return true;
            }
            return false;
        }

        private (string description, decimal quantity, string unit) ExtractProductInfo(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return (string.Empty, 0, "ST");
            // Remove obvious price/discount noise
            var cleaned = Regex.Replace(line, @"\d+[.,]\d+\s*/\s*\d+\s*[A-Za-z]", "");
            cleaned = Regex.Replace(cleaned, @"\d+\s*%\b", "");

            var matches = Regex.Matches(cleaned, @"\b(\d+)\s*(ST|PAK)\b", RegexOptions.IgnoreCase);
            if (matches.Count == 0) return (cleaned.Trim(), 0, "ST");

            Match chosen = null;
            foreach (Match m in matches)
            {
                int start = m.Index - 1;
                while (start >= 0 && start >= m.Index - 3 && char.IsWhiteSpace(cleaned[start])) start--;
                if (start >= 0 && cleaned[start] == '/') continue; // skip "/1 ST" like price tokens
                chosen = m; // keep last non-price match
            }
            if (chosen == null) return (cleaned.Trim(), 0, "ST");

            var qty = decimal.Parse(chosen.Groups[1].Value);
            var unit = chosen.Groups[2].Value.ToUpperInvariant();
            var description = cleaned.Substring(0, chosen.Index).Trim();
            return (CleanProductName(description), qty, unit);
        }

        private string CleanProductName(string productName)
        {
            if (string.IsNullOrWhiteSpace(productName)) 
                return productName;

            // Nettoyage conservateur
			var cleaned = productName;

            // Supprimer les prix
            cleaned = Regex.Replace(cleaned, @"\d+[.,]\d+\s*/\s*\d+\s*[A-Za-z]", "");
            cleaned = Regex.Replace(cleaned, @"\d+[.,]\d+", "");
            cleaned = Regex.Replace(cleaned, @"\d+%", "");

            // Supprimer les références entre parenthèses qui ne sont pas descriptives
            cleaned = Regex.Replace(cleaned, @"\(\d+\)\s*$", "");

			// Troncature aux tokens de footer/en-tête fréquents
			var tokens = new[] { "bank", "rekening", "iban", "bic", "swift", "www.knauf.com", "algemene verkoopsvoorwaarden", "leveringsbevestiging", "pagina", "factuur", "invoice" };
			var lower = cleaned.ToLowerInvariant();
			int cut = -1;
			foreach (var t in tokens)
			{
				var idx = lower.IndexOf(t);
				if (idx >= 0) cut = cut == -1 ? idx : Math.Min(cut, idx);
			}
			if (cut > 0) cleaned = cleaned.Substring(0, cut).Trim();

			// Exclure palettes
			var l2 = cleaned.ToLowerInvariant();
			if (l2.Contains("euro-palet") || l2.Contains("euro palet") || l2.Contains("palet") || l2.Contains("palette"))
				return string.Empty;

            // Normaliser les espaces
            cleaned = Regex.Replace(cleaned, @"\s+", " ").Trim();

            return cleaned;
        }

        private IEnumerable<string> CleanLines(string[] lines)
        {
            foreach (var line in lines)
            {
                var cleanLine = line.Trim();
                if (string.IsNullOrWhiteSpace(cleanLine)) 
                    continue;

                if (IsNoiseLine(cleanLine)) 
                    continue;

                yield return cleanLine;
            }
        }

        private bool IsNoiseLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) 
                return true;

            var lower = line.ToLowerInvariant();

            // Lignes de bruit évidentes
            var noisePatterns = new[]
            {
                "bank", "rekening", "iban", "bic", "swift", "deutsche bank",
                "algemene verkoopsvoorwaarden", "www.knauf.com", 
                "betalingsvoorwaarden", "totaal posities", "euro-palet",
                "factuur", "invoice", "nummer", "boekingsdatum", "boekh.document",
                "p_o_s_.", "____beschrijving", "rue du parc industriel",
                "n. et b. knauf", "tel :", "fax :"
            };

            return noisePatterns.Any(pattern => lower.Contains(pattern)) ||
                   Regex.IsMatch(lower, @"^-{3,}\s*$");
        }
    }
}