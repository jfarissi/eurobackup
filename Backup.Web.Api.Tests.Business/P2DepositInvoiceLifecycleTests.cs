using Backup.Web.Api.Server.Services.Sales;

namespace Backup.Web.Api.Tests.Business
{
    /// <summary>RG-AA1–4 Facture d'acompte.</summary>
    public class P2DepositInvoiceLifecycleTests
    {
        [Fact]
        public void RejectIfDepositOrderUnusable_BlocksCancelledOrder()
        {
            Assert.NotNull(DocumentLifecycleRules.RejectIfDepositOrderUnusable("Cancelled"));
            Assert.Null(DocumentLifecycleRules.RejectIfDepositOrderUnusable("Confirmed"));
        }

        [Fact]
        public void RejectIfDepositAmountInvalid_RequiresPositiveHt()
        {
            Assert.NotNull(DocumentLifecycleRules.RejectIfDepositAmountInvalid(0));
            Assert.NotNull(DocumentLifecycleRules.RejectIfDepositAmountInvalid(-1));
            Assert.Null(DocumentLifecycleRules.RejectIfDepositAmountInvalid(10));
        }

        [Fact]
        public void RejectIfDepositExceedsOrder_BlocksOverCap()
        {
            var err = DocumentLifecycleRules.RejectIfDepositExceedsOrder(120m, 100m, "CC-1");

            Assert.NotNull(err);
            Assert.Contains("CC-1", err);
        }

        [Fact]
        public void RejectIfDepositExceedsOrder_AllowsWithinCap()
        {
            Assert.Null(DocumentLifecycleRules.RejectIfDepositExceedsOrder(100m, 100m, "CC-1"));
        }

        [Theory]
        [InlineData("Validated")]
        [InlineData("Applied")]
        [InlineData("Cancelled")]
        public void RejectIfDepositCannotValidate_BlocksNonDraft(string status)
        {
            Assert.NotNull(DocumentLifecycleRules.RejectIfDepositCannotValidate(status));
        }

        [Fact]
        public void RejectIfDepositCannotValidate_AllowsDraft()
        {
            Assert.Null(DocumentLifecycleRules.RejectIfDepositCannotValidate("Draft"));
        }

        [Theory]
        [InlineData("Draft")]
        [InlineData("Applied")]
        [InlineData("Cancelled")]
        public void RejectIfDepositCannotApply_BlocksInvalid(string status)
        {
            Assert.NotNull(DocumentLifecycleRules.RejectIfDepositCannotApply(status));
        }

        [Fact]
        public void RejectIfDepositCannotApply_AllowsValidated()
        {
            Assert.Null(DocumentLifecycleRules.RejectIfDepositCannotApply("Validated"));
        }

        [Fact]
        public void RejectIfDepositCannotCancel_BlocksApplied()
        {
            var err = DocumentLifecycleRules.RejectIfDepositCannotCancel("Applied");

            Assert.NotNull(err);
            Assert.Contains("appliqué", err);
        }

        [Theory]
        [InlineData("Draft")]
        [InlineData("Validated")]
        public void RejectIfDepositCannotCancel_AllowsPreApply(string status)
        {
            Assert.Null(DocumentLifecycleRules.RejectIfDepositCannotCancel(status));
        }
    }
}
