using System;
using Backup.Web.Api.Server.Services.Sales;

namespace Backup.Web.Api.Tests.Business
{
    /// <summary>RG-BRF / AF / AC5 / LT — retours fournisseur, avoirs, refund, lettrage.</summary>
    public class P2PurchasesAndCreditLifecycleTests
    {
        [Fact]
        public void RejectIfSupplierReturnCannotShip_DraftOnly()
        {
            Assert.Null(DocumentLifecycleRules.RejectIfSupplierReturnCannotShip("Draft"));
            Assert.NotNull(DocumentLifecycleRules.RejectIfSupplierReturnCannotShip("Shipped"));
        }

        [Fact]
        public void RejectIfSupplierReturnCannotCancel_BlocksWhenCreditExists()
        {
            var err = DocumentLifecycleRules.RejectIfSupplierReturnCannotCancel("Shipped", hasCreditNote: true);

            Assert.NotNull(err);
            Assert.Contains("avoir", err);
        }

        [Fact]
        public void RejectIfSupplierReturnCannotCancel_AllowsShippedWithoutCredit()
        {
            Assert.Null(DocumentLifecycleRules.RejectIfSupplierReturnCannotCancel("Shipped", hasCreditNote: false));
        }

        [Fact]
        public void RejectIfSupplierReturnCannotCreateCreditNote_BlocksCancelledAndDuplicate()
        {
            Assert.NotNull(DocumentLifecycleRules.RejectIfSupplierReturnCannotCreateCreditNote("Cancelled", false));
            Assert.NotNull(DocumentLifecycleRules.RejectIfSupplierReturnCannotCreateCreditNote("Shipped", true));
            Assert.Null(DocumentLifecycleRules.RejectIfSupplierReturnCannotCreateCreditNote("Shipped", false));
        }

        [Fact]
        public void RejectIfSupplierCreditCannotValidate_DraftOnly()
        {
            Assert.Null(DocumentLifecycleRules.RejectIfSupplierCreditCannotValidate("Draft"));
            Assert.NotNull(DocumentLifecycleRules.RejectIfSupplierCreditCannotValidate("Validated"));
        }

        [Theory]
        [InlineData("Applied")]
        [InlineData("Cancelled")]
        public void RejectIfSupplierCreditCannotApply_BlocksInvalid(string status)
        {
            Assert.NotNull(DocumentLifecycleRules.RejectIfSupplierCreditCannotApply(status));
        }

        [Theory]
        [InlineData("Draft")]
        [InlineData("Validated")]
        public void RejectIfSupplierCreditCannotApply_AllowsDraftOrValidated(string status)
        {
            Assert.Null(DocumentLifecycleRules.RejectIfSupplierCreditCannotApply(status));
        }

        [Fact]
        public void RejectIfSupplierCreditCannotCancel_BlocksApplied()
        {
            Assert.NotNull(DocumentLifecycleRules.RejectIfSupplierCreditCannotCancel("Applied"));
            Assert.Null(DocumentLifecycleRules.RejectIfSupplierCreditCannotCancel("Validated"));
        }

        [Fact]
        public void ValidateSupplierCreditCap_FailsWhenExceedsInvoice()
        {
            var err = DocumentLifecycleRules.ValidateSupplierCreditCap(100m, "FF-1", 60m, 50m);

            Assert.NotNull(err);
            Assert.Contains("FF-1", err);
        }

        [Fact]
        public void ValidateSupplierCreditCap_AllowsWithinInvoice()
        {
            Assert.Null(DocumentLifecycleRules.ValidateSupplierCreditCap(100m, "FF-1", 40m, 50m));
        }

        [Theory]
        [InlineData("Draft")]
        [InlineData("Applied")]
        [InlineData("Cancelled")]
        [InlineData("Refunded")]
        public void RejectIfCreditNoteCannotRefund_BlocksInvalid(string status)
        {
            Assert.NotNull(DocumentLifecycleRules.RejectIfCreditNoteCannotRefund(status));
        }

        [Fact]
        public void RejectIfCreditNoteCannotRefund_AllowsValidated()
        {
            Assert.Null(DocumentLifecycleRules.RejectIfCreditNoteCannotRefund("Validated"));
        }

        [Fact]
        public void RejectIfCannotUnletter_BlocksAfterFiscalEnd()
        {
            var err = DocumentLifecycleRules.RejectIfCannotUnletter(
                new DateTime(2026, 6, 30),
                new DateTime(2026, 7, 1));

            Assert.NotNull(err);
            Assert.Contains("clôturée", err);
        }

        [Fact]
        public void RejectIfCannotUnletter_AllowsOnFiscalEndDate()
        {
            Assert.Null(DocumentLifecycleRules.RejectIfCannotUnletter(
                new DateTime(2026, 6, 30),
                new DateTime(2026, 6, 30)));
        }

        [Fact]
        public void RejectIfCannotUnletter_AllowsWhenNoEndBound()
        {
            Assert.Null(DocumentLifecycleRules.RejectIfCannotUnletter(null, DateTime.UtcNow));
        }
    }
}
