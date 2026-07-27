using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Backup.Web.Api.Server.Services.StoreChat;

namespace Backup.Web.Api.Server.Services.SalesAssistant
{
    /// <summary>
    /// Affinage avant tableau quand le catalogue est trop large (tous projets).
    /// </summary>
    public static class SalesCatalogRefine
    {
        public const int MatchThreshold = 30;

        public static bool ShouldGate(StoreChatSession session, string text, ProductSearchFilter meta)
        {
            if (meta.TotalMatches < MatchThreshold)
                return false;

            // Déjà affiné ou signal fort (marque, poids, surface, kelvin…).
            if (HasStrongSignal(session, text, meta))
                return false;

            return true;
        }

        public static string BuildPrompt(StoreChatSession session, ProductSearchFilter meta)
        {
            var count = meta.TotalMatches;
            var domain = session.ActiveProjectDomainId ?? string.Empty;
            var key = ResolvePromptKey(session, domain, meta);
            if (key == "catalog_refine_generic")
            {
                var label = SalesLocale.DomainDisplay(
                    session, domain, session.ActiveProjectDomainLabel) ?? "catalogue";
                return SalesLocale.T(session, key, count, label);
            }

            return SalesLocale.T(session, key, count);
        }

        public static bool IsSkip(string text)
        {
            var lower = (text ?? string.Empty).ToLowerInvariant();
            return ContainsAny(lower,
                "montre quand même", "montrez quand même", "montre quand meme", "montrez quand meme",
                "affiche quand même", "affichez quand même", "affiche quand meme", "affichez quand meme",
                "montre les produits", "montrez les produits", "affiche les produits", "affichez les produits",
                "sans affiner", "peu importe", "n'importe", "n importe",
                "toon toch", "toon de producten", "gewoon tonen",
                "show anyway", "show products", "just show", "skip refine");
        }

        /// <summary>
        /// Extrait critères d’affinage (tous domaines) et les stocke en sticky.
        /// Retourne true si au moins un hint a été ajouté ou si skip.
        /// </summary>
        public static bool TryApplyAnswer(StoreChatSession session, string text)
        {
            if (IsSkip(text))
            {
                session.AwaitingCatalogRefine = false;
                return true;
            }

            var hints = ExtractHints(text);
            if (hints.Count == 0)
                return false;

            foreach (var h in hints)
            {
                if (!session.CatalogRefineHints.Contains(h, StringComparer.OrdinalIgnoreCase))
                    session.CatalogRefineHints.Add(h);

                // Propager vers les filtres sticky catalogue (types / matériaux).
                if (!session.SearchTypeHints.Contains(h, StringComparer.OrdinalIgnoreCase)
                    && SalesMaterialLexicon.MaterialSynonyms.ContainsKey(h))
                    session.SearchTypeHints.Add(h);

                if (!session.MaterialHints.Contains(h, StringComparer.OrdinalIgnoreCase)
                    && SalesMaterialLexicon.MaterialSynonyms.ContainsKey(h))
                    session.MaterialHints.Add(h);
            }

            session.AwaitingCatalogRefine = false;
            return true;
        }

        public static bool HasEnoughRefine(string text) => ExtractHints(text).Count > 0;

        public static bool HasEnoughRefineHints(IReadOnlyList<string> hints) =>
            hints.Count > 0;

