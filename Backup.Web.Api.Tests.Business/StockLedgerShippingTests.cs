using Backup.Web.Api.Server.Services.Stock;

namespace Backup.Web.Api.Tests.Business
{
    public class StockLedgerShippingTests
    {
        [Theory]
        [InlineData("FDP")]
        [InlineData("fdp")]
        [InlineData("SHIPPING")]
        [InlineData("shipping")]
        [InlineData("Shipping")]
        public void IsShippingFeeKey_ReturnsTrue_ForShippingKeys(string productKey)
        {
            Assert.True(StockLedger.IsShippingFeeKey(productKey));
        }

        [Theory]
        [InlineData("14293")]
        [InlineData("FF Group 14293")]
        [InlineData("SKU-001")]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void IsShippingFeeKey_ReturnsFalse_ForNormalSku(string? productKey)
        {
            Assert.False(StockLedger.IsShippingFeeKey(productKey));
        }
    }
}
