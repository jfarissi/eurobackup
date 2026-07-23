using System.Text.Json;
using Backup.Web.Api.Server.Services.SalesAssistant;
using Backup.Web.Api.Server.Services.StoreChat;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Backup.Web.Api.Tests.SalesAssistant.Ollama;

/// <summary>
/// Branche les tests live sur Ollama local.
/// </summary>
public static class OllamaTestSupport
{
    public static string Host =>
        (Environment.GetEnvironmentVariable("OLLAMA_HOST") ?? "http://localhost:11434").TrimEnd('/');

    public static string Model =>
        Environment.GetEnvironmentVariable("OLLAMA_MODEL") ?? "qwen2.5:14b";

    public static bool RequireOllama =>
        string.Equals(Environment.GetEnvironmentVariable("SALES_ASSISTANT_REQUIRE_OLLAMA"), "1",
            StringComparison.OrdinalIgnoreCase);

    public static bool SkipRequested =>
        string.Equals(Environment.GetEnvironmentVariable("SALES_ASSISTANT_SKIP_OLLAMA"), "1",
            StringComparison.OrdinalIgnoreCase);

    /// <summary>Null si OK, sinon motif de skip / échec.</summary>
    public static string? ProbeUnavailableReason()
    {
        if (SkipRequested)
            return "SALES_ASSISTANT_SKIP_OLLAMA=1";

        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            using var response = http.GetAsync($"{Host}/api/tags").GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode)
                return $"Ollama HTTP {(int)response.StatusCode} sur {Host}";

            var json = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            using var doc = JsonDocument.Parse(json);
            var models = doc.RootElement.TryGetProperty("models", out var arr)
                ? arr.EnumerateArray()
                    .Select(m => m.TryGetProperty("name", out var n) ? n.GetString() : null)
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .Cast<string>()
                    .ToList()
                : [];

            var ok = models.Any(n =>
                string.Equals(n, Model, StringComparison.OrdinalIgnoreCase)
                || string.Equals(n, Model + ":latest", StringComparison.OrdinalIgnoreCase)
                || n.StartsWith(Model, StringComparison.OrdinalIgnoreCase));

            if (!ok)
                return $"Modèle '{Model}' absent sur {Host}. Présents : {string.Join(", ", models.Take(8))}";

