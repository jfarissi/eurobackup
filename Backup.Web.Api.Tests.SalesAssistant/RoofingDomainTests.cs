using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Services.SalesAssistant;
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
}
