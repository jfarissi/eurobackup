using System;
using System.Collections.Generic;
using System.Linq;
using Backup.Web.Api.Server.Services.StoreChat;

namespace Backup.Web.Api.Server.Services.SalesAssistant
{
    public interface ISalesDeterministicReply
    {
        string Compose(
            string? aiReply,
            IReadOnlyList<StoreChatProductSuggestionDto> products,
            StoreChatSession session,
            ProductSearchFilter meta,
            string? userText = null);

        string BuildCalculationSummary(StoreChatSession session);
        string? BuildVagueDomainFollowUp(StoreChatSession session, ProductSearchFilter meta, string? userText);
    }

    public sealed class SalesDeterministicReply : ISalesDeterministicReply
    {
        public string Compose(
            string? aiReply,
            IReadOnlyList<StoreChatProductSuggestionDto> products,
            StoreChatSession session,
            ProductSearchFilter meta,
            string? userText = null)
        {
            var calc = BuildCalculationSummary(session);
            var brand = meta.Brand;
            var typeLabel = meta.TypeHints.Count > 0 ? string.Join(" / ", meta.TypeHints) : null;
            var weightLabel = meta.WeightKg is > 0 ? $"{meta.WeightKg:0.##} kg" : null;
            var vagueFollowUp = BuildVagueDomainFollowUp(session, meta, userText);

            if (meta.Outcome == ProductSearchOutcome.WeightNotFound)
            {
                return $"Je n'ai pas trouvé de"
                       + (typeLabel != null ? $" {typeLabel}" : " produit")
                       + (brand != null ? $" {brand}" : "")
                       + (weightLabel != null ? $" en {weightLabel}" : "")
                       + " dans le catalogue."
                       + (brand != null && typeLabel != null
                           ? $" Souhaitez-vous voir d'autres formats {brand} ({typeLabel}) ?"
                           : " Affinez marque, type ou poids.");
            }

            if (products.Count > 0)
            {
                string intro;
                if (!string.IsNullOrWhiteSpace(vagueFollowUp))
                {
                    intro = vagueFollowUp + "\n\nVoici quelques exemples en attendant :";
                }
                else if (!string.IsNullOrWhiteSpace(calc) && meta.Outcome is ProductSearchOutcome.Domain)
                {
                    intro = calc;
                }
                else if (meta.IsYesNoBrandQuestion
                         && meta.Outcome is ProductSearchOutcome.BrandAndType or ProductSearchOutcome.BrandOnly)
                {
                    var samples = string.Join(", ", products.Take(2).Select(p => p.Name));
                    intro = $"Oui. {brand} propose "
                            + (typeLabel != null ? $"du {typeLabel} " : "ces produits ")
                            + "dans notre catalogue"
                            + (samples.Length > 0 ? $", notamment : {samples}." : ".");

                    if (meta.WeightKg is null or <= 0)
                        intro += "\n\nCherchez-vous un petit format (ex. 5 kg) ou un sac chantier (25 kg) ?";
                }
                else if (meta.Outcome == ProductSearchOutcome.BrandAndType)
                {
                    intro = weightLabel != null
                        ? $"Voici {products.Count} référence(s) {brand} — {typeLabel} — {weightLabel}."
                        : $"Voici {products.Count} référence(s) {brand} liées à « {typeLabel} ».";
                }
                else if (meta.Outcome == ProductSearchOutcome.BrandWithoutType)
                {
                    intro = $"Je n'ai pas trouvé de {typeLabel} de la marque {brand} dans le catalogue. "
                            + $"Voici d'autres produits {brand} :";
                }
                else if (meta.Outcome == ProductSearchOutcome.BrandOnly)
                {
                    intro = weightLabel != null
                        ? $"Voici {products.Count} produit(s) {brand} en {weightLabel}."
                        : $"Voici {products.Count} produit(s) de la marque {brand}.";
                }
                else
                {
                    intro = $"Voici {products.Count} produit(s) du catalogue"
                            + (string.IsNullOrWhiteSpace(session.ActiveProjectDomainLabel)
                                ? "."
                                : $" pour {session.ActiveProjectDomainLabel}.");
                }

                if (meta.TotalMatches > products.Count && string.IsNullOrWhiteSpace(vagueFollowUp))
                    intro += $"\n(Affichage des {products.Count} meilleures sur {meta.TotalMatches} — précisez pour affiner.)";

                var isBrandPath = meta.Outcome is ProductSearchOutcome.BrandOnly
                    or ProductSearchOutcome.BrandAndType
                    or ProductSearchOutcome.BrandWithoutType
                    || meta.IsYesNoBrandQuestion;

                if (isBrandPath)
                {
                    if (meta.IsYesNoBrandQuestion)
                        return intro.Trim();

                    if (meta.WeightKg is null or <= 0
                        && meta.Outcome is ProductSearchOutcome.BrandAndType or ProductSearchOutcome.BrandOnly)
                    {
                        intro += "\n\nCherchez-vous un petit format (ex. 5 kg) ou un sac chantier (25 kg) ?";
                    }
                    else
                    {
                        intro += "\n\nAjustez les quantités puis ajoutez au panier / devis / commande.";
                    }

                    return intro.Trim();
                }

                if (!string.IsNullOrWhiteSpace(vagueFollowUp))
                    intro += "\n\nRépondez avec le type souhaité (ex. « ampoules »), ou ajoutez un exemple au panier.";
                else
                    intro += "\n\nAjustez les quantités puis ajoutez au panier / devis / commande.";

                if (string.IsNullOrWhiteSpace(vagueFollowUp)
                    && !string.IsNullOrWhiteSpace(aiReply)
                    && aiReply!.Length < 400
                    && !LooksLikeInventedProductList(aiReply)
                    && !LooksLikeHallucinatedBrandClaim(aiReply, products, brand))
                {
                    intro = (!string.IsNullOrWhiteSpace(calc) ? calc + "\n\n" : "")
                            + aiReply.Trim()
                            + "\n\nLes quantités proposées sont préremplies dans le tableau.";
                }

                return intro.Trim();
            }

            if (meta.Outcome == ProductSearchOutcome.BrandNotFound && !string.IsNullOrWhiteSpace(brand))
            {
                return $"Je n'ai trouvé aucun produit de la marque {brand} dans le catalogue. "
                       + "Vérifiez l'orthographe ou essayez une autre marque / un type de produit.";
            }

            if (meta.Outcome == ProductSearchOutcome.BrandWithoutType
                && !string.IsNullOrWhiteSpace(brand)
                && typeLabel != null)
            {
                return $"La marque {brand} est présente, mais je n'ai pas de {typeLabel} {brand} dans le catalogue. "
                       + "Précisez un autre type (plâtre, plaque, colle…) ou une autre marque.";
            }

            if (!string.IsNullOrWhiteSpace(calc))
                return calc + "\n\nJe n'ai pas trouvé de parpaings/briques/mortier/ciment correspondants dans le catalogue. Affinez avec un matériau précis.";

            if (!string.IsNullOrWhiteSpace(aiReply) && !LooksLikeInventedProductList(aiReply))
                return aiReply!.Trim();

            return "Je n'ai pas trouvé de produit correspondant dans le catalogue. "
                   + "Indiquez un matériau ou une marque précise (ex. Knauf, parpaing, brique, mortier, ciment).";
        }

