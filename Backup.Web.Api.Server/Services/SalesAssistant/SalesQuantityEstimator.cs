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
            // Quantités déjà posées dans SearchProductsAsync (classification Name/Name2).
            // Garde-fou si un autre chemin crée des suggestions sans qté.
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
