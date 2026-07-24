using Backup.Web.Api.Server.Services.SalesAssistant;
using Backup.Web.Api.Server.Services.StoreChat;

namespace Backup.Web.Api.Tests.SalesAssistant;

public class LlmRouterGuardTests
{
    [Fact]
    public void Complete_wall_cart_excludes_wall_next_and_confirm_order()
    {
        var session = CompleteWallSession();
        var allowed = SalesLlmRouterGuard.AllowedActions(session);

        Assert.Contains(SalesLlmRouterAction.ReviewCart, allowed);
        Assert.Contains(SalesLlmRouterAction.SearchProducts, allowed);
        Assert.DoesNotContain(SalesLlmRouterAction.WallNextStep, allowed);
        Assert.DoesNotContain(SalesLlmRouterAction.ConfirmOrder, allowed);

        var state = SalesLlmRouterGuard.BuildState(session);
        Assert.True(state.WallGuideComplete);
        Assert.DoesNotContain("wall_next_step", state.AllowedActions);
        Assert.DoesNotContain("confirm_order", state.AllowedActions);
    }

    [Fact]
    public void Incomplete_wall_allows_next_step()
    {
        var session = new StoreChatSession { ActiveProjectDomainId = "wall_construction" };
        session.Cart.Add(new StoreChatCartItem { ErpProductId = 1, Name = "Snelbouw", Quantity = 1, UnitPrice = 1 });

        Assert.Contains(SalesLlmRouterAction.WallNextStep, SalesLlmRouterGuard.AllowedActions(session));
        Assert.False(SalesLlmRouterGuard.BuildState(session).WallGuideComplete);
    }

    [Theory]
    [InlineData("{\"action\":\"review_cart\",\"reason\":\"ok\"}", SalesLlmRouterAction.ReviewCart)]
    [InlineData("```json\n{\"action\":\"wall_next_step\"}\n```", SalesLlmRouterAction.WallNextStep)]
    [InlineData("{\"action\":\"confirm_order\"}", SalesLlmRouterAction.ConfirmOrder)]
    [InlineData("not json", null)]
    public void Parse_decision_handles_fences_and_junk(string raw, SalesLlmRouterAction? expected)
    {
        var decision = SalesLlmRouterGuard.TryParseDecision(raw);
        if (expected is null)
        {
            Assert.Null(decision);
            return;
        }

        Assert.NotNull(decision);
        Assert.Equal(expected, decision!.ParsedAction);
    }

    [Fact]
    public void Confirm_order_never_allowed_even_with_full_cart()
    {
        var session = CompleteWallSession();
        Assert.False(SalesLlmRouterGuard.IsAllowed(SalesLlmRouterAction.ConfirmOrder, session));
    }

    private static StoreChatSession CompleteWallSession()
    {
        var session = new StoreChatSession { ActiveProjectDomainId = "wall_construction" };
        session.Cart.Add(new StoreChatCartItem { ErpProductId = 1, Name = "Lijmblok", Quantity = 1, UnitPrice = 1 });
        session.Cart.Add(new StoreChatCartItem { ErpProductId = 2, Name = "Cement", Quantity = 1, UnitPrice = 1 });
        session.Cart.Add(new StoreChatCartItem { ErpProductId = 3, Name = "Murfor", Quantity = 1, UnitPrice = 1 });
        session.Cart.Add(new StoreChatCartItem { ErpProductId = 4, Name = "troffel", Quantity = 1, UnitPrice = 1 });
        return session;
    }
}
