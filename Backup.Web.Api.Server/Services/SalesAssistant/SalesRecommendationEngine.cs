using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Backup.Web.Api.Server.Services.StoreChat;

namespace Backup.Web.Api.Server.Services.SalesAssistant
{
    public sealed class SalesRecommendationDto
    {
        public string Code { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public string? SearchHint { get; set; }
    }

    public interface ISalesRecommendationEngine
    {
        IReadOnlyList<SalesRecommendationDto> SuggestComplements(
            StoreChatSession session,
            IReadOnlyList<StoreChatProductSuggestionDto> currentProducts);

        string BuildCartComplementsReply(StoreChatSession session);
    }

    public class SalesRecommendationEngine : ISalesRecommendationEngine
    {
        public IReadOnlyList<SalesRecommendationDto> SuggestComplements(
            StoreChatSession session,
            IReadOnlyList<StoreChatProductSuggestionDto> currentProducts)
        {
            var domain = session.ActiveProjectDomainId ?? InferDomain(session, currentProducts);
            var present = string.Join(' ', currentProducts.Select(p => $"{p.Name} {p.Category}")).ToLowerInvariant();
            return BuildTipsForDomain(domain, present, session);
        }

        public string BuildCartComplementsReply(StoreChatSession session)
        {
            if (session.Cart.Count == 0)
            {
                return "Votre panier est vide. Ajoutez d'abord des produits, puis je vous dirai ce qu'il manque.";
            }

            var cartAsProducts = session.Cart.Select(c => new StoreChatProductSuggestionDto
            {
                ProductId = c.ErpProductId.ToString(),
                Name = c.Name,
                Category = c.Reference
            }).ToList();

            var present = string.Join(' ', session.Cart.Select(c => c.Name)).ToLowerInvariant();
            var domain = session.ActiveProjectDomainId ?? InferDomain(session, cartAsProducts);
            var missing = BuildTipsForDomain(domain, present, session);

            var sb = new StringBuilder();
            sb.AppendLine("D'après votre panier actuel :");
            foreach (var line in session.Cart)
                sb.AppendLine($"• {line.Name} × {line.Quantity:0.##}");

            var hasStructure = ContainsAny(present,
                "brique", "baksteen", "blok", "bloc", "parpaing", "porotherm", "silka", "steen",
                "snelbouw", "boerkes", "kalkzand", "lijmblok", "gaten", "parpaing");
            var hasBinder = ContainsAny(present, "ciment", "cement", "mortier", "mortel", "lijm");

            if (domain is "wall_construction" or "Wall")
            {
                if (hasStructure && hasBinder)
                    sb.AppendLine("\nBase chantier OK : vous avez déjà un matériau de structure + un liant (ciment/mortier).");
                else if (hasStructure && !hasBinder)
                    sb.AppendLine("\nIl vous manque surtout un liant (ciment / mortier) pour assembler.");
                else if (!hasStructure && hasBinder)
                    sb.AppendLine("\nIl vous manque le matériau de structure (briques ou blocs).");
            }

            if (missing.Count == 0)
            {
                sb.AppendLine("\nRien d'essentiel ne manque pour démarrer. Vous pouvez passer au devis.");
                return sb.ToString().Trim();
            }

            sb.AppendLine("\nCompléments utiles (pas encore dans le panier) :");
            foreach (var m in missing.Take(4))
                sb.AppendLine($"• {m.Label} — {m.Reason}");

            sb.AppendLine("\nJe peux chercher ces compléments dans le catalogue si vous voulez (ex. « treillis » ou « truelle »).");
            sb.Append("Pas besoin de racheter briques/blocs/ciment déjà choisis.");
            return sb.ToString().Trim();
        }

