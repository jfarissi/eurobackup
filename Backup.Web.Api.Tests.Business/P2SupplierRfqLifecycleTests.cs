using Backup.Web.Api.Server.Services.Sales;

namespace Backup.Web.Api.Tests.Business
{
    /// <summary>RG-DPF1–4 Demande de prix fournisseur.</summary>
    public class P2SupplierRfqLifecycleTests
    {
        [Fact]
        public void RejectIfRfqCannotSend_DraftOnly()
        {
            Assert.Null(DocumentLifecycleRules.RejectIfRfqCannotSend("Draft"));
            Assert.NotNull(DocumentLifecycleRules.RejectIfRfqCannotSend("Sent"));
        }

        [Fact]
        public void RejectIfRfqCannotAwait_SentOnly()
        {
            Assert.Null(DocumentLifecycleRules.RejectIfRfqCannotAwait("Sent"));
            Assert.NotNull(DocumentLifecycleRules.RejectIfRfqCannotAwait("Draft"));
            Assert.NotNull(DocumentLifecycleRules.RejectIfRfqCannotAwait("Awaiting"));
        }

        [Theory]
        [InlineData("Cancelled")]
        [InlineData("Processed")]
        public void RejectIfRfqCannotCancel_BlocksTerminal(string status)
        {
            Assert.NotNull(DocumentLifecycleRules.RejectIfRfqCannotCancel(status));
        }

        [Theory]
        [InlineData("Draft")]
        [InlineData("Sent")]
        [InlineData("Awaiting")]
        public void RejectIfRfqCannotCancel_AllowsOpen(string status)
        {
            Assert.Null(DocumentLifecycleRules.RejectIfRfqCannotCancel(status));
        }

        [Theory]
        [InlineData("Processed")]
        [InlineData("Cancelled")]
        public void RejectIfRfqCannotConvert_BlocksTerminal(string status)
        {
            Assert.NotNull(DocumentLifecycleRules.RejectIfRfqCannotConvert(status));
        }

        [Theory]
        [InlineData("Draft")]
        [InlineData("Sent")]
        [InlineData("Awaiting")]
        public void RejectIfRfqCannotConvert_AllowsOpen(string status)
        {
            Assert.Null(DocumentLifecycleRules.RejectIfRfqCannotConvert(status));
        }
    }
}
