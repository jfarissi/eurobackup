using Backup.Web.Api.Server.Services.Sales;

namespace Backup.Web.Api.Tests.Business
{
    /// <summary>RG-BL1 / P1 : BL uniquement depuis commande confirmée.</summary>
    public class P1BlOrderConfirmedGateTests
    {
        [Theory]
        [InlineData("Draft")]
        [InlineData("Pending")]
        [InlineData("Cancelled")]
        [InlineData(null)]
        [InlineData("")]
        public void RejectIfOrderNotConfirmedForDelivery_BlocksUnconfirmed(string? status)
        {
            var err = SalesBusinessRules.RejectIfOrderNotConfirmedForDelivery(status);

            Assert.NotNull(err);
            Assert.Contains("confirmée", err);
        }

        [Theory]
        [InlineData("Confirmed")]
        [InlineData("PartiallyDelivered")]
        [InlineData("Delivered")]
        [InlineData("PartiallyInvoiced")]
        [InlineData("Invoiced")]
        public void RejectIfOrderNotConfirmedForDelivery_AllowsCommitted(string status)
        {
            Assert.Null(SalesBusinessRules.RejectIfOrderNotConfirmedForDelivery(status));
        }
    }
}
