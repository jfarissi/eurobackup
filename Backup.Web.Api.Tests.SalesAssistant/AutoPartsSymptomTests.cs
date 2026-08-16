using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Services.AutoParts;
using Backup.Web.Api.Server.Services.SalesAssistant;
using Backup.Web.Api.Server.Services.SalesAssistant.Guides;
using Backup.Web.Api.Server.Services.StoreChat;
using Moq;

namespace Backup.Web.Api.Tests.SalesAssistant;

public class AutoPartsSymptomTests
{
    [Theory]
    [InlineData("bruit de frein au freinage")]
    [InlineData("les plaquettes sont usées")]
    [InlineData("disque de frein à changer")]
    [InlineData("remmen piepen")]
    public void Symptom_message_detects_auto_parts_domain(string text)
    {
        var detector = new SalesContextDetector(new Mock<IStorageBroker>().Object);
        var session = new StoreChatSession();
        detector.DetectDomain(session, text);
        Assert.Equal(AutoPartsSymptomMatcher.DomainId, session.ActiveProjectDomainId);
    }

    [Fact]
    public void Paint_message_is_not_auto_parts()
    {
        var detector = new SalesContextDetector(new Mock<IStorageBroker>().Object);
        var session = new StoreChatSession();
        detector.DetectDomain(session, "je veux peindre ma chambre");
        Assert.Equal("painting", session.ActiveProjectDomainId);
    }

    [Fact]
    public void Guide_resolve_pads_on_squeal()
    {
        var guide = AutoPartsSymptomGuide.Instance;
        var session = new StoreChatSession { ActiveProjectDomainId = AutoPartsSymptomMatcher.DomainId };
        var step = guide.ResolveNext(session, "bruit de frein qui grince");
        Assert.Equal("pads", step.Id);
    }

    [Fact]
    public void Matcher_maps_squeal_to_pad_and_disc()
    {
        var hits = AutoPartsSymptomMatcher.Match("bruit de frein");
        Assert.Contains(hits, h => h.ProductRefs.Contains("DIAG-PAD"));
        Assert.Contains(hits, h => h.ProductRefs.Contains("DIAG-DISC"));
    }
}
