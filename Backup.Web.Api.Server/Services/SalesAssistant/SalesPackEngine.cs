using System;
using System.Collections.Generic;
using System.Linq;
using Backup.Web.Api.Server.Services.StoreChat;

namespace Backup.Web.Api.Server.Services.SalesAssistant
{
    public sealed class SalesPackLineDto
    {
        public string Code { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string? ProductId { get; set; }
        public string? ProductName { get; set; }
        public decimal? UnitPrice { get; set; }
        public decimal SuggestedQuantity { get; set; } = 1;
        public string Status { get; set; } = "Suggested";
    }

    public sealed class SalesPackDto
    {
        public string PackType { get; set; } = "Wall";
        public string Title { get; set; } = string.Empty;
        public List<SalesPackLineDto> Lines { get; set; } = new();
        public decimal? EstimatedTotal { get; set; }
        public string? BudgetNote { get; set; }
    }

    public interface ISalesPackEngine
    {
        string ResolvePackType(string text, StoreChatSession session);
        SalesPackDto BuildPack(
            string packType,
            StoreChatSession session,
            IReadOnlyList<StoreChatProductSuggestionDto> catalogHits);
    }

    public class SalesPackEngine : ISalesPackEngine
    {
        public string ResolvePackType(string text, StoreChatSession session)
        {
            var lower = (text ?? string.Empty).ToLowerInvariant();
            if (ContainsAny(lower, "salle de bain", "sdb", "bathroom"))
                return "Bathroom";
            if (ContainsAny(lower, "peinture", "peindre"))
                return "Painting";
            if (ContainsAny(lower, "mur", "maçon", "macon", "brique", "parpaing")
                || string.Equals(session.ActiveProjectDomainId, "wall_construction", StringComparison.OrdinalIgnoreCase))
                return "Wall";
            if (string.Equals(session.ActiveProjectDomainId, "painting", StringComparison.OrdinalIgnoreCase))
                return "Painting";
            if (string.Equals(session.ActiveProjectDomainId, "tiling", StringComparison.OrdinalIgnoreCase))
                return "Bathroom";
            return "Wall";
        }

        public SalesPackDto BuildPack(
            string packType,
            StoreChatSession session,
            IReadOnlyList<StoreChatProductSuggestionDto> catalogHits)
        {
            var templates = GetTemplates(packType);
            var lines = new List<SalesPackLineDto>();
            var usedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var t in templates)
            {
                var match = catalogHits.FirstOrDefault(p =>
                    !usedIds.Contains(p.ProductId)
                    && MatchesKeywords(p, t.Keywords));

                var qty = EstimateQty(t.Code, session);
                if (match != null)
                {
                    usedIds.Add(match.ProductId);
                    lines.Add(new SalesPackLineDto
                    {
                        Code = t.Code,
                        Label = t.Label,
                        ProductId = match.ProductId,
                        ProductName = match.Name,
                        UnitPrice = match.Price,
                        SuggestedQuantity = match.SuggestedQuantity ?? qty,
                        Status = "Suggested"
                    });
                }
                else
                {
                    lines.Add(new SalesPackLineDto
                    {
                        Code = t.Code,
                        Label = t.Label,
                        SuggestedQuantity = qty,
                        Status = "Todo"
                    });
                }
            }

            // Garantir ≥ 3 lignes complémentaires (ACCEPTANCE P1)
            while (lines.Count < 3)
            {
                lines.Add(new SalesPackLineDto
                {
                    Code = $"extra{lines.Count + 1}",
                    Label = "Complément chantier",
                    SuggestedQuantity = 1,
                    Status = "Todo"
                });
            }

            var total = lines
                .Where(l => l.UnitPrice is > 0)
                .Sum(l => l.UnitPrice!.Value * l.SuggestedQuantity);

            string? budgetNote = null;
            if (session.BudgetMax is > 0)
            {
                budgetNote = total > session.BudgetMax
                    ? $"Attention : total estimé {total:N2} € > budget {session.BudgetMax:N2} €."
                    : $"Dans le budget ({total:N2} € / {session.BudgetMax:N2} €).";
            }

            return new SalesPackDto
            {
                PackType = packType,
                Title = packType switch
                {
                    "Bathroom" => "Pack salle de bain",
                    "Painting" => "Pack peinture",
                    _ => "Pack mur"
                },
                Lines = lines,
                EstimatedTotal = total > 0 ? Math.Round(total, 2) : null,
                BudgetNote = budgetNote
            };
        }

        private static decimal EstimateQty(string code, StoreChatSession session)
        {
            var area = session.WallAreaM2 ?? 10m;
            return code.ToLowerInvariant() switch
            {
                "blocks" or "bricks" => Math.Max(1, Math.Ceiling(area * 12m)),
                "mortar" => Math.Max(1, Math.Ceiling(area / 4m)),
                "paint" => Math.Max(1, Math.Ceiling(area / 10m)),
                "primer" => Math.Max(1, Math.Ceiling(area / 12m)),
                _ => 1
            };
        }

        private static bool MatchesKeywords(StoreChatProductSuggestionDto p, string[] keywords)
        {
            var hay = $"{p.Name} {p.Brand} {p.Category}".ToLowerInvariant();
            return keywords.Any(k => hay.Contains(k, StringComparison.OrdinalIgnoreCase));
        }

        private static (string Code, string Label, string[] Keywords)[] GetTemplates(string packType) =>
            packType switch
            {
                "Bathroom" => new[]
                {
                    ("tiles", "Carrelage / faïence", new[] { "carrel", "tegels", "faience", "faïence" }),
                    ("adhesive", "Colle carrelage", new[] { "colle", "lijm", "tegellijm", "mortier colle" }),
                    ("grout", "Joint", new[] { "joint", "voeg", "grout" }),
                    ("primer", "Primaire", new[] { "primer", "primaire", "sous-couche" }),
                },
                "Painting" => new[]
                {
                    ("paint", "Peinture", new[] { "peinture", "verf", "paint" }),
                    ("primer", "Sous-couche", new[] { "sous-couche", "primer", "grondverf" }),
                    ("roller", "Rouleau / pinceau", new[] { "rouleau", "pinceau", "roller", "kwast" }),
                    ("tape", "Ruban de masquage", new[] { "ruban", "masking", "afplak" }),
                },
                _ => new[]
                {
                    ("blocks", "Briques / blocs", new[] { "brique", "bloc", "blok", "baksteen", "parpaing", "kalkzand" }),
                    ("mortar", "Mortier / ciment", new[] { "mortier", "mortel", "ciment", "cement" }),
                    ("mesh", "Treillis / armature", new[] { "treillis", "wapening", "mesh", "armature" }),
                    ("tools", "Outils (truelle / niveau)", new[] { "truelle", "niveau", "troffel", "waterpas", "auge" }),
                }
            };

        private static bool ContainsAny(string hay, params string[] needles) =>
            needles.Any(n => hay.Contains(n, StringComparison.OrdinalIgnoreCase));
    }
}
