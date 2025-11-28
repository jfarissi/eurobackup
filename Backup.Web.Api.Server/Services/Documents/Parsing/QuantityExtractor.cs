using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Backup.Web.Api.Server.Services.Documents.Parsing
{
    internal static class QuantityExtractor
    {
        private static readonly Regex QtyWithUnit = new(@"\b(\d{1,3}(?:[ \u00A0]\d{3})*(?:[.,]\d+)?)(?:\s*([A-Za-z\.]{1,10}))?\b", RegexOptions.Compiled);

        public static bool TryExtractPieces(DocumentParserConfig config, string raw, out int index, out int length, out decimal qty, out string unit)
        {
            index = -1; 
            length = 0; 
            qty = 0m; 
            unit = null;

            if (string.IsNullOrWhiteSpace(raw)) 
                return false;

            // Nettoyage plus agressif
            var text = Regex.Replace(raw, @"\([^\)]*\)", ""); // Supprime contenu entre parenthèses
            text = Regex.Replace(text, @"\d+[.,]\d+\s*/\s*\d+\s*[A-Za-z]", ""); // Supprime les prix
            text = Regex.Replace(text, @"\s+", " ").Trim(); // Normalise les espaces

            var matches = QtyWithUnit.Matches(text).Cast<Match>().ToList();
            if (matches.Count == 0) 
                return false;

            // Filtrer uniquement les unités pièces VALIDES
            var pieceMatches = matches.Where(m =>
            {
                var unitValue = m.Groups.Count > 2 ? m.Groups[2].Value.Trim().TrimEnd('.') : string.Empty;
                
                if (string.IsNullOrEmpty(unitValue) || !config.PieceUnits.Contains(unitValue))
                    return false;

                // Anti-falsification: vérifier le contexte
                var matchText = m.Value;
                var before = text.Substring(0, m.Index).Trim();
                var after = text.Substring(m.Index + m.Length).Trim();

                // Rejeter si c'est un prix (ex: "19,43 /1 ST")
                if (before.EndsWith("/") || after.StartsWith("/"))
                    return false;

                // Rejeter si c'est un poids déguisé (ex: "16 KG" mais capturé comme "16" + "KG" mal interprété)
                if (unitValue.Equals("kg", System.StringComparison.OrdinalIgnoreCase))
                    return false;

                return true;
            }).ToList();

            if (pieceMatches.Count == 0) 
                return false;

            // Prendre la DERNIÈRE occurrence (les quantités sont généralement en fin de ligne)
            var best = pieceMatches.Last();
            
            index = best.Index;
            length = best.Length;
            unit = best.Groups.Count > 2 ? best.Groups[2].Value.Trim().ToUpper() : "ST";
            qty = ParseDecimal(best.Groups[1].Value);
            
            return qty > 0m;
        }

        private static decimal ParseDecimal(string token)
        {
            if (string.IsNullOrWhiteSpace(token)) 
                return 0m;

            var cleaned = token
                .Replace("\u00A0", " ")
                .Replace(" ", "")
                .Replace(",", ".");

            if (decimal.TryParse(cleaned, 
                System.Globalization.NumberStyles.Any, 
                System.Globalization.CultureInfo.InvariantCulture, 
                out var result))
            {
                return result;
            }

            return 0m;
        }
    }
}