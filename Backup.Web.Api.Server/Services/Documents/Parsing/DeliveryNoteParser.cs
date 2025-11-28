using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Backup.Web.Api.Server.Models;

namespace Backup.Web.Api.Server.Services.Documents.Parsing
{
    public class DeliveryNoteParser : IDocumentParser
    {
        private readonly DocumentParserConfig _config;

        public DeliveryNoteParser(DocumentParserConfig config)
        {
            _config = config;
        }

        public bool CanParse(DocumentLanguage language, string text)
        {
            if (string.IsNullOrWhiteSpace(text)) 
                return false;

            var lower = text.ToLowerInvariant();
            
            // Vérifier les mots-clés de livraison
            foreach (var keyword in _config.DeliveryKeywords)
            {
                if (lower.Contains(keyword))
                    return true;
            }

            // Fallback: présence d'en-têtes de colonnes
            return lower.Contains("beschrijving") && lower.Contains("geleverde");
        }

        public List<DocumentLine> Parse(DocumentLanguage language, string text)
        {
            var results = new List<DocumentLine>();
            if (string.IsNullOrWhiteSpace(text)) 
                return results;

            try
            {
                // Utiliser l'extracteur de table si disponible
                var rawLines = text.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
                var lines = TableExtractor.ExtractProductTable(rawLines).ToArray();

                // Logique de parsing simplifiée
                results = ParseLines(lines);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Erreur DeliveryNoteParser: {ex.Message}");
            }

            return results;
        }

        private List<DocumentLine> ParseLines(string[] lines)
        {
            var results = new List<DocumentLine>();
            var seenProducts = new HashSet<string>();

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (string.IsNullOrWhiteSpace(line)) 
                    continue;

                // Chercher les codes produit
                if (IsProductCodeLine(line))
                {
                    var product = ExtractProductFromContext(lines, i, seenProducts);
                    if (product != null)
                    {
                        results.Add(product);
                    }
                }
            }

            return results;
        }

        private DocumentLine ExtractProductFromContext(string[] lines, int index, HashSet<string> seenProducts)
        {
            var codeMatch = Regex.Match(lines[index], @"^\s*(\d{1,3})\s+([A-Z0-9\-]{4,})\b");
            if (!codeMatch.Success) 
                return null;

            var lineNumber = int.Parse(codeMatch.Groups[1].Value);
            var productCode = codeMatch.Groups[2].Value;

            // Chercher dans les 5 lignes suivantes
            for (int i = index + 1; i < Math.Min(index + 6, lines.Length); i++)
            {
                if (QuantityExtractor.TryExtractPieces(_config, lines[i], out var idx, out var len, out var qty, out var unit) && qty > 0)
                {
                    var description = idx > 0 ? lines[i].Substring(0, idx).Trim() : lines[i].Trim();
                    
                    var key = $"{productCode}-{qty}";
                    if (!seenProducts.Contains(key))
                    {
                        seenProducts.Add(key);
                        
                        return new DocumentLine
                        {
                            LineNumber = lineNumber,
                            ProductCode = productCode,
                            Product = CleanDescription(description),
                            Quantity = qty,
                            Unit = unit,
                            RawLine = $"{lines[index]} | {lines[i]}"
                        };
                    }
                }
            }

            return null;
        }

        private bool IsProductCodeLine(string line)
        {
            return Regex.IsMatch(line, @"^\s*\d{1,3}\s+[A-Z0-9\-]{4,}\b");
        }

        private string CleanDescription(string description)
        {
            if (string.IsNullOrWhiteSpace(description)) 
                return description;

            // Nettoyage basique
            var cleaned = Regex.Replace(description, @"\d+[.,]\d+.*", "");
            cleaned = Regex.Replace(cleaned, @"\s+", " ").Trim();

            return cleaned;
        }
    }
}