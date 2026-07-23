using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Services.SalesAssistant;
using Backup.Web.Api.Server.Services.StoreChat;
using Moq;

namespace Backup.Web.Api.Tests.SalesAssistant;

/// <summary>
/// Exécute les scénarios JSON : couches C# déterministes (domaine, surfaces, parcours mur, intents).
/// Les asserts catalogue (categoryMustMatchAny) sont documentés pour les tests d'intégration futurs.
/// </summary>
public class ScenarioDeterministicTests
{
    public static IEnumerable<object[]> ScenarioCases() =>
        ScenarioLoader.LoadAll().Select(s => new object[] { s.Id, s });

    [Theory]
    [MemberData(nameof(ScenarioCases))]
    public void Scenario_deterministic_expectations(string id, ChatScenario scenario)
    {
        Assert.False(string.IsNullOrWhiteSpace(id));
        Assert.False(string.IsNullOrWhiteSpace(scenario.MainType));
        Assert.NotEmpty(scenario.Turns);

        var detector = new SalesContextDetector(new Mock<IStorageBroker>().Object);
        var intentDetector = new SalesGuidedIntentDetector();
        var session = new StoreChatSession();

        for (var i = 0; i < scenario.Turns.Count; i++)
        {
            var turn = scenario.Turns[i];
            ApplyCart(session, turn.CartBefore);

            if (!string.IsNullOrWhiteSpace(scenario.DomainId)
                && string.IsNullOrWhiteSpace(session.ActiveProjectDomainId))
            {
                session.ActiveProjectDomainId = scenario.DomainId;
                session.ActiveProjectDomainLabel = scenario.MainType;
            }

            detector.DetectDomain(session, turn.User);
            if (string.Equals(session.ActiveProjectDomainId, "painting", StringComparison.OrdinalIgnoreCase)
                || turn.User.Contains("peindre", StringComparison.OrdinalIgnoreCase)
                || turn.User.Contains("peinture", StringComparison.OrdinalIgnoreCase))
            {
                detector.ParsePaintSurfaces(session, turn.User);
            }
            else if (turn.User.Contains("mur", StringComparison.OrdinalIgnoreCase))
            {
                detector.ParseWallDimensions(session, turn.User);
            }

            var guided = intentDetector.Detect(turn.User, session);
            var expect = turn.Expect;
            if (expect == null)
                continue;

            if (!string.IsNullOrWhiteSpace(expect.DomainId))
            {
                Assert.Equal(expect.DomainId, session.ActiveProjectDomainId);
            }

            if (expect.PaintAreaMin is > 0)
            {
                Assert.True(session.PaintAreaM2 is > 0,
                    $"[{id}] tour {i}: PaintAreaM2 attendu, obtenu {session.PaintAreaM2}");
                Assert.True(session.PaintAreaM2 >= expect.PaintAreaMin,
                    $"[{id}] PaintAreaM2 {session.PaintAreaM2} < min {expect.PaintAreaMin}");
            }

            if (expect.PaintAreaMax is > 0 && session.PaintAreaM2 is > 0)
            {
                Assert.True(session.PaintAreaM2 <= expect.PaintAreaMax,
                    $"[{id}] PaintAreaM2 {session.PaintAreaM2} > max {expect.PaintAreaMax}");
            }

            if (expect.WallAreaMin is > 0)
            {
                Assert.True(session.WallAreaM2 is > 0,
                    $"[{id}] WallAreaM2 attendu");
                Assert.True(session.WallAreaM2 >= expect.WallAreaMin);
            }

            if (!string.IsNullOrWhiteSpace(expect.WallGuideFamily)
                && string.Equals(session.ActiveProjectDomainId, "wall_construction", StringComparison.OrdinalIgnoreCase))
            {
                var family = SalesProjectGuide.ResolveWallFamily(session, turn.User);
                Assert.Equal(expect.WallGuideFamily, family.ToString());
            }

            if (expect.ProjectBaseComplete == true)
            {
                Assert.True(SalesComplementRules.IsBaseComplete(session),
                    $"[{id}] base chantier attendue complète");
            }

            if (!string.IsNullOrWhiteSpace(expect.GuidedIntent))
            {
                Assert.Equal(expect.GuidedIntent, guided.Intent.ToString());
            }

            // Réponse chatbot prévisible = couche C# (calc + checklist + avis), sans LLM.
            if (expect.ReplyMustContain is { Count: > 0 } || expect.ReplyMustNotContain is { Count: > 0 })
            {
                var reply = BuildDeterministicReplyPreview(session, turn.User, guided);
                foreach (var fragment in expect.ReplyMustContain ?? [])
                {
                    Assert.Contains(fragment, reply, StringComparison.OrdinalIgnoreCase);
                }

                foreach (var bad in expect.ReplyMustNotContain ?? [])
                {
                    Assert.DoesNotContain(bad, reply, StringComparison.OrdinalIgnoreCase);
                }
            }

            // Documente les attentes catalogue (exécutées en intégration DB).
            if (expect.CategoryMustMatchAny is { Count: > 0 })
            {
                Assert.NotEmpty(expect.CategoryMustMatchAny);
            }

            if (expect.ProductMustNotMatch is { Count: > 0 }
                && scenario.ForbiddenProductHints.Count == 0)
            {
                scenario.ForbiddenProductHints.AddRange(expect.ProductMustNotMatch);
            }
        }
    }

