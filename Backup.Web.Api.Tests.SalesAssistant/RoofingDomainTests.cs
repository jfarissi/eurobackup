using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Services.SalesAssistant;
using Backup.Web.Api.Server.Services.SalesAssistant.Guides;
using Backup.Web.Api.Server.Services.StoreChat;
using Moq;

namespace Backup.Web.Api.Tests.SalesAssistant;

public class RoofingDomainTests
{
    [Fact]
    public void Toit_message_detects_roofing_not_cart_complements()
    {
        var detector = new SalesContextDetector(new Mock<IStorageBroker>().Object);
        var intent = new SalesGuidedIntentDetector();
        var session = new StoreChatSession();
        var text = "il me faut des matériaux pour réparer mon toit";

        detector.DetectDomain(session, text);
        Assert.Equal("roofing", session.ActiveProjectDomainId);

        var guided = intent.Detect(text, session);
        Assert.Equal(GuidedSalesIntent.None, guided.Intent);

        var hints = SalesMaterialLexicon.ExtractTypeHints(text);
        Assert.Contains("toiture", hints);
    }

    [Theory]
    [InlineData("je dois réparer ma toiture")]
    [InlineData("il me faut des tuiles pour le toit")]
    [InlineData("dakpannen voor mijn dak")]
    public void Roofing_keywords_set_domain(string text)
    {
        var detector = new SalesContextDetector(new Mock<IStorageBroker>().Object);
        var session = new StoreChatSession();
        detector.DetectDomain(session, text);
        Assert.Equal("roofing", session.ActiveProjectDomainId);
    }

    [Fact]
    public void Materiaux_alone_is_stopword_not_type_hint()
    {
        Assert.Contains("matériaux", SalesMaterialLexicon.StopWords);
        var hints = SalesMaterialLexicon.ExtractTypeHints("il me faut des matériaux");
        Assert.DoesNotContain("matériaux", hints);
    }

    [Fact]
    public void Cover_cart_markers_ignore_diamond_disc_dakpan()
    {
        var guide = RoofingProjectGuide.Instance;
        var cover = guide.Families[0];
        var discHay = "leman diamantschijf doorlopende rand dakpan graniett 125x m14";
        Assert.DoesNotContain(cover.CartMarkers, m => discHay.Contains(m, StringComparison.OrdinalIgnoreCase));

        var tileHay = "edilians waarborgpallet dakpannen";
        Assert.Contains(cover.CartMarkers, m => tileHay.Contains(m, StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("TENORD Gevelpan L. Rood Ref.076")]
    [InlineData("Vorstpan Halfrond met sluiting Leikleur Ref. 717-011")]
    [InlineData("Dakpaneel 5-kroons 100 x 500cm 80mm Antraciet")]
    public void Cover_step_complete_when_typical_nl_tiles_in_cart(string productName)
    {
        var session = new StoreChatSession { PreferredLanguage = "nl", ActiveProjectDomainId = "roofing" };
        session.Cart.Add(new StoreChatCartItem
        {
            ErpProductId = 1,
            Name = productName,
            Quantity = 1,
            UnitPrice = 10m
        });

        var guide = RoofingProjectGuide.Instance;
        Assert.True(guide.CartHasStep(session, guide.Families[0]));
        Assert.Equal("fixings", guide.ResolveNext(session, "Volgende stap").Id);
    }

    [Fact]
    public void Roofing_checklist_nl_uses_daktraject()
    {
        var session = new StoreChatSession { PreferredLanguage = "nl" };
        session.ActiveProjectDomainId = "roofing";
        var guide = RoofingProjectGuide.Instance;
        var focus = guide.Families[0];
        var checklist = guide.BuildChecklist(session, focus);
        Assert.Contains("Daktraject", checklist);
        Assert.Contains("Dakbedekking", checklist);
        Assert.DoesNotContain("Parcours toiture", checklist);
        Assert.Contains("nu kiezen", checklist);
    }
}