            return null;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException or AggregateException)
        {
            return $"Ollama injoignable sur {Host}: {ex.GetBaseException().Message}";
        }
    }

    public static ISalesReplyComposer CreateComposer()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AISettings:Provider"] = "Ollama",
                ["AISettings:Ollama:ApiKey"] = "ollama",
                ["AISettings:Ollama:Model"] = Model,
                ["AISettings:Ollama:Endpoint"] = Host,
                ["AISettings:Ollama:EmbeddingModel"] = "nomic-embed-text"
            })
            .Build();

        var http = new HttpClient { Timeout = TimeSpan.FromMinutes(4) };
        var store = Options.Create(new StoreChatOptions { BrandName = "EuroBrico" });
        var ai = new StoreChatAiClient(http, config, store, NullLogger<StoreChatAiClient>.Instance);
        return new SalesReplyComposer(ai, store, NullLogger<SalesReplyComposer>.Instance);
    }

    public static SalesReplyFacts WallSearchFacts()
    {
        var session = new StoreChatSession
        {
            ActiveProjectDomainId = "wall_construction",
            ActiveProjectDomainLabel = "Bouwmaterialen",
            WallLengthM = 7,
            WallHeightM = 2
        };

        var products = new List<StoreChatProductSuggestionDto>
        {
            new()
            {
                ProductId = "1001",
                Name = "Snelbouw Porotherm Thermobrick 29x09",
                Brand = "Porotherm",
                Category = "Snelbouwstenen",
                Price = 4.85m,
                SuggestedQuantity = 700
            },
            new()
            {
                ProductId = "1002",
                Name = "Lijmblok Ytong 60x20",
                Brand = "Ytong",
                Category = "Lijmblokken",
                Price = 6.20m,
                SuggestedQuantity = 350
            }
        };

        var meta = new ProductSearchFilter
        {
            Outcome = ProductSearchOutcome.Domain,
            Intent = "PRODUCT_SEARCH",
            TotalMatches = 2,
            TypeHints = { "Stenen", "Snelbouw" }
        };

        var calc = new SalesDeterministicReply().BuildCalculationSummary(session);
        return SalesReplyFacts.FromSearch(session, products, meta, calc,
            suggestedFollowUp: "Souhaitez-vous des briques ou des blocs ?");
    }

    public static SalesReplyFacts EmptyCatalogFacts()
    {
        var session = new StoreChatSession { ActiveProjectDomainId = "painting" };
        var meta = new ProductSearchFilter
        {
            Outcome = ProductSearchOutcome.BrandNotFound,
            Brand = "HammeriteXYZ",
            Intent = "PRODUCT_SEARCH",
            TotalMatches = 0
        };
        return SalesReplyFacts.FromSearch(session, Array.Empty<StoreChatProductSuggestionDto>(), meta,
            suggestedFollowUp: "Quelle marque ou type de peinture murale cherchez-vous ?");
    }

    public static SalesReplyFacts PaintHouseFacts()
    {
        var session = new StoreChatSession
        {
            ActiveProjectDomainId = "painting",
            ActiveProjectDomainLabel = "Verf",
            PaintAreaM2 = 96,
            ProjectTypeHint = "Maison : chambres + SDB + couloir"
        };

        var products = new List<StoreChatProductSuggestionDto>
        {
            new()
            {
                ProductId = "2001",
                Name = "Muurverf Acryl Mat Wit 10L",
                Brand = "Gamma",
                Category = "Muurverf",
                Price = 42.90m,
                SuggestedQuantity = 2
            },
            new()
            {
                ProductId = "2002",
                Name = "Primer Muur Acryl 5L",
                Brand = "Sigma",
                Category = "Primer",
                Price = 28.50m,
                SuggestedQuantity = 1
            }
        };

        var meta = new ProductSearchFilter
        {
            Outcome = ProductSearchOutcome.Domain,
            Intent = "PRODUCT_SEARCH",
            TotalMatches = 2,
            TypeHints = { "muurverf", "latex" }
        };

        var calc = new SalesDeterministicReply().BuildCalculationSummary(session);
        return SalesReplyFacts.FromSearch(session, products, meta, calc,
            suggestedFollowUp: "Souhaitez-vous une peinture mate ou satinée ?");
    }

    public static SalesReplyFacts CementBrandFacts()
    {
        var session = new StoreChatSession
        {
            ActiveProjectDomainId = "wall_construction",
            ActiveProjectDomainLabel = "Bouwmaterialen",
            WallLengthM = 7,
            WallHeightM = 2
        };

        var products = new List<StoreChatProductSuggestionDto>
        {
            new()
            {
                ProductId = "3001",
                Name = "Cement Wit - 20kg",
                Brand = "Holcim",
                Category = "Cement en Mortels",
                Price = 9.95m,
                SuggestedQuantity = 8
            }
        };

        var meta = new ProductSearchFilter
        {
            Outcome = ProductSearchOutcome.BrandAndType,
            Brand = "Holcim",
            Intent = "PRODUCT_SEARCH",
            TotalMatches = 1,
            TypeHints = { "ciment", "cement" },
            IsYesNoBrandQuestion = true
        };

        return SalesReplyFacts.FromSearch(session, products, meta,
            new SalesDeterministicReply().BuildCalculationSummary(session),
            suggestedFollowUp: "Combien de sacs souhaitez-vous ajouter au panier ?");
    }

    public static SalesReplyFacts MeshReinforcementFacts()
    {
        var session = new StoreChatSession
        {
            ActiveProjectDomainId = "wall_construction",
            ActiveProjectDomainLabel = "Bouwmaterialen"
        };

        var products = new List<StoreChatProductSuggestionDto>
        {
            new()
            {
                ProductId = "4001",
                Name = "Murfor Plat 04cm",
                Brand = "Murfor",
                Category = "Net, IJzer en Toebehoren",
                Price = 12.40m,
                SuggestedQuantity = 10
            },
            new()
            {
                ProductId = "4002",
                Name = "Betonnet Gegalvaniseerd 2x1m",
                Brand = "Zind",
                Category = "Zind & Grid",
                Price = 18.00m,
                SuggestedQuantity = 5
            }
        };

        var meta = new ProductSearchFilter
        {
            Outcome = ProductSearchOutcome.Domain,
            Intent = "PRODUCT_SEARCH",
            TotalMatches = 2,
            TypeHints = { "Murfor", "wapening", "treillis" }
        };

        return SalesReplyFacts.FromSearch(session, products, meta,
            suggestedFollowUp: "Préférez-vous Murfor ou un treillis béton ?");
    }

    public static SalesReplyFacts ToolsFacts()
    {
        var session = new StoreChatSession
        {
            ActiveProjectDomainId = "wall_construction",
            ActiveProjectDomainLabel = "Bouwmaterialen"
        };

        var products = new List<StoreChatProductSuggestionDto>
        {
            new()
            {
                ProductId = "5001",
                Name = "Troffel RVS 280mm",
                Brand = "Stubai",
                Category = "Gereedschap",
                Price = 14.90m,
                SuggestedQuantity = 1
            },
            new()
            {
                ProductId = "5002",
                Name = "Emmer Maçon 12L",
                Brand = "EuroBrico",
                Category = "Emmer",
                Price = 4.50m,
                SuggestedQuantity = 2
            }
        };

        var meta = new ProductSearchFilter
        {
            Outcome = ProductSearchOutcome.Domain,
            Intent = "PRODUCT_SEARCH",
            TotalMatches = 2,
            TypeHints = { "truelle", "troffel" }
        };

        return SalesReplyFacts.FromSearch(session, products, meta,
            suggestedFollowUp: "Faut-il aussi un niveau ou des gants ?");
    }

    public static SalesReplyFacts ElectricalFacts()
    {
        var session = new StoreChatSession
        {
            ActiveProjectDomainId = "electrical",
            ActiveProjectDomainLabel = "Elektriciteit"
        };

        var products = new List<StoreChatProductSuggestionDto>
        {
            new()
            {
                ProductId = "6001",
                Name = "LED Spot GU10 5W Blanc",
                Brand = "Philips",
                Category = "Verlichting",
                Price = 6.95m,
                SuggestedQuantity = 4
            }
        };

        var meta = new ProductSearchFilter
        {
            Outcome = ProductSearchOutcome.Domain,
            Intent = "PRODUCT_SEARCH",
            TotalMatches = 1,
            TypeHints = { "LED", "spot" }
        };

        return SalesReplyFacts.FromSearch(session, products, meta,
            suggestedFollowUp: "Combien de spots souhaitez-vous ?");
    }

    /// <summary>Cas smoke LLM (id, facts, tokens attendus, tokens interdits).</summary>
    public static IEnumerable<object[]> ComposeCases()
    {
        yield return
        [
            "wall-structure",
            WallSearchFacts(),
            new[] { "porotherm", "ytong", "mur", "brique", "bloc", "m²", "m2", "surface" },
            new[] { "hammerite", "gipsplaat", "griffes pour murs" }
        ];
        yield return
        [
            "paint-house",
            PaintHouseFacts(),
            new[] { "peint", "m²", "m2", "l", "litre", "muurverf", "acryl", "surface" },
            new[] { "hammerite", "porotherm", "briques" }
        ];
        yield return
        [
            "cement-brand",
            CementBrandFacts(),
            new[] { "cement", "ciment", "holcim", "sac", "20" },
            new[] { "gipsplaat", "ampoule" }
        ];
        yield return
        [
            "mesh-murfor",
            MeshReinforcementFacts(),
            new[] { "murfor", "treillis", "ferrail", "zind", "béton", "beton" },
            new[] { "gipsplaat", "gipsplaten", "hammerite" }
        ];
        yield return
        [
            "tools-truelle",
            ToolsFacts(),
            new[] { "troffel", "truelle", "emmer", "seau", "outil" },
            new[] { "hammerite", "gipsplaat" }
        ];
        yield return
        [
            "electrical-led",
            ElectricalFacts(),
            new[] { "led", "spot", "philips", "éclairage", "eclairage", "ampoule", "luminaire" },
            new[] { "porotherm", "ciment" }
        ];
        yield return
        [
            "empty-catalog",
            EmptyCatalogFacts(),
            new[] { "pas", "aucun", "trouv", "disponib", "sans" },
            new[] { "porotherm thermobrick", "1." }
        ];
    }
}

/// <summary>[Fact] qui se skip si Ollama / modèle absent (sauf REQUIRE=1).</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class OllamaFactAttribute : FactAttribute
{
    public OllamaFactAttribute()
    {
        ApplySkip(this);
    }

    internal static void ApplySkip(FactAttribute attr)
    {
        var reason = OllamaTestSupport.ProbeUnavailableReason();
        if (reason == null)
            return;
        if (OllamaTestSupport.RequireOllama)
            return;
        attr.Skip = reason;
    }
}

/// <summary>[Theory] Ollama avec skip auto.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class OllamaTheoryAttribute : TheoryAttribute
{
    public OllamaTheoryAttribute()
    {
        OllamaFactAttribute.ApplySkip(this);
    }
}