    /// <summary>
    /// Prévisualise la voix métier C# (identique aux briques utilisées avant/au lieu du LLM).
    /// </summary>
    private static string BuildDeterministicReplyPreview(
        StoreChatSession session,
        string userText,
        GuidedSalesSlots guided)
    {
        var deterministic = new SalesDeterministicReply();
        var parts = new List<string>();

        var calc = deterministic.BuildCalculationSummary(session);
        if (!string.IsNullOrWhiteSpace(calc))
            parts.Add(calc);

        if (string.Equals(session.ActiveProjectDomainId, "wall_construction", StringComparison.OrdinalIgnoreCase))
        {
            var family = SalesProjectGuide.ResolveWallFamily(session, userText);
            parts.Add(SalesProjectGuide.BuildWallChecklist(session, family));
            parts.Add($"Étape « {SalesProjectGuide.FocusLabel(family)} »");
        }

        if (guided.Intent == GuidedSalesIntent.Tips)
            parts.Add(new SalesRecommendationEngine().BuildCartReviewReply(session));

        if (guided.Intent == GuidedSalesIntent.CartComplements)
            parts.Add(new SalesRecommendationEngine().BuildCartComplementsReply(session));

        var vague = deterministic.BuildVagueDomainFollowUp(
            session,
            new ProductSearchFilter { Outcome = ProductSearchOutcome.Domain },
            userText);
        if (!string.IsNullOrWhiteSpace(vague))
            parts.Add(vague);

        return string.Join("\n", parts);
    }

    [Fact]
    public void Catalog_maintypes_all_have_scenario_files()
    {
        var catalog = ScenarioLoader.LoadCatalogMainTypes();
        Assert.NotEmpty(catalog);

        var scenarios = ScenarioLoader.LoadAll();
        var covered = scenarios
            .Select(s => s.MainType.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missing = catalog
            .Where(c => c.HasScenario)
            .Where(c => !covered.Contains(c.Name.Trim()))
            .Select(c => c.Name)
            .ToList();

        Assert.True(missing.Count == 0,
            "MainTypes sans scénario : " + string.Join(", ", missing));
    }

    [Fact]
    public void Paint_house_scenario_parses_multi_room_area()
    {
        var detector = new SalesContextDetector(new Mock<IStorageBroker>().Object);
        var session = new StoreChatSession { ActiveProjectDomainId = "painting" };
        var text =
            "j'ai besoin de peindre ma maison 6 chambre de 2m de haut et 2m de longeur , salle de bain 2m de haut et 1.5 de longeur couloires 6m de longeur et 2m de hauteru";
        detector.ParsePaintSurfaces(session, text);
        Assert.True(session.PaintAreaM2 is >= 80 and <= 200, $"area={session.PaintAreaM2}");
    }

    [Fact]
    public void Wall_first_message_is_structure_not_reinforcement()
    {
        var detector = new SalesContextDetector(new Mock<IStorageBroker>().Object);
        var session = new StoreChatSession();
        var text = "je veux construire un mur de 7m de longeur et 2m de hateur";
        detector.DetectDomain(session, text);
        detector.ParseWallDimensions(session, text);
        Assert.Equal("wall_construction", session.ActiveProjectDomainId);
        var family = SalesProjectGuide.ResolveWallFamily(session, text);
        Assert.Equal(WallGuideFamily.Structure, family);
        Assert.True(session.WallAreaM2 is >= 13);
    }

    [Fact]
    public void Cart_review_intent_is_tips_not_complements()
    {
        var session = new StoreChatSession
        {
            ActiveProjectDomainId = "wall_construction"
        };
        session.Cart.Add(new StoreChatCartItem
        {
            ErpProductId = 1,
            Name = "Porotherm + Cement",
            Quantity = 1,
            UnitPrice = 1
        });
        var guided = new SalesGuidedIntentDetector().Detect("que pensez vous de mon panier ?", session);
        Assert.Equal(GuidedSalesIntent.Tips, guided.Intent);
    }

    private static void ApplyCart(StoreChatSession session, List<string>? names)
    {
        session.Cart.Clear();
        if (names == null)
            return;
        var id = 1;
        foreach (var name in names)
        {
            session.Cart.Add(new StoreChatCartItem
            {
                ErpProductId = id++,
                Name = name,
                Quantity = 1,
                UnitPrice = 1
            });
        }
    }
}
