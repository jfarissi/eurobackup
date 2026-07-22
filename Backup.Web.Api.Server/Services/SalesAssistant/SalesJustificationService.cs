using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Backup.Web.Api.Server.Services.StoreChat;

namespace Backup.Web.Api.Server.Services.SalesAssistant
{
    public sealed class SalesCompareRowDto
    {
        public string ProductId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Brand { get; set; }
        public string? Category { get; set; }
        public decimal? Price { get; set; }
        public string? WeightHint { get; set; }
    }

    public interface ISalesCompareEngine
    {
        (string Reply, List<SalesCompareRowDto> Rows) BuildComparison(
            IReadOnlyList<StoreChatProductSuggestionDto> products,
            IReadOnlyList<string>? brandHints = null);
    }

    public class SalesCompareEngine : ISalesCompareEngine
    {
        public (string Reply, List<SalesCompareRowDto> Rows) BuildComparison(
            IReadOnlyList<StoreChatProductSuggestionDto> products,
            IReadOnlyList<string>? brandHints = null)
        {
            var selected = products.Take(3).ToList();
            if (brandHints is { Count: >= 2 })
            {
                var byBrand = new List<StoreChatProductSuggestionDto>();
                foreach (var brand in brandHints.Take(3))
                {
                    var hit = products.FirstOrDefault(p =>
                        (!string.IsNullOrWhiteSpace(p.Brand) && p.Brand.Contains(brand, StringComparison.OrdinalIgnoreCase))
                        || p.Name.Contains(brand, StringComparison.OrdinalIgnoreCase));
                    if (hit != null && byBrand.All(x => x.ProductId != hit.ProductId))
                        byBrand.Add(hit);
                }

                if (byBrand.Count >= 2)
                    selected = byBrand.Take(3).ToList();
            }

            if (selected.Count < 2)
            {
                return (
                    "Pour comparer, indiquez deux marques ou produits (ex. « comparer Knauf et Silka ») ou affichez d'abord une liste.",
                    new List<SalesCompareRowDto>());
            }

            var rows = selected.Select(p => new SalesCompareRowDto
            {
                ProductId = p.ProductId,
                Name = p.Name,
                Brand = p.Brand,
                Category = p.Category,
                Price = p.Price,
                WeightHint = ExtractWeightHint(p.Name)
            }).ToList();

            var sb = new StringBuilder();
            sb.AppendLine("Comparatif (faits catalogue uniquement) :");
            sb.AppendLine("| Produit | Marque | Catégorie | Prix | Poids |");
            sb.AppendLine("|---|---|---|---|---|");
            foreach (var r in rows)
            {
                sb.Append("| ")
                    .Append(r.Name)
                    .Append(" | ")
                    .Append(r.Brand ?? "—")
                    .Append(" | ")
                    .Append(r.Category ?? "—")
                    .Append(" | ")
                    .Append(r.Price is null ? "—" : $"{r.Price:N2} €")
                    .Append(" | ")
                    .Append(r.WeightHint ?? "—")
                    .AppendLine(" |");
            }

            sb.AppendLine();
            sb.Append("Aucun autre critère inventé — uniquement libellé, marque, catégorie et prix catalogue.");
            return (sb.ToString().Trim(), rows);
        }

        private static string? ExtractWeightHint(string name)
        {
            var m = System.Text.RegularExpressions.Regex.Match(name ?? string.Empty, @"(\d+(?:[.,]\d+)?)\s*kg", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            return m.Success ? m.Value.Replace(',', '.') : null;
        }
    }

    public interface ISalesJustificationService
    {
        string Justify(string text, StoreChatSession session, IReadOnlyList<StoreChatProductSuggestionDto> products);
    }

