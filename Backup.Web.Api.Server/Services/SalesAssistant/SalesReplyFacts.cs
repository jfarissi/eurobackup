using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using Backup.Web.Api.Server.Services.StoreChat;

namespace Backup.Web.Api.Server.Services.SalesAssistant
{
    /// <summary>
    /// Faits métier validés envoyés au LLM (voix uniquement — pas de décision).
    /// </summary>
    public sealed class SalesReplyFacts
    {
        [JsonPropertyName("intent")]
        public string Intent { get; set; } = "PRODUCT_SEARCH";

        [JsonPropertyName("language")]
        public string Language { get; set; } = SalesLocale.Fr;

        [JsonPropertyName("project")]
        public SalesReplyProjectFacts Project { get; set; } = new();

        [JsonPropertyName("answerFacts")]
        public SalesReplyAnswerFacts Answer { get; set; } = new();

        [JsonPropertyName("rules")]
        public List<string> Rules { get; set; } = DefaultRules.ToList();

        public static readonly string[] DefaultRules =
        {
            "Ne cite aucun produit hors topProducts",
            "N'invente ni prix, ni référence, ni gamme absente",
            "Ne confirme jamais une commande ou un paiement",
            "Ne choisis aucun outil / action métier",
            "2 à 4 phrases en français",
            "Une seule question de suivi maximum"
        };

        public static SalesReplyFacts FromSearch(
            StoreChatSession session,
            IReadOnlyList<StoreChatProductSuggestionDto> products,
            ProductSearchFilter meta,
            string? calculationSummary = null,
            string? suggestedFollowUp = null)
        {
            return new SalesReplyFacts
            {
                Intent = meta.Intent ?? meta.Outcome.ToString(),
                Language = SalesLocale.Of(session),
                Project = new SalesReplyProjectFacts
                {
                    DomainId = session.Project.DomainId,
                    DomainLabel = session.Project.DomainLabel,
                    Brand = meta.Brand ?? session.Project.PreferredBrand,
                    Categories = meta.TypeHints.Count > 0
                        ? meta.TypeHints.ToList()
                        : session.Project.SearchTypeHints.ToList(),
                    WeightKg = meta.WeightKg ?? session.Project.PreferredWeightKg,
                    BudgetMax = session.Project.BudgetMax,
                    SkillLevel = session.Project.SkillLevel,
                    Summary = session.Project.SummaryLine(),
                    WallAreaM2 = session.Project.WallAreaM2,
                    CalculationSummary = calculationSummary
                },
                Answer = new SalesReplyAnswerFacts
                {
                    HasMatches = products.Count > 0,
                    TotalMatches = meta.TotalMatches > 0 ? meta.TotalMatches : products.Count,
                    Outcome = meta.Outcome.ToString(),
                    IsYesNoBrandQuestion = meta.IsYesNoBrandQuestion,
                    SuggestedFollowUp = suggestedFollowUp,
                    TopProducts = products.Take(8).Select(p => new SalesReplyProductFact
                    {
                        Id = p.ProductId,
                        Name = p.Name,
                        Brand = p.Brand,
                        Category = p.Category,
                        Price = p.Price,
                        SuggestedQuantity = p.SuggestedQuantity ?? 0
                    }).ToList()
                }
            };
        }
    }

    public sealed class SalesReplyProjectFacts
    {
        [JsonPropertyName("domainId")]
        public string? DomainId { get; set; }

        [JsonPropertyName("domainLabel")]
        public string? DomainLabel { get; set; }

        [JsonPropertyName("brand")]
        public string? Brand { get; set; }

        [JsonPropertyName("categories")]
        public List<string> Categories { get; set; } = new();

        [JsonPropertyName("weightKg")]
        public decimal? WeightKg { get; set; }

        [JsonPropertyName("budgetMax")]
        public decimal? BudgetMax { get; set; }

        [JsonPropertyName("skillLevel")]
        public string? SkillLevel { get; set; }

        [JsonPropertyName("summary")]
        public string? Summary { get; set; }

        [JsonPropertyName("wallAreaM2")]
        public decimal? WallAreaM2 { get; set; }

        [JsonPropertyName("calculationSummary")]
        public string? CalculationSummary { get; set; }
    }

    public sealed class SalesReplyAnswerFacts
    {
        [JsonPropertyName("hasMatches")]
        public bool HasMatches { get; set; }

        [JsonPropertyName("totalMatches")]
        public int TotalMatches { get; set; }

        [JsonPropertyName("outcome")]
        public string? Outcome { get; set; }

        [JsonPropertyName("isYesNoBrandQuestion")]
        public bool IsYesNoBrandQuestion { get; set; }

        [JsonPropertyName("suggestedFollowUp")]
        public string? SuggestedFollowUp { get; set; }

        [JsonPropertyName("topProducts")]
        public List<SalesReplyProductFact> TopProducts { get; set; } = new();
    }

    public sealed class SalesReplyProductFact
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("brand")]
        public string? Brand { get; set; }

        [JsonPropertyName("category")]
        public string? Category { get; set; }

        [JsonPropertyName("price")]
        public decimal? Price { get; set; }

        [JsonPropertyName("suggestedQuantity")]
        public decimal SuggestedQuantity { get; set; }
    }
}
