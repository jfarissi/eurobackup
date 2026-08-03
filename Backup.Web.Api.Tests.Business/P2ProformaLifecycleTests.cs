using Backup.Web.Api.Server.Services.Sales;

namespace Backup.Web.Api.Tests.Business
{
    /// <summary>RG-PF1–4 Proforma lifecycle.</summary>
    public class P2ProformaLifecycleTests
    {
        [Fact]
        public void RejectIfNotDraft_AllowsDraft()
        {
            Assert.Null(DocumentLifecycleRules.RejectIfNotDraft("Draft", "proforma"));
        }

        [Theory]
        [InlineData("Sent")]
        [InlineData("Cancelled")]
        public void RejectIfNotDraft_BlocksNonDraft(string status)
        {
            Assert.NotNull(DocumentLifecycleRules.RejectIfNotDraft(status, "proforma"));
        }

        [Fact]
        public void RejectIfProformaCannotSend_AllowsDraftOnly()
        {
            Assert.Null(DocumentLifecycleRules.RejectIfProformaCannotSend("Draft"));
            Assert.NotNull(DocumentLifecycleRules.RejectIfProformaCannotSend("Sent"));
        }

        [Fact]
        public void RejectIfProformaCannotCancel_BlocksAlreadyCancelled()
        {
            Assert.NotNull(DocumentLifecycleRules.RejectIfProformaCannotCancel("Cancelled"));
            Assert.Null(DocumentLifecycleRules.RejectIfProformaCannotCancel("Sent"));
        }

        [Fact]
        public void RejectIfProformaCannotDelete_DraftOnly()
        {
            Assert.Null(DocumentLifecycleRules.RejectIfProformaCannotDelete("Draft"));
            Assert.NotNull(DocumentLifecycleRules.RejectIfProformaCannotDelete("Sent"));
        }
    }
}