        private static List<SalesRecommendationDto> BuildTipsForDomain(
            string domain,
            string present,
            StoreChatSession session)
        {
            var tips = new List<SalesRecommendationDto>();

            void AddIfMissing(string code, string label, string reason, string? searchHint, params string[] markers)
            {
                if (markers.Any(m => present.Contains(m, StringComparison.OrdinalIgnoreCase)))
                    return;
                tips.Add(new SalesRecommendationDto
                {
                    Code = code,
                    Label = label,
                    Reason = reason,
                    SearchHint = searchHint
                });
            }

            switch (domain)
            {
                case "wall_construction":
                case "Wall":
                    AddIfMissing("mortar", "Mortier / ciment", "Pour lier les blocs/briques.", "ciment",
                        "ciment", "cement", "mortier", "mortel");
                    AddIfMissing("mesh", "Treillis", "Renforce les joints / enduit.", "treillis",
                        "treillis", "mesh", "wapening");
                    AddIfMissing("tools", "Truelle + niveau", "Pose plus précise, moins de reprise.", "truelle",
                        "truelle", "niveau", "troffel", "waterpas");
                    AddIfMissing("tub", "Auge / seau", "Pour gâcher le mortier.", "auge",
                        "auge", "seau", "emmer", "kuip");
                    AddIfMissing("gloves", "Gants", "Protection lors du malaxage.", "gants",
                        "gant", "gloves", "handschoen");
                    break;
                case "painting":
                case "Painting":
                    AddIfMissing("primer", "Sous-couche", "Meilleure accroche et rendu uniforme.", "sous-couche",
                        "sous-couche", "primer");
                    AddIfMissing("roller", "Rouleau", "Application rapide sur grandes surfaces.", "rouleau",
                        "rouleau", "roller");
                    AddIfMissing("tape", "Ruban de masquage", "Finitions propres aux angles.", "ruban",
                        "ruban", "masking");
                    break;
                case "tiling":
                case "Bathroom":
                    AddIfMissing("adhesive", "Colle carrelage", "Fixation adaptée au support.", "colle carrelage",
                        "colle", "lijm");
                    AddIfMissing("grout", "Joint", "Étanchéité et finition.", "joint",
                        "joint", "voeg");
                    AddIfMissing("primer", "Primaire", "Prépare le support.", "primaire",
                        "primer", "primaire");
                    break;
                case "garden_landscaping":
                case "garden_cleaning":
                case "garden_maintenance":
                case "Garden":
                    AddIfMissing("border", "Bordures", "Délimite allées / massifs.", "bordure",
                        "bordure", "border", "opsluitband");
                    AddIfMissing("geo", "Géotextile", "Anti-mauvaises herbes sous dalles/gravier.", "geotextile",
                        "géotextile", "geotextile", "anti-wortel");
                    AddIfMissing("sand", "Sable / graviers", "Lit de pose et drainage.", "sable",
                        "sable", "zand", "gravier", "grind");
                    AddIfMissing("fence", "Clôture / brise-vue", "Délimitation et intimité.", "cloture",
                        "clôture", "cloture", "schutting", "brise");
                    if (domain is "garden_cleaning" or "Garden")
                    {
                        AddIfMissing("blower", "Souffleur / balai", "Ramassage feuilles et déchets.", "souffleur",
                            "souffleur", "bladblazer", "balai");
                    }

                    break;
                default:
                    if (session.SearchTypeHints.Any(h => h.Equals("ciment", StringComparison.OrdinalIgnoreCase))
                        || ContainsAny(present, "ciment", "cement"))
                    {
                        AddIfMissing("tools", "Seau / auge", "Mélange et transport du ciment.", "seau",
                            "seau", "auge", "emmer");
                        AddIfMissing("gloves", "Gants", "Protection lors du malaxage.", "gants",
                            "gant", "gloves");
                    }
                    break;
            }

            return tips.Take(5).ToList();
        }

        private static string InferDomain(
            StoreChatSession session,
            IReadOnlyList<StoreChatProductSuggestionDto> products)
        {
            if (!string.IsNullOrWhiteSpace(session.ActiveProjectDomainId))
                return session.ActiveProjectDomainId!;

            var hay = string.Join(' ', products.Select(p => $"{p.Name} {p.Category}")).ToLowerInvariant();
            if (ContainsAny(hay, "ciment", "cement", "brique", "bloc", "mortier", "baksteen", "steen",
                    "snelbouw", "boerkes", "kalkzand", "lijmblok"))
                return "wall_construction";
            if (ContainsAny(hay, "peinture", "verf", "paint"))
                return "painting";
            if (ContainsAny(hay, "carrel", "tegel"))
                return "tiling";
            if (ContainsAny(hay, "jardin", "tuin", "terrasse", "dalle", "bordure", "tondeuse", "gazon"))
                return "garden_landscaping";
            return "Other";
        }

        private static bool ContainsAny(string hay, params string[] needles) =>
            needles.Any(n => hay.Contains(n, StringComparison.OrdinalIgnoreCase));
    }
}
