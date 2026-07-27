using Backup.Web.Api.Server.Services.SalesAssistant;
using Backup.Web.Api.Server.Services.SalesAssistant.Guides;
using Backup.Web.Api.Server.Services.StoreChat;

namespace Backup.Web.Api.Tests.SalesAssistant;

public class ClientIntentCtaTests
{
    [Fact]
    public void Paint_next_after_paint_is_primer()
    {
        var session = new StoreChatSession { ActiveProjectDomainId = "painting" };
        session.Cart.Add(new StoreChatCartItem { ErpProductId = 1, Name = "Muurverf latex", Quantity = 1, UnitPrice = 1 });
        var step = PaintProjectGuide.Instance.ResolveNext(session, "étape suivante");
        Assert.Equal("primer", step.Id);
        Assert.False(PaintProjectGuide.Instance.IsComplete(session));
    }

    [Fact]
    public void Registry_resolves_all_project_domains()
    {
        foreach (var domain in new[]
                 {
                     "wall_construction", "painting", "tiling", "garden_cleaning",
                     "garden_landscaping", "garden_maintenance", "electrical", "roofing", "plumbing"
                 })
        {
            Assert.True(ProjectGuides.TryGet(domain, out var guide));
            Assert.Equal(domain, guide!.DomainId);
        }
    }

    [Fact]
    public void Wall_complete_grid_review_phrases_map_to_tips()
    {
        var session = new StoreChatSession { ActiveProjectDomainId = "wall_construction" };
        session.Cart.Add(new StoreChatCartItem { ErpProductId = 1, Name = "Snelbouw Porotherm", Quantity = 1, UnitPrice = 1 });
        session.Cart.Add(new StoreChatCartItem { ErpProductId = 2, Name = "Cement Wit", Quantity = 1, UnitPrice = 1 });
        session.Cart.Add(new StoreChatCartItem { ErpProductId = 3, Name = "Murfor Plat", Quantity = 1, UnitPrice = 1 });
        session.Cart.Add(new StoreChatCartItem { ErpProductId = 4, Name = "Emmer troffel", Quantity = 1, UnitPrice = 1 });

        Assert.True(SalesProjectGuide.IsWallGuideComplete(session));

        var detector = new SalesGuidedIntentDetector();
        Assert.Equal(GuidedSalesIntent.Tips, detector.Detect("c'est bon ?", session).Intent);
        Assert.Equal(GuidedSalesIntent.Tips, detector.Detect("c'est bon pour le panier ?", session).Intent);
    }

    [Fact]
    public void Wall_next_family_after_structure_is_binder()
    {
        var session = new StoreChatSession { ActiveProjectDomainId = "wall_construction" };
        session.Cart.Add(new StoreChatCartItem { ErpProductId = 1, Name = "Snelbouw Porotherm", Quantity = 1, UnitPrice = 1 });
        var family = SalesProjectGuide.ResolveWallFamily(session, "étape suivante");
        Assert.Equal(WallGuideFamily.Binder, family);
    }

    [Fact]
    public void Complete_checklist_has_no_focus_arrow()
    {
        var session = new StoreChatSession { ActiveProjectDomainId = "wall_construction" };
        session.Cart.Add(new StoreChatCartItem { ErpProductId = 1, Name = "Lijmblok", Quantity = 1, UnitPrice = 1 });
        session.Cart.Add(new StoreChatCartItem { ErpProductId = 2, Name = "Cement", Quantity = 1, UnitPrice = 1 });
        session.Cart.Add(new StoreChatCartItem { ErpProductId = 3, Name = "Murfor", Quantity = 1, UnitPrice = 1 });
        session.Cart.Add(new StoreChatCartItem { ErpProductId = 4, Name = "troffel", Quantity = 1, UnitPrice = 1 });

        var text = SalesProjectGuide.BuildWallChecklist(session, WallGuideFamily.Tools);
        Assert.Contains("Parcours mur complet", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("← à choisir maintenant", text, StringComparison.OrdinalIgnoreCase);
    }
}
