using System;
using System.Collections.Generic;
using System.Linq;
using Backup.Web.Api.Server.Services.StoreChat;

namespace Backup.Web.Api.Server.Services.SalesAssistant
{
    /// <summary>
    /// Règles métier C# pour proposer des compléments — 0 LLM, 0 dictionnaire magique.
    /// </summary>
    public static class SalesComplementRules
    {
        private static readonly SalesRecommendationEngine Reco = new();

        /// <summary>
        /// Contexte justifie de proposer des compléments (panier + base + manquants).
        /// </summary>
        public static bool ShouldOfferComplements(StoreChatSession session)
        {
            if (session.Cart.Count == 0)
                return false;

            if (session.PendingComplementHints.Count > 0 || session.AwaitingComplementConfirm)
                return true;

            if (!IsBaseComplete(session))
                return false;

            return GetMissingHints(session).Count > 0;
        }

        /// <summary>
        /// Base chantier complète d’après le panier (+ hints projet).
        /// </summary>
        public static bool IsBaseComplete(StoreChatSession session)
        {
            var domain = session.ActiveProjectDomainId ?? string.Empty;
            var hay = CartHaystack(session);

            return domain switch
            {
                "wall_construction" => HasStructureMaterial(hay) && HasBinderMaterial(hay),
                "tiling" => HasTilingSurface(hay) && HasTilingAdhesive(hay),
                // Peinture murale dans le panier = base ; sous-couche / rouleau / ruban = compléments.
                "painting" => HasPaint(hay),
                _ => session.Project.HasStructureHint && session.Project.HasBinderHint
            };
        }

        /// <summary>Hints de recherche pour compléments encore absents du panier.</summary>
        public static IReadOnlyList<string> GetMissingHints(StoreChatSession session)
        {
            if (session.PendingComplementHints.Count > 0)
                return session.PendingComplementHints.ToList();

            var cartAsProducts = session.Cart.Select(c => new StoreChatProductSuggestionDto
            {
                ProductId = c.ErpProductId.ToString(),
                Name = c.Name,
                Category = c.Reference
            }).ToList();

            return Reco.SuggestComplements(session, cartAsProducts)
                .Select(m => m.SearchHint ?? m.Label)
                .Where(h => !string.IsNullOrWhiteSpace(h))
                .Select(h => h!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// Si le contexte le permet et le message demande une suite → CartComplements.
        /// Null = laisser le détecteur standard décider.
        /// </summary>
        public static GuidedSalesIntent? TryOverrideIntent(string userMessage, StoreChatSession session)
        {
            if (!ShouldOfferComplements(session))
                return null;

            var lower = (userMessage ?? string.Empty).ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(lower))
                return null;

            if (HasContinuationMarker(lower) || AsksForSpecificMissing(lower, session) || AsksGenericProducts(lower))
                return GuidedSalesIntent.CartComplements;

            return null;
        }

        /// <summary>Correction silencieuse MoreProducts → CartComplements.</summary>
        public static bool ShouldRedirectMoreProductsToComplements(
            GuidedSalesIntent intent,
            string userMessage,
            StoreChatSession session)
        {
            if (intent != GuidedSalesIntent.MoreProducts)
                return false;
            if (!ShouldOfferComplements(session))
                return false;

            var lower = (userMessage ?? string.Empty).ToLowerInvariant();
            return HasContinuationMarker(lower)
                   || AsksGenericProducts(lower)
                   || AsksForSpecificMissing(lower, session)
                   || session.Cart.Count > 0;
        }

        private static bool HasContinuationMarker(string lower)
        {
            // Marqueurs courts FR/NL/EN — renforcent le contexte, ne décident pas seuls.
            string[] phrases =
            {
                "quoi d'autre", "quoi d autre", "quoi encore", "wat anders", "what else",
                "il me faut", "il me manque", "il manque", "me manque",
                "autres produits", "autre produit", "d'autres produits", "d autres produits",
                "encore des", "produits en plus", "à ajouter", "a ajouter", "ajouter au panier",
                "ajouter a mon panier", "ajouter à mon panier",
                "complément", "complement", "accessoire", "outillage", "outils", "outil",
                "supplément", "supplement", "ander", "andere", "ontbreekt", "nodig",
                "c'est bon", "cest bon", "besoin d",
                "et maintenant", "et ensuite", "et après", "et apres", "quoi maintenant",
                "et la suite", "la suite"
            };
            if (phrases.Any(p => lower.Contains(p, StringComparison.OrdinalIgnoreCase)))
                return true;

            // Mots courts : exiger une demande de suite (autres / encore / manque…)
            return ContainsWord(lower, "autres")
                   || ContainsWord(lower, "autre")
                   || ContainsWord(lower, "encore")
                   || ContainsWord(lower, "manque")
                   || ContainsWord(lower, "reste")
                   || ContainsWord(lower, "other")
                   || ContainsWord(lower, "else")
                   || ContainsWord(lower, "missing")
                   || ContainsWord(lower, "nog");
        }

        private static bool AsksGenericProducts(string lower) =>
            lower.Contains("produit", StringComparison.OrdinalIgnoreCase)
            || lower.Contains("product", StringComparison.OrdinalIgnoreCase)
            || lower.Contains("artikel", StringComparison.OrdinalIgnoreCase);

        private static bool AsksForSpecificMissing(string lower, StoreChatSession session)
        {
            foreach (var hint in GetMissingHints(session))
            {
                if (hint.Length >= 3 && lower.Contains(hint, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static string CartHaystack(StoreChatSession session)
        {
            var cart = string.Join(' ', session.Cart.Select(c => c.Name));
            var hints = string.Join(' ', session.MaterialHints);
            return $"{cart} {hints}".ToLowerInvariant();
        }

        private static bool HasStructureMaterial(string hay) =>
            ContainsAny(hay,
                "brique", "baksteen", "blok", "bloc", "parpaing", "porotherm", "silka",
                "steen", "snelbouw", "boerkes", "kalkzand", "lijmblok", "gaten");

        private static bool HasBinderMaterial(string hay) =>
            ContainsAny(hay, "ciment", "cement", "mortier", "mortel");

        private static bool HasTilingSurface(string hay) =>
            ContainsAny(hay, "carrelage", "tegel", "faïence", "faience", "carreau");

        private static bool HasTilingAdhesive(string hay) =>
            ContainsAny(hay, "colle", "lijm", "mortier", "mortel", "tegellijm");

        private static bool HasPaint(string hay) =>
            ContainsAny(hay, "peinture", "verf", "latex", "lasurer");

        private static bool HasPrimer(string hay) =>
            ContainsAny(hay, "primaire", "primer", "sous-couche", "sous couche", "grondverf", "undercoat");

        private static bool ContainsAny(string hay, params string[] needles) =>
            needles.Any(n => hay.Contains(n, StringComparison.OrdinalIgnoreCase));

        private static bool ContainsWord(string hay, string word) =>
            System.Text.RegularExpressions.Regex.IsMatch(
                hay,
                $@"\b{System.Text.RegularExpressions.Regex.Escape(word)}\b",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);
    }
}
