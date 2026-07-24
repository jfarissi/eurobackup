using Backup.Web.Api.Server.Services.SalesAssistant;
using Backup.Web.Api.Server.Services.StoreChat;

namespace Backup.Web.Api.Tests.SalesAssistant.Ollama;

/// <summary>
/// Harnais routeur LLM : JSON valide + action dans la whitelist guard.
/// Lancer : dotnet test --filter Category=Ollama
/// </summary>
[Trait("Category", "Ollama")]
public class LlmRouterOllamaTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return
        [
            "complete-cest-bon",
            CompleteWall(),
            "c'est bon ?",
            new[] { "review_cart", "ask_clarification", "create_quote" }
        ];
        yield return
        [
            "complete-manque",
            CompleteWall(),
            "il me manque quelque chose ?",
            new[] { "review_cart", "suggest_complements", "ask_clarification" }
        ];
        yield return
        [
            "structure-autres",
            StructureOnly(),
            "autres ?",
            new[] { "wall_next_step", "search_products", "suggest_complements" }
        ];
        yield return
        [
            "empty-search",
            new StoreChatSession { ActiveProjectDomainId = "wall_construction" },
            "je cherche du ciment portland 25kg",
            new[] { "search_products", "ask_clarification" }
        ];
        yield return
        [
            "pro-jump",
            StructureOnly(),
            "Murfor + ciment, passe aux outils",
            new[] { "wall_next_step", "search_products", "ask_clarification" }
        ];
    }

    [OllamaTheory]
    [MemberData(nameof(Cases))]
    public async Task Router_returns_whitelisted_json_action(
        string caseId,
        StoreChatSession session,
        string userText,
        string[] acceptableActions)
    {
        Assert.False(string.IsNullOrWhiteSpace(caseId));
        FailIfRequiredButDown();

        var router = OllamaTestSupport.CreateLlmRouter();
        var decision = await router.TryDecideAsync(session, userText);

        Assert.NotNull(decision);
        Assert.NotNull(decision!.ParsedAction);
        Assert.Contains(decision.Action, acceptableActions);

        Assert.True(
            SalesLlmRouterGuard.IsAllowed(decision.ParsedAction.Value, session),
            $"[{caseId}] action {decision.Action} hors guard");
    }

    private static StoreChatSession CompleteWall()
    {
        var s = new StoreChatSession { ActiveProjectDomainId = "wall_construction" };
        s.Cart.Add(new StoreChatCartItem { ErpProductId = 1, Name = "Juwö Lijmblok", Quantity = 175, UnitPrice = 5 });
        s.Cart.Add(new StoreChatCartItem { ErpProductId = 2, Name = "Cement Wit - 20kg", Quantity = 21, UnitPrice = 13 });
        s.Cart.Add(new StoreChatCartItem { ErpProductId = 3, Name = "Murfor Plat 04cm", Quantity = 5, UnitPrice = 12 });
        s.Cart.Add(new StoreChatCartItem { ErpProductId = 4, Name = "OX Pro Emmer troffel", Quantity = 2, UnitPrice = 32 });
        s.Cart.Add(new StoreChatCartItem { ErpProductId = 5, Name = "WERKHANDSCHOENEN", Quantity = 1, UnitPrice = 2 });
        return s;
    }

    private static StoreChatSession StructureOnly()
    {
        var s = new StoreChatSession { ActiveProjectDomainId = "wall_construction" };
        s.Cart.Add(new StoreChatCartItem { ErpProductId = 1, Name = "Snelbouw Porotherm", Quantity = 100, UnitPrice = 1 });
        return s;
    }

    private static void FailIfRequiredButDown()
    {
        var reason = OllamaTestSupport.ProbeUnavailableReason();
        if (reason != null && OllamaTestSupport.RequireOllama)
            Assert.Fail(reason);
    }
}
