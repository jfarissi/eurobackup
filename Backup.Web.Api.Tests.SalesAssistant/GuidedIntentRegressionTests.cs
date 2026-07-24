using Backup.Web.Api.Server.Services.SalesAssistant;
using Backup.Web.Api.Server.Services.StoreChat;

namespace Backup.Web.Api.Tests.SalesAssistant;

public class GuidedIntentRegressionTests
{
    private readonly SalesGuidedIntentDetector _detector = new();

    [Theory]
    [InlineData("il me faut des matériaux pour réparer mon toit")]
    [InlineData("il me faut une perceuse et des forets")]
    [InlineData("il me faut un radiateur pour le salon")]
    [InlineData("il me faut un siphon pour mon lavabo")]
    [InlineData("il me faut de la colle carrelage et du joint")]
    public void Il_me_faut_product_search_is_not_cart_complements(string user)
    {
        var session = new StoreChatSession();
        var guided = _detector.Detect(user, session);
        Assert.NotEqual(GuidedSalesIntent.CartComplements, guided.Intent);
        Assert.Equal(GuidedSalesIntent.None, guided.Intent);
    }

    [Fact]
    public void Explicit_add_to_cart_phrase_is_cart_complements()
    {
        var session = new StoreChatSession { ActiveProjectDomainId = "painting" };
        session.Cart.Add(new StoreChatCartItem
        {
            ErpProductId = 1,
            Name = "Muurverf Latex Mat 10L",
            Quantity = 2,
            UnitPrice = 40
        });

        Assert.True(SalesComplementRules.IsBaseComplete(session));
        var guided = _detector.Detect("autres produits à ajouter à mon panier", session);
        Assert.Equal(GuidedSalesIntent.CartComplements, guided.Intent);
    }

    [Fact]
    public void Paint_need_tools_or_other_products_is_cart_complements()
    {
        var session = new StoreChatSession { ActiveProjectDomainId = "painting" };
        session.Cart.Add(new StoreChatCartItem
        {
            ErpProductId = 1,
            Name = "Trimetal Rollacryl Superlatex 001 10L",
            Quantity = 3,
            UnitPrice = 157
        });

        Assert.True(SalesComplementRules.ShouldOfferComplements(session));
        var guided = _detector.Detect(
            "c'est bon ou j'ai besoin d'autres produits ou outils",
            session);
        Assert.Equal(GuidedSalesIntent.CartComplements, guided.Intent);
    }

    [Fact]
    public void Paint_base_complete_with_paint_only_no_primer_required()
    {
        var session = new StoreChatSession { ActiveProjectDomainId = "painting" };
        session.Cart.Add(new StoreChatCartItem
        {
            ErpProductId = 1,
            Name = "Muurverf Latex 10L",
            Quantity = 1,
            UnitPrice = 40
        });
        Assert.True(SalesComplementRules.IsBaseComplete(session));
        Assert.Contains(SalesComplementRules.GetMissingHints(session),
            h => h.Contains("sous-couche", StringComparison.OrdinalIgnoreCase)
                 || h.Contains("rouleau", StringComparison.OrdinalIgnoreCase)
                 || h.Contains("ruban", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Explicit_panier_complements_phrase_still_detected()
    {
        var session = new StoreChatSession();
        session.Cart.Add(new StoreChatCartItem
        {
            ErpProductId = 1,
            Name = "Produit X",
            Quantity = 1,
            UnitPrice = 1
        });
        var guided = _detector.Detect("qu'est-ce qui manque pour mon panier ?", session);
        Assert.Equal(GuidedSalesIntent.CartComplements, guided.Intent);
    }
}
