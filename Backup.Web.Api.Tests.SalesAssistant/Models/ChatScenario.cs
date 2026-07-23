using System.Text.Json;
using System.Text.Json.Serialization;

namespace Backup.Web.Api.Tests.SalesAssistant;

/// <summary>Scénario conversation riche (catalogue MainType EuroBrico + attentes C#).</summary>
public sealed class ChatScenario
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    /// <summary>MainType ERP (ex. Bouwmaterialen, Verf) — issu du catalogue.</summary>
    public string MainType { get; set; } = "";
    public string? MainTypeId { get; set; }
    public string? DomainId { get; set; }
    public string? Notes { get; set; }
    public List<string> ExpectedCategoryHints { get; set; } = new();
    public List<string> ForbiddenProductHints { get; set; } = new();
    public List<ScenarioTurn> Turns { get; set; } = new();
}

public sealed class ScenarioTurn
{
    public string User { get; set; } = "";
    public ScenarioExpect? Expect { get; set; }
    /// <summary>État panier simulé avant ce tour (noms produits).</summary>
    public List<string>? CartBefore { get; set; }
}

public sealed class ScenarioExpect
{
    public string? DomainId { get; set; }
    public string? WallGuideFamily { get; set; }
    public decimal? PaintAreaMin { get; set; }
    public decimal? PaintAreaMax { get; set; }
    public decimal? WallAreaMin { get; set; }
    public string? GuidedIntent { get; set; }
    public bool? ProjectBaseComplete { get; set; }
    public List<string>? ReplyMustContain { get; set; }
    /// <summary>Fragments interdits dans la réponse C# déterministe (pas le LLM).</summary>
    public List<string>? ReplyMustNotContain { get; set; }
    public List<string>? CategoryMustMatchAny { get; set; }
    public List<string>? ProductMustNotMatch { get; set; }
}

public static class ScenarioLoader
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static string ScenariosDir =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "Scenarios"));

    public static IReadOnlyList<ChatScenario> LoadAll()
    {
        var dir = ScenariosDir;
        if (!Directory.Exists(dir))
            return Array.Empty<ChatScenario>();

        return Directory.GetFiles(dir, "*.json", SearchOption.AllDirectories)
            .Where(f => !Path.GetFileName(f).Equals("catalog-maintypes.json", StringComparison.OrdinalIgnoreCase))
            .Select(f =>
            {
                var json = File.ReadAllText(f);
                var s = JsonSerializer.Deserialize<ChatScenario>(json, JsonOpts)
                        ?? throw new InvalidOperationException($"Invalid scenario: {f}");
                if (string.IsNullOrWhiteSpace(s.Id))
                    s.Id = Path.GetFileNameWithoutExtension(f);
                return s;
            })
            .OrderBy(s => s.MainType)
            .ThenBy(s => s.Id)
            .ToList();
    }

    public static IReadOnlyList<CatalogMainType> LoadCatalogMainTypes()
    {
        var path = Path.Combine(ScenariosDir, "catalog-maintypes.json");
        if (!File.Exists(path))
            return Array.Empty<CatalogMainType>();
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<List<CatalogMainType>>(json, JsonOpts) ?? new();
    }
}

public sealed class CatalogMainType
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string? MappedDomainId { get; set; }
    public bool HasScenario { get; set; } = true;
}