    public class SalesJustificationService : ISalesJustificationService
    {
        public string Justify(string text, StoreChatSession session, IReadOnlyList<StoreChatProductSuggestionDto> products)
        {
            var product = products.FirstOrDefault()
                          ?? session.LastSuggestedProducts.FirstOrDefault();
            if (product == null)
            {
                return "Montrez d'abord un produit (recherche ou liste), puis demandez « pourquoi ce produit ? ».";
            }

            var category = InferCategory(session, product);
            var template = category switch
            {
                "ciment" =>
                    $"Je recommande « {product.Name} » car c'est un liant catalogue adapté au malaxage mortier/béton. "
                    + "Vérifiez le format (5 kg bricolage vs 25 kg chantier) selon la surface. "
                    + (product.Price is > 0 ? $"Prix catalogue : {product.Price:N2} €." : ""),
                "peinture" =>
                    $"« {product.Name} » convient comme finition peinture catalogue"
                    + (string.IsNullOrWhiteSpace(product.Brand) ? "." : $" ({product.Brand}).")
                    + " Pour un débutant : sous-couche + 2 couches ; pour un pro : aller directement au devis quantitatif.",
                "carrelage" =>
                    $"« {product.Name} » est une référence carrelage/faïence du catalogue"
                    + (product.Price is > 0 ? $" à {product.Price:N2} €." : ".")
                    + " Pensez colle + joint adaptés au support (intérieur/extérieur).",
                _ =>
                    $"« {product.Name} » figure dans le catalogue"
                    + (string.IsNullOrWhiteSpace(product.Brand) ? "" : $" (marque {product.Brand})")
                    + (string.IsNullOrWhiteSpace(product.Category) ? "" : $", rayon {product.Category}")
                    + (product.Price is > 0 ? $", prix {product.Price:N2} €." : ".")
                    + " Justification basée uniquement sur ces faits catalogue — pas de gamme inventée."
            };

            if (string.Equals(session.SkillLevel, "Beginner", StringComparison.OrdinalIgnoreCase))
            {
                template += "\n\nÉtape débutant : commencez par la plus petite quantité, testez sur une zone, puis complétez.";
            }
            else if (string.Equals(session.SkillLevel, "Pro", StringComparison.OrdinalIgnoreCase))
            {
                template += "\n\nProfil pro : je peux préparer un devis direct sur cette référence.";
            }

            return template.Trim();
        }

        private static string InferCategory(StoreChatSession session, StoreChatProductSuggestionDto product)
        {
            if (session.SearchTypeHints.Any(h => h.Equals("ciment", StringComparison.OrdinalIgnoreCase)))
                return "ciment";
            if (session.SearchTypeHints.Any(h => h.Equals("peinture", StringComparison.OrdinalIgnoreCase)))
                return "peinture";

            var hay = $"{product.Name} {product.Category}".ToLowerInvariant();
            if (hay.Contains("ciment") || hay.Contains("cement"))
                return "ciment";
            if (hay.Contains("peinture") || hay.Contains("verf"))
                return "peinture";
            if (hay.Contains("carrel") || hay.Contains("tegel"))
                return "carrelage";
            return "generic";
        }
    }

    public static class SalesSkillTone
    {
        public static string Prefix(StoreChatSession session)
        {
            return session.SkillLevel switch
            {
                "Beginner" => "Conseil débutant : ",
                "Pro" => "Mode pro : ",
                "Diy" => "Conseil bricolage : ",
                _ => string.Empty
            };
        }

        public static string AdaptReply(string reply, StoreChatSession session)
        {
            if (string.IsNullOrWhiteSpace(reply))
                return reply;

            if (string.Equals(session.SkillLevel, "Beginner", StringComparison.OrdinalIgnoreCase)
                && !reply.Contains("Étape", StringComparison.OrdinalIgnoreCase)
                && !reply.Contains("débutant", StringComparison.OrdinalIgnoreCase))
            {
                return reply.TrimEnd()
                       + "\n\nÉtape pédagogique : choisissez 1 produit principal, validez la quantité, puis on complète le reste.";
            }

            if (string.Equals(session.SkillLevel, "Pro", StringComparison.OrdinalIgnoreCase)
                && !reply.Contains("devis", StringComparison.OrdinalIgnoreCase))
            {
                return reply.TrimEnd()
                       + "\n\nChemin court : ajoutez au panier → devis PDF quand vous êtes prêt.";
            }

            return reply;
        }
    }
}