        public string? BuildVagueDomainFollowUp(
            StoreChatSession session,
            ProductSearchFilter meta,
            string? userText)
        {
            if (!string.IsNullOrWhiteSpace(meta.Brand) || meta.TypeHints.Count > 0 || meta.WeightKg is > 0)
                return null;
            if (!string.IsNullOrWhiteSpace(BuildCalculationSummary(session)))
                return null;

            var domain = session.ActiveProjectDomainId;
            if (string.IsNullOrWhiteSpace(domain))
                return null;

            var text = (userText ?? string.Empty).ToLowerInvariant();

            if (domain == "electrical"
                && (IsLightingQuery(text)
                    || ContainsIgnoreCase(text, "prise")
                    || ContainsIgnoreCase(text, "interrupteur")
                    || ContainsIgnoreCase(text, "câble")
                    || ContainsIgnoreCase(text, "cable")
                    || ContainsIgnoreCase(text, "tableau")))
                return null;

            if (domain == "painting"
                && (ContainsIgnoreCase(text, "acryl")
                    || ContainsIgnoreCase(text, "latex")
                    || ContainsIgnoreCase(text, "sous-couche")
                    || ContainsIgnoreCase(text, "rouleau")
                    || ContainsIgnoreCase(text, "blanc")))
                return null;

            if (IsGardenDomain(domain)
                && (ContainsIgnoreCase(text, "tondeuse")
                    || ContainsIgnoreCase(text, "dalle")
                    || ContainsIgnoreCase(text, "nettoyer")
                    || ContainsIgnoreCase(text, "aménag")
                    || ContainsIgnoreCase(text, "amenag")))
                return null;

            var tokenCount = text.Split(new[] { ' ', ',', '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries).Length;
            var looksBroad = tokenCount <= 6
                             || ContainsIgnoreCase(text, "produit")
                             || ContainsIgnoreCase(text, "cherche")
                             || text.Trim() is "electricite" or "électricité" or "peinture" or "jardin"
                                 or "plomberie" or "carrelage";

            if (!looksBroad)
                return null;

            return domain switch
            {
                "electrical" =>
                    "Le rayon électricité est large. Que cherchez-vous exactement : ampoules / LED, prises & interrupteurs, câbles, ou tableaux / disjoncteurs ?",
                "painting" =>
                    "Pour la peinture : intérieur ou extérieur ? Peinture murale, sous-couche, lasure, ou outils (rouleau / pinceaux) ?",
                "tiling" =>
                    "Carrelage : sol ou mur ? Format / couleur, ou plutôt colle et joints ?",
                "plumbing" =>
                    "Plomberie : robinetterie, PVC / tuyaux, évacuation, ou accessoires (joints, colliers) ?",
                "garden_landscaping" or "garden_cleaning" or "garden_maintenance" =>
                    "Jardin : aménagement (dalles, clôture), entretien (tondeuse, haie), ou nettoyage (souffleur, sacs) ?",
                "wall_construction" =>
                    "Pour votre mur : briques, blocs / parpaings, ou mortier / ciment ?",
                _ => null
            };
        }

        public string BuildCalculationSummary(StoreChatSession session)
        {
            if (session.WallLengthM is not > 0 || session.WallHeightM is not > 0 || session.WallAreaM2 is not > 0)
                return string.Empty;

            var area = session.WallAreaM2!.Value;
            var bricks = Math.Ceiling(area * SalesWallEstimates.BricksPerM2);
            var parpaings = Math.Ceiling(area * SalesWallEstimates.ParpaingsPerM2);
            var mortarBags = Math.Ceiling(area * SalesWallEstimates.MortarKgPerM2 / SalesWallEstimates.DefaultBagKg);

            return $"Mur {session.WallLengthM:0.##} m × {session.WallHeightM:0.##} m → surface ≈ {area:0.##} m².\n"
                   + $"Estimations (ordre de grandeur) : ~{bricks:0} briques, ou ~{parpaings:0} parpaings, "
                   + $"et ~{mortarBags:0} sac(s) de mortier/ciment ({SalesWallEstimates.DefaultBagKg:0} kg).";
        }

        private static bool LooksLikeInventedProductList(string reply)
        {
            var lower = reply.ToLowerInvariant();
            return lower.Contains("voici quelques suggestions")
                   || lower.Contains("griffes pour murs")
                   || lower.Contains("griffe pour murs")
                   || lower.Contains("gamme de produits qui incluent")
                   || lower.Contains("ciments pour plâtre")
                   || lower.Contains("ciments pour mortier")
                   || lower.Contains("ciments pour béton")
                   || lower.Contains("ciments pour beton")
                   || (lower.Contains("matériaux suivants") && lower.Contains("*"))
                   || (lower.Contains("tels que") && lower.Contains("*"));
        }

        private static bool LooksLikeHallucinatedBrandClaim(
            string reply,
            IReadOnlyList<StoreChatProductSuggestionDto> products,
            string? brand)
        {
            if (string.IsNullOrWhiteSpace(brand))
                return false;

            var lower = reply.ToLowerInvariant();
            var brandLower = brand.ToLowerInvariant();
            if (!lower.Contains(brandLower))
                return false;

            return (lower.Contains("ciment") || lower.Contains("cement"))
                   && products.All(p => !MatchesTypeHints(
                       new ScoredProduct
                       {
                           Name = p.Name,
                           Brand = p.Brand,
                           MainTypeName = p.Category,
                           TypeName = p.Category,
                           SubTypeName = p.Category
                       },
                       new List<string> { "ciment" }));
        }

        private static bool MatchesTypeHints(ScoredProduct product, IReadOnlyList<string> typeHints)
        {
            if (typeHints.Count == 0)
                return true;

            var haystack = $"{product.Name} {product.Name2} {product.Brand} {product.MainTypeName} {product.TypeName} {product.SubTypeName}"
                .ToLowerInvariant();

            return typeHints.Any(hint =>
                SalesMaterialLexicon.ExpandTypeHintTerms(hint)
                    .Select(x => x.ToLowerInvariant())
                    .Any(key => haystack.Contains(key, StringComparison.OrdinalIgnoreCase)));
        }

        private static bool ContainsIgnoreCase(string? haystack, string needle) =>
            !string.IsNullOrWhiteSpace(haystack)
            && haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);

        private static bool IsLightingQuery(string text)
        {
            var lower = (text ?? string.Empty).ToLowerInvariant();
            return ContainsIgnoreCase(lower, "ampoule")
                   || ContainsIgnoreCase(lower, "lampe")
                   || ContainsIgnoreCase(lower, "lampes")
                   || ContainsIgnoreCase(lower, "bulb")
                   || ContainsIgnoreCase(lower, "gloeilamp")
                   || ContainsIgnoreCase(lower, "spaarlamp")
                   || ContainsIgnoreCase(lower, "lampje")
                   || ContainsIgnoreCase(lower, "e27")
                   || ContainsIgnoreCase(lower, "e14")
                   || ContainsIgnoreCase(lower, "gu10")
                   || ContainsIgnoreCase(lower, "halogène")
                   || ContainsIgnoreCase(lower, "halogene");
        }

        private static bool IsGardenDomain(string? domainId) =>
            domainId is "garden_cleaning" or "garden_landscaping" or "garden_maintenance";

        private sealed class ScoredProduct
        {
            public string? Name { get; set; }
            public string? Name2 { get; set; }
            public string? Brand { get; set; }
            public string? MainTypeName { get; set; }
            public string? TypeName { get; set; }
            public string? SubTypeName { get; set; }
        }
    }
}
