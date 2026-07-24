using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Backup.Web.Api.Server.Services.StoreChat;

namespace Backup.Web.Api.Server.Services.SalesAssistant
{
    public static class SalesQuantityEstimator
    {
        public static void ApplySuggestedQuantities(
            List<StoreChatProductSuggestionDto> products,
            StoreChatSession session)
        {
            if (string.Equals(session.ActiveProjectDomainId, "painting", StringComparison.OrdinalIgnoreCase)
                && session.PaintAreaM2 is > 0)
            {
                var litersNeeded = Math.Max(1, Math.Ceiling(session.PaintAreaM2.Value / 5m));
                foreach (var product in products)
                {
                    var hay = $"{product.Name} {product.Category}".ToLowerInvariant();
                    if (ContainsAny(hay, "kwast", "pinceau", "roller", "rouleau", "ruban", "tape", "masking",
                            "sous-couche", "primer", "grondverf", "bac à peinture", "verfbak"))
                    {
                        product.SuggestedQuantity = 1;
                        continue;
                    }

                    var packL = TryParsePaintLiters(hay) ?? TryParsePaintKgAsLiters(hay);
                    if (packL is > 0)
                    {
                        var packs = Math.Max(1, Math.Ceiling(litersNeeded / packL.Value));
                        // Liste = alternatives : petits pots (ex. 1 L) → qté 1, pas 27 pots.
                        // Formats chantier (≥ 2,5 L) : nb de pots pour couvrir le chantier, plafonné.
                        if (packL.Value < 2.5m && packs > 4)
                            product.SuggestedQuantity = 1;
                        else
                            product.SuggestedQuantity = Math.Min(packs, 12);
                    }
                    else if (product.SuggestedQuantity is null or <= 0)
                    {
                        product.SuggestedQuantity = 1;
                    }
                }

                return;
            }

            // Quantités mur uniquement en projet construction — sinon qté = 1.
            if (!string.Equals(session.ActiveProjectDomainId, "wall_construction", StringComparison.OrdinalIgnoreCase)
                || session.WallAreaM2 is null or <= 0)
            {
                foreach (var product in products)
                {
                    if (product.SuggestedQuantity is null or <= 0)
                        product.SuggestedQuantity = 1;
                }

                return;
            }

            var area = session.WallAreaM2;
            foreach (var product in products)
            {
                if (product.SuggestedQuantity is > 1)
                    continue;
                var nameOnly = (product.Name ?? string.Empty).Split('—')[0].Trim().ToLowerInvariant();
                if (ContainsAny(nameOnly, "lijmblok", "kalkzandsteen", "betonblok", "snelbouwblok"))
                    product.SuggestedQuantity = EstimateQuantityForKind(SalesCatalogSearchTool.WallProductKind.Block, nameOnly, null, area);
                else if (ContainsAny(nameOnly, "baksteen", "snelbouwsteen", "brique", "brick"))
                    product.SuggestedQuantity = EstimateQuantityForKind(SalesCatalogSearchTool.WallProductKind.Brick, nameOnly, null, area);
                else if (ContainsAny(nameOnly, "mortel", "mortier", "cement", "ciment", "filler", "voegmortel"))
                    product.SuggestedQuantity = EstimateQuantityForKind(SalesCatalogSearchTool.WallProductKind.Mortar, nameOnly, product.Name, area);
            }
        }

        private static decimal EstimateQuantityForKind(
            SalesCatalogSearchTool.WallProductKind kind,
            string? name,
            string? name2,
            decimal? areaM2)
        {
            if (areaM2 is null or <= 0)
                return 1;

            var area = areaM2.Value;
            return kind switch
            {
                SalesCatalogSearchTool.WallProductKind.Block => Math.Max(1, Math.Ceiling(area * SalesWallEstimates.ParpaingsPerM2)),
                SalesCatalogSearchTool.WallProductKind.Brick => Math.Max(1, Math.Ceiling(area * SalesWallEstimates.BricksPerM2)),
                SalesCatalogSearchTool.WallProductKind.Mortar => EstimateMortarBags(name, name2, area),
                _ => 1
            };
        }

        private static decimal EstimateMortarBags(string? name, string? name2, decimal area)
        {
            var hay = $"{name} {name2}".ToLowerInvariant();
            var bagKg = TryParseBagKg(hay) ?? SalesWallEstimates.DefaultBagKg;
            return Math.Max(1, Math.Ceiling(area * SalesWallEstimates.MortarKgPerM2 / bagKg));
        }

        private static decimal? TryParsePaintLiters(string hay)
        {
            var liters = Regex.Match(hay, @"(\d+(?:[.,]\d+)?)\s*l\b");
            if (liters.Success
                && decimal.TryParse(liters.Groups[1].Value.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var l)
                && l > 0 && l <= 40)
                return l;

            var ml = Regex.Match(hay, @"(\d+(?:[.,]\d+)?)\s*ml\b");
            if (ml.Success
                && decimal.TryParse(ml.Groups[1].Value.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var milli)
                && milli > 0)
                return milli / 1000m;

            return null;
        }

        /// <summary>Latex / muurverf souvent en kg ≈ L pour l'ordre de grandeur.</summary>
        private static decimal? TryParsePaintKgAsLiters(string hay)
        {
            if (!ContainsAny(hay, "latex", "muurverf", "acryl", "verf", "paint"))
                return null;

            var match = Regex.Match(hay, @"(\d+(?:[.,]\d+)?)\s*kg\b");
            if (match.Success
                && decimal.TryParse(match.Groups[1].Value.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var kg)
                && kg >= 2.5m && kg <= 40)
                return kg;

            return null;
        }

        private static decimal? TryParseBagKg(string hay)
        {
            var match = Regex.Match(hay, @"(\d+(?:[.,]\d+)?)\s*kg");
            if (match.Success
                && decimal.TryParse(match.Groups[1].Value.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var kg)
                && kg > 0)
                return kg;
            return null;
        }

        private static bool ContainsAny(string hay, params string[] keys) =>
            keys.Any(key => hay.Contains(key, StringComparison.OrdinalIgnoreCase));
    }
}