        public static List<string> ExtractHints(string text)
        {
            var lower = (text ?? string.Empty).ToLowerInvariant();
            var hints = new List<string>();

            // Éclairage
            if (ContainsAny(lower, "2700", "blanc chaud", "warm wit", "warm white"))
                Add(hints, "2700k");
            if (ContainsAny(lower, "4000", "blanc neutre", "neutre", "neutral", "neutraal"))
                Add(hints, "4000k");
            if (ContainsAny(lower, "6500", "blanc froid", "koel wit", "cool white", "daglicht", "daylight"))
                Add(hints, "6500k");

            if (!hints.Any(IsKelvinHint))
            {
                if (ContainsAny(lower, "pièce de vie", "piece de vie", "salon", "chambre", "ambiance", "woonkamer", "living"))
                    Add(hints, "2700k");
                else if (ContainsAny(lower, "bureau", "cuisine", "travail", "kantoor", "keuken", "werk"))
                    Add(hints, "4000k");
                else if (ContainsAny(lower, "extérieur", "exterieur", "garage", "cave", "buiten", "kelder"))
                    Add(hints, "6500k");
            }

            foreach (Match m in Regex.Matches(lower, @"\b(\d{1,2})\s*w\b"))
                Add(hints, m.Groups[1].Value + "w");

            // Lexique matériaux / types (tous rayons)
            foreach (var type in SalesMaterialLexicon.ExtractTypeHints(text))
                Add(hints, type);

            // Peinture / mur / jardin — signaux fréquents
            if (ContainsAny(lower, "intérieur", "interieur", "binnen", "indoor"))
                Add(hints, "intérieur");
            if (ContainsAny(lower, "extérieur", "exterieur", "buiten", "outdoor"))
                Add(hints, "extérieur");
            if (ContainsAny(lower, "sous-couche", "sous couche", "primer", "voorstrijk", "grondverf"))
                Add(hints, "sous-couche");
            if (ContainsAny(lower, "latex", "acryl", "muurverf", "peintu"))
                Add(hints, "peinture");
            if (ContainsAny(lower, "rouleau", "pinceau", "kwast", "verfroller"))
                Add(hints, "rouleau");
            if (ContainsAny(lower, "sol", "vloer", "floor"))
                Add(hints, "sol");
            if (ContainsAny(lower, "mur", "wand", "wall") && ContainsAny(lower, "carrel", "tegel", "faïence", "faience"))
                Add(hints, "carrelage");
            if (ContainsAny(lower, "prise", "stopcontact"))
                Add(hints, "prise");
            if (ContainsAny(lower, "interrupteur", "schakelaar"))
                Add(hints, "interrupteur");
            if (ContainsAny(lower, "goutti", "goot", "dakgoot"))
                Add(hints, "gouttière");
            if (ContainsAny(lower, "tuile", "dakpan"))
                Add(hints, "tuile");
            if (ContainsAny(lower, "tondeuse", "grasmaaier"))
                Add(hints, "tondeuse");
            if (ContainsAny(lower, "dalle", "tegel", "pavé", "pave"))
                Add(hints, "dalle");

            // Poids (ex. 25 kg)
            var weight = Regex.Match(lower, @"(\d+(?:[.,]\d+)?)\s*kg");
            if (weight.Success)
                Add(hints, weight.Groups[1].Value.Replace(',', '.') + "kg");

            // Tokens libres utiles (marque / type saisis à la main)
            foreach (var raw in text.Split(new[] { ' ', ',', ';', '.', '!', '?', '/', '\n', '\t', '\'', '’' },
                         StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var token = raw.Trim().ToLowerInvariant();
                if (token.Length < 3 || SalesMaterialLexicon.StopWords.Contains(token))
                    continue;
                if (token.Any(char.IsDigit) && !Regex.IsMatch(token, @"^\d+(?:[.,]\d+)?(?:kg|w|k|m)?$", RegexOptions.IgnoreCase))
                    continue;
                Add(hints, token);
            }

            return hints;
        }

        public static string BuildSearchText(StoreChatSession session, string answerText)
        {
            var seed = session.PendingRefineSeed ?? string.Empty;
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(seed))
                parts.Add(seed.Trim());
            if (!string.IsNullOrWhiteSpace(answerText) && !IsSkip(answerText))
                parts.Add(answerText.Trim());
            foreach (var h in session.CatalogRefineHints)
                parts.Add(h);
            return string.Join(" ", parts.Distinct(StringComparer.OrdinalIgnoreCase));
        }

        private static bool HasStrongSignal(StoreChatSession session, string text, ProductSearchFilter meta)
        {
            if (session.CatalogRefineHints.Count > 0)
                return true;
            if (!string.IsNullOrWhiteSpace(meta.Brand) || !string.IsNullOrWhiteSpace(session.PreferredBrand))
                return true;
            if (meta.WeightKg is > 0 || session.PreferredWeightKg is > 0)
                return true;
            if (session.PaintAreaM2 is > 0 || session.WallAreaM2 is > 0)
                return true;
            if (HasLightingRefine(text) || HasLightingRefineHints(session.CatalogRefineHints))
                return true;

            // Réponse d’affinage déjà dans le 1er message (type précis hors libellé générique).
            var extracted = ExtractHints(text);
            if (extracted.Any(h => IsKelvinHint(h) || IsWattHint(h) || h.EndsWith("kg", StringComparison.OrdinalIgnoreCase)))
                return true;

            return false;
        }

        private static string ResolvePromptKey(StoreChatSession session, string domain, ProductSearchFilter meta)
        {
            var text = string.Join(" ",
                session.PendingRefineSeed ?? "",
                string.Join(" ", meta.TypeHints ?? new List<string>()));

            if (SalesCatalogSearchTool.IsLightingQueryPublic(text)
                || meta.TypeHints.Any(h =>
                    h.Contains("ampoule", StringComparison.OrdinalIgnoreCase)
                    || h.Contains("lampe", StringComparison.OrdinalIgnoreCase)))
                return "catalog_refine_lighting";

            return domain.ToLowerInvariant() switch
            {
                "painting" => "catalog_refine_painting",
                "wall_construction" => "catalog_refine_wall",
                "tiling" => "catalog_refine_tiling",
                "plumbing" => "catalog_refine_plumbing",
                "roofing" => "catalog_refine_roofing",
                "electrical" => "catalog_refine_electrical",
                "garden_cleaning" or "garden_landscaping" or "garden_maintenance" => "catalog_refine_garden",
                _ => "catalog_refine_generic"
            };
        }

        private static bool HasLightingRefine(string text) =>
            ExtractHints(text).Any(h => IsKelvinHint(h) || IsWattHint(h));

        private static bool HasLightingRefineHints(IReadOnlyList<string> hints) =>
            hints.Any(h => IsKelvinHint(h) || IsWattHint(h));

        private static bool IsKelvinHint(string h) =>
            h.Contains("2700", StringComparison.OrdinalIgnoreCase)
            || h.Contains("4000", StringComparison.OrdinalIgnoreCase)
            || h.Contains("6500", StringComparison.OrdinalIgnoreCase)
            || (h.Length >= 5 && h.EndsWith("k", StringComparison.OrdinalIgnoreCase) && h.Any(char.IsDigit));

        private static bool IsWattHint(string h) =>
            Regex.IsMatch(h, @"^\d{1,2}w$", RegexOptions.IgnoreCase);

        private static void Add(List<string> hints, string value)
        {
            if (!hints.Contains(value, StringComparer.OrdinalIgnoreCase))
                hints.Add(value);
        }

        private static bool ContainsAny(string hay, params string[] needles) =>
            needles.Any(n => hay.Contains(n, StringComparison.OrdinalIgnoreCase));
    }
}
