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

            var cart = CartOnlyHay(session);
            var hasStructure = HasStructure(cart);
            var hasBinder = HasBinder(cart);
            var hasMesh = HasReinforcement(cart);
            var hasTools = HasTools(cart);

            // Intention explicite uniquement si la famille n'est pas déjà couverte par le panier
            // (évite de rester bloqué sur « ciment » à cause des TypeHints de session).
            if (LooksLikeTools(hay) && !hasTools)
                return WallGuideFamily.Tools;
            if (LooksLikeReinforcement(hay) && !hasMesh)
                return WallGuideFamily.Reinforcement;
            if (LooksLikeBinder(hay) && !hasBinder)
                return WallGuideFamily.Binder;
            if (LooksLikeStructure(hay) && !hasStructure)
                return WallGuideFamily.Structure;

            // Suite naturelle d'après le panier réel.
            if (hasStructure && !hasBinder)
                return WallGuideFamily.Binder;
            if (hasStructure && hasBinder && !hasMesh)
                return WallGuideFamily.Reinforcement;
            if (hasStructure && hasBinder && hasMesh && !hasTools)
                return WallGuideFamily.Tools;

            // Tout est couvert : garder Tools comme focus « terminé » (checklist ✓ partout).
            if (hasStructure && hasBinder && hasMesh && hasTools)
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
                var complete = hasStructure && hasBinder && hasMesh && hasTools;
                var here = !complete && family == focus ? " ← à choisir maintenant" : "";
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

            if (hasStructure && hasBinder && hasMesh && hasTools)
                sb.Append("\nParcours mur complet — vous pouvez demander un devis ou passer commande.");
            else
                sb.Append("\nPrécisez une marque, un type (brique / bloc / ciment 25 kg…) ou ajoutez une référence au panier pour passer à l’étape suivante.");
            return sb.ToString().Trim();
        }

        /// <summary>True si structure + liant + ferraillage + outillage sont dans le panier.</summary>
        public static bool IsWallGuideComplete(StoreChatSession session)
        {
            var cart = CartOnlyHay(session);
            return HasStructure(cart) && HasBinder(cart) && HasReinforcement(cart) && HasTools(cart);
        }

        public static string FocusLabel(WallGuideFamily family) => family switch
        {
            WallGuideFamily.Structure => "structure (briques / blocs)",
            WallGuideFamily.Binder => "ciment / mortier",
            WallGuideFamily.Reinforcement => "treillis / ferraillage",
            WallGuideFamily.Tools => "outillage",
            _ => "matériaux"
        };

        public static bool ShouldContinueWallGuide(StoreChatSession session)
        {
            if (!string.Equals(session.ActiveProjectDomainId, "wall_construction", StringComparison.OrdinalIgnoreCase))
                return false;

            var cart = CartOnlyHay(session);
            if (!HasStructure(cart) || !HasBinder(cart))
                return true; // étapes 1-2

            return !HasReinforcement(cart) || !HasTools(cart);
        }

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
                "suivant", "ciment", "cement", "mortier", "mortel", "treillis", "outil",
                "déjà", "deja", "j'ai déjà", "j ai deja");
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

        public static bool HasReinforcement(string hay)
        {
            // Filets plâtre / cloison ≠ ferraillage mur maçonnerie.
            if (ContainsAny(hay, "gipsplaat", "gipsplaten", "pladur", "drywall"))
                return false;
            // Entretoises bétonnet ≠ treillis / Murfor.
            if (ContainsAny(hay, "afstandhouder", "afstandhouders")
                && !ContainsAny(hay, "murfor", "bewapeningsnet", "wapeningsnet", "lintvoeg", "betonijzer"))
                return false;

            return ContainsAny(hay,
                "murfor", "betonijzer", "wapeningsnet", "bewapeningsnet",
                "wapeningsgaas", "treillis", "zind", "metselwapen", "lintvoeg")
                   || (ContainsAny(hay, "betonnet")
                       && !ContainsAny(hay, "afstandhouder", "afstandhouders"))
                   || (ContainsAny(hay, "wapening", "gaas", "mesh")
                       && ContainsAny(hay, "murfor", "ytong", "metsel", "beton", "zind", "ijzer", "net,"));
        }

        public static bool HasTools(string hay) =>
            ContainsAny(hay,
                "truelle", "troffel", "truweel", "poliertruweel", "metseltroffel",
                "niveau", "waterpas", "auge", "seau", "emmer",
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
                "truelle", "troffel", "truweel", "poliertruweel", "metseltroffel",
                "niveau", "waterpas", "auge", "seau", "emmer",
                "outil", "outillage", "gant", "handschoen", "kuip");

        private static bool ContainsAny(string hay, params string[] needles) =>
            needles.Any(n => hay.Contains(n, StringComparison.OrdinalIgnoreCase));
    }
}
