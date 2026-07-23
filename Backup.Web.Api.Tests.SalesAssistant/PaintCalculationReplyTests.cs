using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Services.SalesAssistant;
using Backup.Web.Api.Server.Services.StoreChat;
using Moq;

namespace Backup.Web.Api.Tests.SalesAssistant;

/// <summary>
/// Régression : calcul peinture (m² / L) doit s'afficher même si Outcome = Generic
/// (recherche catalogue peinture ≠ Domain réservé au parcours mur).
/// </summary>
public class PaintCalculationReplyTests
{
    private const string PaintHouse =
        "j'ai besoin de peindre ma maison 6 chambre de 2m de haut et 2m de longeur , salle de bain 2m de haut et 1.5 de longeur couloires 6m de longeur et 2m de hauteru";

    [Fact]
    public void Compose_paint_house_shows_calc_when_outcome_is_generic()
    {
        var detector = new SalesContextDetector(Mock.Of<IStorageBroker>());
        var session = new StoreChatSession();
        detector.DetectDomain(session, PaintHouse);
        detector.ParseProjectDimensions(session, PaintHouse);

        Assert.Equal("painting", session.ActiveProjectDomainId);
        Assert.True(session.PaintAreaM2 is >= 80 and <= 200, $"area={session.PaintAreaM2}");

        var products = new List<StoreChatProductSuggestionDto>
        {
            new()
            {
                ProductId = "1",
                Name = "Muurverf Latex Mat Blanc 10L",
                Category = "Verf",
                Price = 45
            }
        };

        var reply = new SalesDeterministicReply().Compose(
            aiReply: null,
            products,
            session,
            new ProductSearchFilter
            {
                Outcome = ProductSearchOutcome.Generic,
                TotalMatches = 234
            },
            PaintHouse);

        Assert.Contains("m²", reply, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("L", reply, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("peindre", reply, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Voici 1 produit(s) du catalogue", reply, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Enrich_path_parse_project_dimensions_fills_paint_area()
    {
        // Régression pipeline StoreChat : DetectDomain + ParseProjectDimensions
        // (EnrichProjectContextAsync n'appelait que ParseWallDimensions).
        var detector = new SalesContextDetector(Mock.Of<IStorageBroker>());
        var session = new StoreChatSession();
        detector.DetectDomain(session, PaintHouse);
        detector.ParseProjectDimensions(session, PaintHouse);
        Assert.True(session.PaintAreaM2 is >= 80 and <= 200, $"PaintAreaM2={session.PaintAreaM2}");
        Assert.False(string.IsNullOrWhiteSpace(new SalesDeterministicReply().BuildCalculationSummary(session)));
    }
}
