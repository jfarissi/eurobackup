using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Backup.Web.Api.Server.Services.StoreChat;

namespace Backup.Web.Api.Server.Services.SalesAssistant
{
    /// <summary>
    /// Parcours projet guidé par familles catalogue (pas un dump de 6 produits mélangés).
    /// Ex. mur : structure → ciment/mortier → treillis/ferraillage → outillage.
    /// </summary>
    public enum WallGuideFamily
    {
        Structure,
        Binder,
        Reinforcement,
        Tools
    }

    public static class SalesProjectGuide
    {
        public static WallGuideFamily ResolveWallFamily(
            StoreChatSession session,
            string? userText,
            ProductSearchFilter? meta = null)
        {
            var text = (userText ?? string.Empty).ToLowerInvariant();
            var hints = string.Join(' ', meta?.TypeHints ?? session.SearchTypeHints).ToLowerInvariant();
            var hay = $"{text} {hints}";

            if (LooksLikeTools(hay))
                return WallGuideFamily.Tools;
            if (LooksLikeReinforcement(hay))
                return WallGuideFamily.Reinforcement;
            if (LooksLikeBinder(hay))
                return WallGuideFamily.Binder;
            if (LooksLikeStructure(hay))
                return WallGuideFamily.Structure;

            var cart = CartOnlyHay(session);
            var hasStructure = HasStructure(cart);
            var hasBinder = HasBinder(cart);
            var hasMesh = HasReinforcement(cart);

            // Suite naturelle après ajout panier (panier réel, pas MaterialHints).
            if (hasStructure && !hasBinder)
                return WallGuideFamily.Binder;
            if (hasStructure && hasBinder && !hasMesh)
                return WallGuideFamily.Reinforcement;
            if (hasStructure && hasBinder && hasMesh)
                return WallGuideFamily.Tools;

            return WallGuideFamily.Structure;
        }

        /// <summary>
        /// Message de parcours : où on en est + rayons catalogue concernés.
        /// </summary>
        public static string BuildWallChecklist(StoreChatSession session, WallGuideFamily focus)
        {
            var cart = CartOnlyHay(session);
            var hasStructure = HasStructure(cart);
            var hasBinder = HasBinder(cart);
            var hasMesh = HasReinforcement(cart);
            var hasTools = HasTools(cart);

            string Mark(bool done, WallGuideFamily family, string label, string aisle)
            {
                var here = family == focus ? " ← à choisir maintenant" : "";
                var state = done ? "✓" : "○";
                return $"{state} {label} — rayon : {aisle}{here}";
            }

            var sb = new StringBuilder();
            sb.AppendLine("Parcours chantier mur (une famille à la fois) :");
            sb.AppendLine(Mark(hasStructure, WallGuideFamily.Structure,
                "1. Structure (briques / blocs)", "Stenen etc. / Snelbouwstenen…"));
            sb.AppendLine(Mark(hasBinder, WallGuideFamily.Binder,
                "2. Ciment / mortier", "Cement en Mortels"));
            sb.AppendLine(Mark(hasMesh, WallGuideFamily.Reinforcement,
                "3. Treillis / ferraillage", "Zind & Grid · Net, IJzer en Toebehoren"));
            sb.Append(Mark(hasTools, WallGuideFamily.Tools,
                "4. Outillage pose", "Truelle, auge, niveau, gants…"));

            if (session.WallAreaM2 is > 0)
                sb.Append($"\nSurface estimée ~{session.WallAreaM2:0.##} m² — quantités préremplies sur la structure / le liant.");

            sb.Append("\nPrécisez une marque, un type (brique / bloc / ciment 25 kg…) ou ajoutez une référence au panier pour passer à l’étape suivante.");
            return sb.ToString().Trim();
        }

        public static string FocusLabel(WallGuideFamily family) => family switch
        {
            WallGuideFamily.Structure => "structure (briques / blocs)",
            WallGuideFamily.Binder => "ciment / mortier",
            WallGuideFamily.Reinforcement => "treillis / ferraillage",
            WallGuideFamily.Tools => "outillage",
            _ => "matériaux"
        };

        /// <summary>
        /// Demande de suite alors que la base (structure+liant) n’est pas complète
        /// → enchaîner sur la famille manquante, pas « plus de briques ».
        /// </summary>
        public static bool ShouldAdvanceIncompleteWall(
            string userMessage,
            StoreChatSession session)
        {
            if (!string.Equals(session.ActiveProjectDomainId, "wall_construction", StringComparison.OrdinalIgnoreCase))
                return false;
            if (SalesComplementRules.IsBaseComplete(session))
                return false;

            var lower = (userMessage ?? string.Empty).ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(lower))
                return false;

            return ContainsAny(lower,
                "autre", "autres", "encore", "suite", "ensuite", "après", "apres",
                "quoi d", "manque", "ajouter", "complement", "complément",
                "ciment", "cement", "mortier", "mortel", "treillis", "outil");
        }

        /// <summary>Panier uniquement — pour l’avancement du parcours.</summary>
        public static string CartOnlyHay(StoreChatSession session) =>
            string.Join(' ', session.Cart.Select(c => $"{c.Name} {c.Reference}")).ToLowerInvariant();

        public static string CartHay(StoreChatSession session) => CartOnlyHay(session);

        public static bool HasStructure(string hay) =>
            ContainsAny(hay,
                "brique", "baksteen", "blok", "bloc", "parpaing", "porotherm", "silka",
                "steen", "snelbouw", "boerkes", "kalkzand", "lijmblok", "gaten", "thermobrick");

        public static bool HasBinder(string hay) =>
            ContainsAny(hay, "ciment", "cement", "mortier", "mortel");

        public static bool HasReinforcement(string hay) =>
            ContainsAny(hay,
                "treillis", "wapening", "bewapeningsnet", "wapeningsnet", "wapeningsgaas",
                "mesh", "zind", "grid", "gaas", "ferraill", "betonijzer", "draad");

        public static bool HasTools(string hay) =>
            ContainsAny(hay,
                "truelle", "troffel", "niveau", "waterpas", "auge", "seau", "emmer",
                "kuip", "gant", "handschoen");

        private static bool LooksLikeStructure(string hay) =>
            ContainsAny(hay,
                "brique", "baksteen", "bloc", "blok", "parpaing", "snelbouw", "silka",
                "porotherm", "ytong", "cellenbeton", "lijmblok", "steen");

        private static bool LooksLikeBinder(string hay) =>
            ContainsAny(hay, "ciment", "cement", "mortier", "mortel", "liant", "metselspecie");

        private static bool LooksLikeReinforcement(string hay) =>
            ContainsAny(hay,
                "treillis", "mesh", "wapening", "ferraill", "zind", "grid", "gaas",
                "net ", "ijzer", "betonijzer", "bewapen");

        private static bool LooksLikeTools(string hay) =>
            ContainsAny(hay,
                "truelle", "troffel", "niveau", "waterpas", "auge", "seau", "emmer",
                "outil", "outillage", "gant", "handschoen", "kuip");

        private static bool ContainsAny(string hay, params string[] needles) =>
            needles.Any(n => hay.Contains(n, StringComparison.OrdinalIgnoreCase));
    }
}
