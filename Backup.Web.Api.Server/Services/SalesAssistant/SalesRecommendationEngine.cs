using System;
using System.Collections.Generic;
using System.Linq;
using Backup.Web.Api.Server.Services.StoreChat;

namespace Backup.Web.Api.Server.Services.SalesAssistant
{
    public sealed class SalesRecommendationDto
    {
        public string Code { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
    }

    public interface ISalesRecommendationEngine
    {
        IReadOnlyList<SalesRecommendationDto> SuggestComplements(
            StoreChatSession session,
            IReadOnlyList<StoreChatProductSuggestionDto> currentProducts);
    }

    public class SalesRecommendationEngine : ISalesRecommendationEngine
    {
        public IReadOnlyList<SalesRecommendationDto> SuggestComplements(
            StoreChatSession session,
            IReadOnlyList<StoreChatProductSuggestionDto> currentProducts)
        {
            var domain = session.ActiveProjectDomainId ?? InferDomain(session, currentProducts);
            var present = string.Join(' ', currentProducts.Select(p => $"{p.Name} {p.Category}")).ToLowerInvariant();
            var tips = new List<SalesRecommendationDto>();

            void AddIfMissing(string code, string label, string reason, params string[] markers)
            {
                if (markers.Any(m => present.Contains(m, StringComparison.OrdinalIgnoreCase)))
                    return;
                tips.Add(new SalesRecommendationDto { Code = code, Label = label, Reason = reason });
            }

            switch (domain)
            {
                case "wall_construction":
                case "Wall":
                    AddIfMissing("mortar", "Mortier / ciment", "Pour lier les blocs/briques.", "ciment", "cement", "mortier", "mortel");
                    AddIfMissing("mesh", "Treillis", "Renforce les joints / enduit.", "treillis", "mesh", "wapening");
                    AddIfMissing("tools", "Truelle + niveau", "Pose plus précise, moins de reprise.", "truelle", "niveau", "troffel");
                    break;
                case "painting":
                case "Painting":
                    AddIfMissing("primer", "Sous-couche", "Meilleure accroche et rendu uniforme.", "sous-couche", "primer");
                    AddIfMissing("roller", "Rouleau", "Application rapide sur grandes surfaces.", "rouleau", "roller");
                    AddIfMissing("tape", "Ruban de masquage", "Finitions propres aux angles.", "ruban", "masking");
                    break;
                case "tiling":
                case "Bathroom":
                    AddIfMissing("adhesive", "Colle carrelage", "Fixation adaptée au support.", "colle", "lijm");
                    AddIfMissing("grout", "Joint", "Étanchéité et finition.", "joint", "voeg");
                    AddIfMissing("primer", "Primaire", "Prépare le support.", "primer", "primaire");
                    break;
                default:
                    if (session.SearchTypeHints.Any(h => h.Equals("ciment", StringComparison.OrdinalIgnoreCase)))
                    {
                        AddIfMissing("tools", "Seau / auge", "Mélange et transport du ciment.", "seau", "auge", "emmer");
                        AddIfMissing("gloves", "Gants", "Protection lors du malaxage.", "gant", "gloves");
                    }
                    break;
            }

            return tips.Take(4).ToList();
        }

        private static string InferDomain(
            StoreChatSession session,
            IReadOnlyList<StoreChatProductSuggestionDto> products)
        {
            if (!string.IsNullOrWhiteSpace(session.ActiveProjectDomainId))
                return session.ActiveProjectDomainId!;

            var hay = string.Join(' ', products.Select(p => $"{p.Name} {p.Category}")).ToLowerInvariant();
            if (ContainsAny(hay, "ciment", "cement", "brique", "bloc", "mortier"))
                return "wall_construction";
            if (ContainsAny(hay, "peinture", "verf", "paint"))
                return "painting";
            if (ContainsAny(hay, "carrel", "tegel"))
                return "tiling";
            return "Other";
        }

        private static bool ContainsAny(string hay, params string[] needles) =>
            needles.Any(n => hay.Contains(n, StringComparison.OrdinalIgnoreCase));
    }
}
