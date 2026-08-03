using Backup.Web.Api.Server.Services.Sales;

namespace Backup.Web.Api.Tests.Business
{
    public class ProvisionalDocumentNumberTests
    {
        [Fact]
        public void Create_StartsWithDraftPrefix()
        {
            var number = ProvisionalDocumentNumber.Create();

            Assert.StartsWith("DRAFT-", number);
        }

        [Fact]
        public void Create_GeneratesUniqueNumbers()
        {
            var first = ProvisionalDocumentNumber.Create();
            var second = ProvisionalDocumentNumber.Create();

            Assert.NotEqual(first, second);
        }

        [Theory]
        [InlineData("DRAFT-abc123")]
        [InlineData("draft-abc123")]
        [InlineData("BROUILLON-xyz")]
        [InlineData("brouillon-xyz")]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void IsProvisional_ReturnsTrue_ForProvisionalNumbers(string? number)
        {
            Assert.True(ProvisionalDocumentNumber.IsProvisional(number));
        }

        [Theory]
        [InlineData("FAC-2026-0001")]
        [InlineData("DV-2026-0001")]
        [InlineData("BL-2026-0042")]
        public void IsProvisional_ReturnsFalse_ForFinalNumbers(string number)
        {
            Assert.False(ProvisionalDocumentNumber.IsProvisional(number));
        }
    }
}
