using Backup.Web.Api.Server.Services.SalesAssistant;
using Backup.Web.Api.Server.Services.StoreChat;

namespace Backup.Web.Api.Tests.SalesAssistant;

public class SalesLanguageMismatchTests
{
    [Theory]
    [InlineData("il me faut des matériaux pour réparer mon toit", "fr")]
    [InlineData("je veux peindre mon mur de 7m de longueur", "fr")]
    [InlineData("Ik wil mijn muur schilderen, die 7 meter lang is", "nl")]
    [InlineData("Ik heb dakpannen nodig voor mijn dak", "nl")]
    public void Detects_message_language(string text, string expected)
    {
        Assert.Equal(expected, SalesLanguageMismatch.DetectMessageLanguage(text));
    }

    [Fact]
    public void Warns_when_ui_nl_and_message_fr()
    {
        var session = new StoreChatSession { PreferredLanguage = "nl" };
        var reply = SalesLanguageMismatch.MaybePrependWarning(
            session,
            "il me faut des matériaux pour réparer mon toit",
            "Hier zijn producten.");

        Assert.StartsWith("Let op:", reply);
        Assert.Contains("Frans", reply);
        Assert.Contains("Hier zijn producten.", reply);
    }

    [Fact]
    public void No_warn_when_languages_match()
    {
        var session = new StoreChatSession { PreferredLanguage = "fr" };
        var reply = SalesLanguageMismatch.MaybePrependWarning(
            session,
            "il me faut des matériaux pour réparer mon toit",
            "Voici des produits.");

        Assert.Equal("Voici des produits.", reply);
    }

    [Fact]
    public void Skips_short_or_cta_text()
    {
        Assert.Null(SalesLanguageMismatch.DetectMessageLanguage("ok"));
        Assert.Null(SalesLanguageMismatch.DetectMessageLanguage("Nieuw project"));
        Assert.Null(SalesLanguageMismatch.DetectMessageLanguage("Volgende stap"));
    }
}
