using Backup.Web.Api.Server.Services.SalesAssistant;
using Backup.Web.Api.Server.Services.StoreChat;

namespace Backup.Web.Api.Tests.SalesAssistant;

public class PaintQuantityEstimatorTests
{
    [Fact]
    public void Paint_list_does_not_suggest_27_of_1L_cans()
    {
        var session = new StoreChatSession
        {
            ActiveProjectDomainId = "painting",
            PaintAreaM2 = 132 // ≈ 27 L
        };
        var products = new List<StoreChatProductSuggestionDto>
        {
            new() { ProductId = "1", Name = "Trimetal Rollacryl Superlatex 001 10L", Category = "Verf" },
            new() { ProductId = "2", Name = "KEM ACRYL LATEX SATIN P 1L", Category = "Verf" },
            new() { ProductId = "3", Name = "Sygnal Paint Latex Muurverf 20kg", Category = "Verf" },
        };

        SalesQuantityEstimator.ApplySuggestedQuantities(products, session);

        Assert.Equal(3, products[0].SuggestedQuantity); // 27/10 → 3
        Assert.Equal(1, products[1].SuggestedQuantity); // petit format → 1 (pas 27)
        Assert.Equal(2, products[2].SuggestedQuantity); // 27/20 → 2
    }
}
