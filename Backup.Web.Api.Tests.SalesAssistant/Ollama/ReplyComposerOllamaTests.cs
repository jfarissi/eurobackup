using Backup.Web.Api.Server.Services.SalesAssistant;

namespace Backup.Web.Api.Tests.SalesAssistant.Ollama;

/// <summary>
/// Smoke LLM : Ollama réel (qwen). Soft asserts — pas de phrase exacte.
/// Lancer : dotnet test --filter Category=Ollama
/// </summary>
[Trait("Category", "Ollama")]
public class ReplyComposerOllamaTests
{
    public static IEnumerable<object[]> Cases() => OllamaTestSupport.ComposeCases();

    [OllamaTheory]
    [MemberData(nameof(Cases))]
    public async Task Compose_scenarios_soft_assert(
        string caseId,
        SalesReplyFacts facts,
        string[] mustMatchAny,
        string[] mustNotContain)
    {
        Assert.False(string.IsNullOrWhiteSpace(caseId));
        FailIfRequiredButDown();

        var composer = OllamaTestSupport.CreateComposer();
        var reply = await composer.ComposeAsync(facts);

        Assert.False(string.IsNullOrWhiteSpace(reply),
            $"[{caseId}] ComposeAsync null (LLM down ou filet unsafe)");
        Assert.True(reply!.Length is > 15 and <= 600,
            $"[{caseId}] longueur inattendue ({reply.Length}): {reply}");

        var hay = reply.ToLowerInvariant();
        Assert.True(
            mustMatchAny.Any(t => hay.Contains(t.ToLowerInvariant())),
            $"[{caseId}] aucun token attendu parmi [{string.Join(", ", mustMatchAny)}]:\n{reply}");

        foreach (var bad in mustNotContain)
        {
            Assert.DoesNotContain(bad, reply, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static void FailIfRequiredButDown()
    {
        var reason = OllamaTestSupport.ProbeUnavailableReason();
        if (reason != null && OllamaTestSupport.RequireOllama)
            Assert.Fail(reason);
    }
}
