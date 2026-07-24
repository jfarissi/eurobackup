using Backup.Web.Api.Server.Services.SalesAssistant;
using Backup.Web.Api.Server.Services.StoreChat;

namespace Backup.Web.Api.Tests.SalesAssistant;

public class PaintComplementSearchTests
{
    [Theory]
    [InlineData("314 AR Kit rouleaux de rechange · Winkel (oud) / USAG", "rouleau", 0)]
    [InlineData("073 Ruban isolant insulating tape · Winkel (oud)", "ruban", 0)]
    [InlineData("Kubala Telescopische stok voor verfrollerhandvatten", "rouleau", 0)]
    [InlineData("Anti-Spatrol Beugel 6mm", "rouleau", 0)]
    [InlineData("Verfroller 25cm schilder · Verf", "rouleau", 100)]
    [InlineData("Schilderstape 50mm masking tape", "ruban", 100)]
    [InlineData("Grondverf wit 5L primer", "sous-couche", 100)]
    public void Paint_complement_scoring_rejects_clearance_noise(string hay, string hint, int minOrExact)
    {
        var score = SalesComplementTool.ScoreComplementHit(hay.ToLowerInvariant(), hint);
        if (minOrExact == 0)
            Assert.Equal(0, score);
        else
            Assert.True(score >= minOrExact, $"score={score} for {hay}");
    }

    [Fact]
    public void Fake_roller_wheels_do_not_satisfy_paint_roller_complement()
    {
        var session = new StoreChatSession { ActiveProjectDomainId = "painting" };
        session.Cart.Add(new StoreChatCartItem
        {
            ErpProductId = 1,
            Name = "Trimetal Rollacryl Superlatex 001 10L",
            Quantity = 3,
            UnitPrice = 100
        });
        session.Cart.Add(new StoreChatCartItem
        {
            ErpProductId = 2,
            Name = "314 AR Kit rouleaux de rechange",
            Quantity = 2,
            UnitPrice = 40
        });

        var missing = new SalesRecommendationEngine().SuggestComplements(
            session,
            session.Cart.Select(c => new StoreChatProductSuggestionDto
            {
                ProductId = c.ErpProductId.ToString(),
                Name = c.Name
            }).ToList());

        Assert.Contains(missing, m => m.Code == "roller");
        Assert.Contains(missing, m => m.Code == "tape");
        Assert.Contains(missing, m => m.Code == "primer");
    }

    [Fact]
    public void Paint_complements_reply_does_not_mention_bricks()
    {
        var session = new StoreChatSession { ActiveProjectDomainId = "painting" };
        session.Cart.Add(new StoreChatCartItem
        {
            ErpProductId = 1,
            Name = "Muurverf Latex 10L",
            Quantity = 3,
            UnitPrice = 50
        });

        var reply = new SalesRecommendationEngine().BuildCartComplementsReply(session);
        Assert.DoesNotContain("briques", reply, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("treillis", reply, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("peinture", reply, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sous-couche", reply, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Et_maintenant_with_paint_cart_is_cart_complements()
    {
        var session = new StoreChatSession { ActiveProjectDomainId = "painting" };
        session.Cart.Add(new StoreChatCartItem
        {
            ErpProductId = 1,
            Name = "Muurverf Latex 10L",
            Quantity = 3,
            UnitPrice = 50
        });

        var guided = new SalesGuidedIntentDetector().Detect("et maintenant ?", session);
        Assert.Equal(GuidedSalesIntent.CartComplements, guided.Intent);
    }

    [Fact]
    public void Telescopic_pole_does_not_count_as_paint_roller()
    {
        Assert.False(SalesRecommendationEngine.HasRealPaintRoller(
            "kubala telescopische stok voor verfrollerhandvatten"));
        Assert.True(SalesRecommendationEngine.HasRealPaintRoller(
            "verfroller 25cm nylon schilder"));
    }

    [Fact]
    public void Locale_paint_surface_switches_language()
    {
        var session = new StoreChatSession
        {
            PreferredLanguage = "nl",
            ActiveProjectDomainId = "painting",
            PaintAreaM2 = 100
        };
        var text = new SalesDeterministicReply().BuildCalculationSummary(session);
        Assert.Contains("Muuroppervlakte", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("m²", text);
    }
}
