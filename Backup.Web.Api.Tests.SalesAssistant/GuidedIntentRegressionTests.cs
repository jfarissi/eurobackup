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
        session.Cart.Add(new StoreChatCartItem
        {
            ErpProductId = 2,
            Name = "Primer Acryl 5L",
            Quantity = 1,
            UnitPrice = 25
        });

        Assert.True(SalesComplementRules.IsBaseComplete(session));
        var guided = _detector.Detect("autres produits à ajouter à mon panier", session);
        Assert.Equal(GuidedSalesIntent.CartComplements, guided.Intent);
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
