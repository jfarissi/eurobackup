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
                var typePart = typeLabel != null ? $" {typeLabel}" : SalesLocale.T(session, "product_word");
                var brandPart = brand != null ? $" {brand}" : "";
                var weightPart = weightLabel != null ? SalesLocale.T(session, "in_weight", weightLabel) : "";
                var ask = brand != null && typeLabel != null
                    ? SalesLocale.T(session, "weight_not_found_ask", brand, typeLabel)
                    : SalesLocale.T(session, "weight_not_found_refine");
                return SalesLocale.T(session, "weight_not_found", typePart, brandPart, weightPart, ask);
            }

            if (products.Count > 0)
            {
                string intro;
                if (!string.IsNullOrWhiteSpace(vagueFollowUp))
                {
                    intro = vagueFollowUp + "\n\n" + SalesLocale.T(session, "here_examples");
                }
                // Calcul C# (peinture m²/L, mur briques…) = voix métier : toujours prioritaire.
                else if (!string.IsNullOrWhiteSpace(calc))
                {
                    intro = calc;
                }
                else if (meta.IsYesNoBrandQuestion
                         && meta.Outcome is ProductSearchOutcome.BrandAndType or ProductSearchOutcome.BrandOnly)
                {
                    var samples = string.Join(", ", products.Take(2).Select(p => p.Name));
                    var typePart = typeLabel != null ? $"{typeLabel} " : "";
                    var samplePart = samples.Length > 0
                        ? SalesLocale.Of(session) switch
                        {
                            SalesLocale.Nl => $", o.a.: {samples}.",
                            SalesLocale.En => $", including: {samples}.",
                            _ => $", notamment : {samples}."
                        }
                        : ".";
                    intro = SalesLocale.T(session, "yes_brand", brand, typePart, samplePart);

                    if (meta.WeightKg is null or <= 0)
                        intro += "\n\n" + SalesLocale.T(session, "ask_weight");
                }
                else if (meta.Outcome == ProductSearchOutcome.BrandAndType)
                {
                    intro = weightLabel != null
                        ? SalesLocale.T(session, "here_n_brand_type_weight", products.Count, brand, typeLabel, weightLabel)
                        : SalesLocale.T(session, "here_n_brand_type", products.Count, brand, typeLabel);
                }
                else if (meta.Outcome == ProductSearchOutcome.BrandWithoutType)
                {
                    intro = SalesLocale.T(session, "brand_type_missing", typeLabel, brand);
                }
                else if (meta.Outcome == ProductSearchOutcome.BrandOnly)
                {
                    intro = weightLabel != null
                        ? SalesLocale.T(session, "here_n_brand_weight", products.Count, brand, weightLabel)
                        : SalesLocale.T(session, "here_n_brand", products.Count, brand);
                }
                else
                {
                    var domainLabel = SalesLocale.DomainDisplay(
                        session, session.ActiveProjectDomainId, session.ActiveProjectDomainLabel);
                    intro = string.IsNullOrWhiteSpace(domainLabel)
                        ? SalesLocale.T(session, "here_n_catalog", products.Count)
                        : SalesLocale.T(session, "here_n_domain", products.Count, domainLabel);
                }

                if (meta.TotalMatches > products.Count && string.IsNullOrWhiteSpace(vagueFollowUp))
                {
                    if (meta.WallGuideFamily is { } family)
                    {
                        intro += "\n" + SalesLocale.T(session, "wall_step_matches",
                            SalesProjectGuide.FocusLabel(family), products.Count, meta.TotalMatches);
                    }
                    else
                    {
                        intro += "\n" + SalesLocale.T(session, "display_best", products.Count, meta.TotalMatches);
                    }
                }

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
                        intro += "\n\n" + SalesLocale.T(session, "ask_weight");
                    }
                    else
                    {
                        intro += "\n\n" + SalesLocale.T(session, "adjust_qty");
                    }

                    return intro.Trim();
                }

                if (!string.IsNullOrWhiteSpace(vagueFollowUp))
                    intro += "\n\n" + vagueFollowUp; // already localized upstream when possible
                else
                    intro += "\n\n" + SalesLocale.T(session, "adjust_qty");

                if (string.IsNullOrWhiteSpace(vagueFollowUp)
                    && !string.IsNullOrWhiteSpace(aiReply)
                    && aiReply!.Length < 400
                    && !LooksLikeInventedProductList(aiReply)
                    && !LooksLikeHallucinatedBrandClaim(aiReply, products, brand))
                {
                    intro = (!string.IsNullOrWhiteSpace(calc) ? calc + "\n\n" : "")
                            + aiReply.Trim()
                            + "\n\n" + SalesLocale.T(session, "qty_prefilled");
                }

                if (meta.WallGuideFamily is { } wallFamily
                    && string.Equals(session.ActiveProjectDomainId, "wall_construction", StringComparison.OrdinalIgnoreCase))
                {
                    intro = intro.TrimEnd()
                            + "\n\n"
                            + SalesProjectGuide.BuildWallChecklist(session, wallFamily);
                }

                return intro.Trim();
            }

            if (meta.Outcome == ProductSearchOutcome.BrandNotFound && !string.IsNullOrWhiteSpace(brand))
            {
                return SalesLocale.T(session, "brand_not_found", brand);
            }

            if (meta.Outcome == ProductSearchOutcome.BrandWithoutType
                && !string.IsNullOrWhiteSpace(brand)
                && typeLabel != null)
            {
                return SalesLocale.T(session, "brand_present_no_type", brand, typeLabel);
            }

            if (!string.IsNullOrWhiteSpace(calc))
                return calc + "\n\n" + SalesLocale.T(session, "no_matching_materials");

            if (!string.IsNullOrWhiteSpace(aiReply) && !LooksLikeInventedProductList(aiReply))
                return aiReply!.Trim();

            return SalesLocale.T(session, "no_matching_product");
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
                && (session.PaintAreaM2 is > 0
                    || ContainsIgnoreCase(text, "acryl")
                    || ContainsIgnoreCase(text, "latex")
                    || ContainsIgnoreCase(text, "sous-couche")
                    || ContainsIgnoreCase(text, "rouleau")
                    || ContainsIgnoreCase(text, "muurverf")
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
                "electrical" => SalesLocale.T(session, "vague_electrical"),
                "painting" => SalesLocale.T(session, "vague_painting"),
                "tiling" => SalesLocale.T(session, "vague_tiling"),
                "plumbing" => SalesLocale.T(session, "vague_plumbing"),
                "garden_landscaping" or "garden_cleaning" or "garden_maintenance" =>
                    SalesLocale.T(session, "vague_garden"),
                "wall_construction" => SalesLocale.T(session, "vague_wall"),
                _ => null
            };
        }

        public string BuildCalculationSummary(StoreChatSession session)
        {
            if (string.Equals(session.ActiveProjectDomainId, "painting", StringComparison.OrdinalIgnoreCase)
                && session.PaintAreaM2 is > 0)
            {
                var area = session.PaintAreaM2.Value;
                // ~10 m²/L/couche, 2 couches.
                var liters = Math.Max(1, Math.Ceiling(area / 5m));
                var detail = !string.IsNullOrWhiteSpace(session.ProjectTypeHint)
                    ? session.ProjectTypeHint + "\n"
                    : "";
                return detail + SalesLocale.T(session, "paint_surface", area, liters);
            }

            if (!string.Equals(session.ActiveProjectDomainId, "wall_construction", StringComparison.OrdinalIgnoreCase))
                return string.Empty;

            if (session.WallLengthM is not > 0 || session.WallHeightM is not > 0 || session.WallAreaM2 is not > 0)
                return string.Empty;

            var wallArea = session.WallAreaM2!.Value;
            var bricks = Math.Ceiling(wallArea * SalesWallEstimates.BricksPerM2);
            var parpaings = Math.Ceiling(wallArea * SalesWallEstimates.ParpaingsPerM2);
            var mortarBags = Math.Ceiling(wallArea * SalesWallEstimates.MortarKgPerM2 / SalesWallEstimates.DefaultBagKg);

            return SalesLocale.T(session, "wall_estimate",
                session.WallLengthM, session.WallHeightM, wallArea, bricks, parpaings, mortarBags,
                SalesWallEstimates.DefaultBagKg);
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
