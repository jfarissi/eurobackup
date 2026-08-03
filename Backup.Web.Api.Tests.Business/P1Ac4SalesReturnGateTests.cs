using Backup.Web.Api.Server.Models.Entities;
using Backup.Web.Api.Server.Services.Sales;

namespace Backup.Web.Api.Tests.Business
{
    /// <summary>RG-AC4 / RG-BR1–5 : cycle BRC et gate avoir.</summary>
    public class P1Ac4SalesReturnGateTests
    {
        [Fact]
        public void RejectIfSalesReturnNotIntegrated_Skips_WhenNoReturnLinked()
        {
            Assert.Null(SalesBusinessRules.RejectIfSalesReturnNotIntegrated(null, null));
            Assert.Null(SalesBusinessRules.RejectIfSalesReturnNotIntegrated(null, 0));
        }

        [Fact]
        public void RejectIfSalesReturnNotIntegrated_Fails_WhenReturnMissing()
        {
            var err = SalesBusinessRules.RejectIfSalesReturnNotIntegrated(null, 42);

            Assert.NotNull(err);
            Assert.Contains("introuvable", err);
        }

        [Theory]
        [InlineData("Draft")]
        [InlineData("Received")]
        [InlineData("Controlled")]
        [InlineData("Cancelled")]
        public void RejectIfSalesReturnNotIntegrated_Fails_WhenNotIntegrated(string status)
        {
            var salesReturn = new SalesReturn { ReturnNumber = "BRC-1", Status = status };

            var err = SalesBusinessRules.RejectIfSalesReturnNotIntegrated(salesReturn, 7);

            Assert.NotNull(err);
            Assert.Contains("BRC-1", err);
            Assert.Contains("Intégré", err);
        }

        [Fact]
        public void RejectIfSalesReturnNotIntegrated_Allows_WhenIntegrated()
        {
            var salesReturn = new SalesReturn { ReturnNumber = "BRC-2", Status = "Integrated" };

            Assert.Null(SalesBusinessRules.RejectIfSalesReturnNotIntegrated(salesReturn, 7));
        }

        [Theory]
        [InlineData("Received")]
        [InlineData("Controlled")]
        [InlineData("Integrated")]
        [InlineData("Cancelled")]
        public void RejectIfSalesReturnCannotReceive_BlocksNonDraft(string status)
        {
            Assert.NotNull(SalesBusinessRules.RejectIfSalesReturnCannotReceive(status));
        }

        [Fact]
        public void RejectIfSalesReturnCannotReceive_AllowsDraft()
        {
            Assert.Null(SalesBusinessRules.RejectIfSalesReturnCannotReceive("Draft"));
        }

        [Theory]
        [InlineData("Draft")]
        [InlineData("Controlled")]
        [InlineData("Integrated")]
        public void RejectIfSalesReturnCannotControl_BlocksNonReceived(string status)
        {
            Assert.NotNull(SalesBusinessRules.RejectIfSalesReturnCannotControl(status));
        }

        [Fact]
        public void RejectIfSalesReturnCannotControl_AllowsReceived()
        {
            Assert.Null(SalesBusinessRules.RejectIfSalesReturnCannotControl("Received"));
        }

        [Theory]
        [InlineData("Draft")]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("Cancelled")]
        [InlineData("Integrated")]
        public void RejectIfSalesReturnCannotIntegrate_BlocksInvalid(string? status)
        {
            Assert.NotNull(SalesBusinessRules.RejectIfSalesReturnCannotIntegrate(status));
        }

        [Theory]
        [InlineData("Received")]
        [InlineData("Controlled")]
        public void RejectIfSalesReturnCannotIntegrate_AllowsReceivedOrControlled(string status)
        {
            Assert.Null(SalesBusinessRules.RejectIfSalesReturnCannotIntegrate(status));
        }

        [Fact]
        public void RejectIfSalesReturnCannotCancel_BlocksIntegrated()
        {
            var err = SalesBusinessRules.RejectIfSalesReturnCannotCancel("Integrated");

            Assert.NotNull(err);
            Assert.Contains("intégré", err);
        }

        [Fact]
        public void RejectIfSalesReturnCannotCancel_BlocksAlreadyCancelled()
        {
            Assert.NotNull(SalesBusinessRules.RejectIfSalesReturnCannotCancel("Cancelled"));
        }

        [Theory]
        [InlineData("Draft")]
        [InlineData("Received")]
        [InlineData("Controlled")]
        public void RejectIfSalesReturnCannotCancel_AllowsPreIntegration(string status)
        {
            Assert.Null(SalesBusinessRules.RejectIfSalesReturnCannotCancel(status));
        }
    }
}
