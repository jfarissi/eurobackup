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
        string BuildCartReviewReply(StoreChatSession session);
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

        public string BuildCartReviewReply(StoreChatSession session)
        {
            if (session.Cart.Count == 0)
                return "Votre panier est vide. Ajoutez d'abord des produits pour que je puisse le commenter.";

            var cart = SalesProjectGuide.CartOnlyHay(session);
            var sb = new StringBuilder();
            sb.AppendLine("Voici mon avis sur votre panier :");
            foreach (var line in session.Cart)
                sb.AppendLine($"• {line.Name} × {line.Quantity:0.##}");

            if (string.Equals(session.ActiveProjectDomainId, "wall_construction", StringComparison.OrdinalIgnoreCase)
                || session.WallAreaM2 is > 0)
            {
                sb.AppendLine();
                if (SalesProjectGuide.HasStructure(cart) && SalesProjectGuide.HasBinder(cart))
                    sb.AppendLine("✓ Base OK : structure + liant présents.");
                else if (SalesProjectGuide.HasStructure(cart))
                    sb.AppendLine("⚠ Il manque encore un liant (ciment / mortier / colle blocs) adapté.");
                else
                    sb.AppendLine("⚠ Il manque encore le matériau de structure (briques / blocs).");

                if (ContainsAny(cart, "gipsplaat", "gipsplaten"))
                    sb.AppendLine("⚠ Le filet « gipsplaten » sert aux cloisons plâtre — pour un mur maçonné, préférez Murfor / treillis de lit de pose (Zind & Grid, Net IJzer).");

                if (ContainsAny(cart, "lijmblok", "lijmblokken") && ContainsAny(cart, "cement wit", "cement cem")
                    && !ContainsAny(cart, "lijmmortel", "blokkenlijm", "dunbed"))
                    sb.AppendLine("💡 Blocs collés (lijmblok) : un mortier-colle / dunbed est souvent plus adapté qu’un ciment classique — vérifiez la fiche produit.");

                if (!SalesProjectGuide.HasReinforcement(cart))
                    sb.AppendLine("○ Treillis / ferraillage mur encore absent (Murfor, bétonnet…).");
                else
                    sb.AppendLine("✓ Renforcement présent.");

                if (!SalesProjectGuide.HasTools(cart))
                    sb.AppendLine("○ Outillage pose (truelle, niveau, auge, gants) encore partiel ou absent.");

                if (session.WallAreaM2 is > 0)
                    sb.AppendLine($"Surface projet ~{session.WallAreaM2:0.##} m² — contrôlez les quantités avant devis.");
            }

            var missing = SuggestComplements(session, session.Cart.Select(c => new StoreChatProductSuggestionDto
            {
                ProductId = c.ErpProductId.ToString(),
                Name = c.Name
            }).ToList());
            if (missing.Count > 0)
            {
                sb.AppendLine("\nProchaines familles utiles :");
                foreach (var m in missing.Take(3))
                    sb.AppendLine($"• {m.Label} — {m.Reason}");
            }
            else
            {
                sb.AppendLine("\nRien d’essentiel ne manque pour passer au devis / commande.");
            }

            return sb.ToString().Trim();
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
                    AddIfMissing("mesh", "Treillis / ferraillage", "Rayons Zind & Grid · Net, IJzer en Toebehoren.", "treillis",
                        "treillis", "mesh", "wapening", "zind", "grid", "gaas", "betonijzer");
                    AddIfMissing("tools", "Truelle + niveau", "Pose plus précise, moins de reprise.", "truelle",
                        "truelle", "niveau", "troffel", "waterpas");
                    AddIfMissing("tub", "Auge / seau", "Pour gâcher le mortier.", "auge",
                        "auge", "seau", "emmer", "kuip");
                    AddIfMissing("gloves", "Gants", "Protection lors du malaxage.", "gants",
                        "gant", "gloves", "handschoen");
                    // Ne pas proposer « mortier » si déjà en panier (parcours étape 2).
                    if (!ContainsAny(present, "ciment", "cement", "mortier", "mortel"))
                    {
                        tips.Insert(0, new SalesRecommendationDto
                        {
                            Code = "mortar",
                            Label = "Mortier / ciment",
                            Reason = "Rayon Cement en Mortels — choisir le liant adapté.",
                            SearchHint = "ciment"
                        });
                    }
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
