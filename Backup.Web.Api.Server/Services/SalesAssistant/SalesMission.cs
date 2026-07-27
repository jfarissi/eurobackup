using System;
using System.Linq;
using Backup.Web.Api.Server.Services.StoreChat;

namespace Backup.Web.Api.Server.Services.SalesAssistant
{
    public enum SalesMissionKind
    {
        Project = 0,
        SimpleSku = 1
    }

    /// <summary>Contraintes sticky pour une recherche produit simple (ex. ampoule E27).</summary>
    public sealed class SalesSkuConstraints
    {
        /// <summary>ampoule | …</summary>
        public string? ProductKind { get; set; }
        /// <summary>e27 | e14 | gu10</summary>
        public string? Socket { get; set; }
        /// <summary>2700 | 4000 | 6500</summary>
        public string? Kelvin { get; set; }
    }

    /// <summary>
    /// Couche mission légère : SimpleSku (acheter une ampoule) vs Project (parcours chantier).
    /// </summary>
    public static class SalesMission
    {
        public static bool IsSimpleSku(StoreChatSession session) =>
            session.ActiveMission == SalesMissionKind.SimpleSku
            || string.Equals(session.ActiveMissionName, "simple_sku", StringComparison.OrdinalIgnoreCase);

        public static void DetectAndApply(StoreChatSession session, string text)
        {
            var lower = (text ?? string.Empty).ToLowerInvariant();

            // Soft refine pendant une mission SKU (ex. « bureau ») : garder la mission, enrichir kelvin.
            if (IsSimpleSku(session) && LooksLikeSkuRefine(lower) && !LooksLikeProject(lower))
            {
                ApplyKelvinFromText(session, lower);
                return;
            }

            if (LooksLikeSimpleLightingSku(lower))
            {
                session.ActiveMission = SalesMissionKind.SimpleSku;
                session.ActiveMissionName = "simple_sku";
                session.SuppressProjectGuide = true;
                session.SkuConstraints ??= new SalesSkuConstraints();
                session.SkuConstraints.ProductKind = "ampoule";
                session.SkuConstraints.Socket ??= DetectSocket(lower);
                ApplyKelvinFromText(session, lower);

                // Conserver le culot dans les hints catalogue.
                if (!string.IsNullOrWhiteSpace(session.SkuConstraints.Socket)
                    && !session.CatalogRefineHints.Contains(session.SkuConstraints.Socket, StringComparer.OrdinalIgnoreCase))
                    session.CatalogRefineHints.Add(session.SkuConstraints.Socket!);

                return;
            }

            // Nouvelle intention projet claire → sortir du mode SKU.
            if (LooksLikeProject(lower))
            {
                session.ActiveMission = SalesMissionKind.Project;
                session.ActiveMissionName = "project";
                session.SuppressProjectGuide = false;
                session.SkuConstraints = null;
            }
        }

        public static string EnrichSearchText(StoreChatSession session, string text)
        {
            if (!IsSimpleSku(session) || session.SkuConstraints is null)
                return text;

            var parts = new System.Collections.Generic.List<string>();
            if (!string.IsNullOrWhiteSpace(text))
                parts.Add(text.Trim());
            if (!string.IsNullOrWhiteSpace(session.SkuConstraints.ProductKind))
                parts.Add(session.SkuConstraints.ProductKind!);
            if (!string.IsNullOrWhiteSpace(session.SkuConstraints.Socket))
                parts.Add(session.SkuConstraints.Socket!);
            if (!string.IsNullOrWhiteSpace(session.SkuConstraints.Kelvin))
                parts.Add(session.SkuConstraints.Kelvin + "k");

            // Seed d’origine si refine court (« bureau »).
            if (!string.IsNullOrWhiteSpace(session.PendingRefineSeed)
                && text.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).Length <= 3)
                parts.Insert(0, session.PendingRefineSeed!);

            return string.Join(" ", parts.Distinct(StringComparer.OrdinalIgnoreCase));
        }

        private static bool LooksLikeSimpleLightingSku(string lower)
        {
            var lighting = ContainsAny(lower,
                "ampoule", "ampoules", "lampe", "lampes", "lamp", "bulb",
                "gloeilamp", "ledlamp", "led lamp");
            if (!lighting)
                return false;

            // Culot explicite → toujours SKU.
            if (DetectSocket(lower) != null)
                return true;

            // Formulation d’achat simple.
            return ContainsAny(lower,
                "je veux", "je cherche", "cherche", "besoin", "acheter",
                "ik zoek", "ik wil", "i want", "i need", "looking for");
        }

        private static bool LooksLikeSkuRefine(string lower)
        {
            if (string.IsNullOrWhiteSpace(lower))
                return false;
            return ContainsAny(lower,
                       "bureau", "salon", "chambre", "cuisine", "garage", "cave",
                       "woonkamer", "kantoor", "keuken", "buiten", "living", "office",
                       "blanc chaud", "blanc neutre", "blanc froid",
                       "warm wit", "koel wit", "2700", "4000", "6500")
                   || lower.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).Length <= 4;
        }

        private static bool LooksLikeProject(string lower) =>
            ContainsAny(lower,
                "construire", "mur", "peindre", "peinture m", "carrelage", "toiture",
                "réparer mon toit", "reparer mon toit", "matériaux pour", "materiaux pour",
                "salle de bain", "installer", "parcours",
                "muur bouwen", "dak repareren", "build a wall");

        private static string? DetectSocket(string lower)
        {
            if (ContainsAny(lower, "e27")) return "e27";
            if (ContainsAny(lower, "e14")) return "e14";
            if (ContainsAny(lower, "gu10")) return "gu10";
            if (ContainsAny(lower, "gu5.3", "mr16")) return "gu10";
            return null;
        }

        private static void ApplyKelvinFromText(StoreChatSession session, string lower)
        {
            session.SkuConstraints ??= new SalesSkuConstraints();
            string? k = null;
            if (ContainsAny(lower, "2700", "blanc chaud", "warm wit", "warm white", "salon", "chambre", "ambiance", "woonkamer", "living"))
                k = "2700";
            else if (ContainsAny(lower, "4000", "blanc neutre", "neutre", "neutral", "neutraal", "bureau", "cuisine", "kantoor", "keuken", "office", "travail"))
                k = "4000";
            else if (ContainsAny(lower, "6500", "blanc froid", "koel wit", "cool white", "garage", "cave", "daglicht", "daylight", "extérieur", "exterieur", "buiten"))
                k = "6500";

            if (k is null)
                return;

            session.SkuConstraints.Kelvin = k;
            var hint = k + "k";
            if (!session.CatalogRefineHints.Contains(hint, StringComparer.OrdinalIgnoreCase))
                session.CatalogRefineHints.Add(hint);
        }

        private static bool ContainsAny(string hay, params string[] needles) =>
            needles.Any(n => hay.Contains(n, StringComparison.OrdinalIgnoreCase));
    }
}
