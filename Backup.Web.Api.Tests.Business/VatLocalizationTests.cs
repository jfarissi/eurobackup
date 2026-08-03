using Backup.Web.Api.Server.Services.Sales;

namespace Backup.Web.Api.Tests.Business
{
    public class VatLocalizationTests
    {
        [Theory]
        [InlineData("BE", 21)]
        [InlineData("Belgique", 21)]
        [InlineData("Belgium", 21)]
        [InlineData("FR", 20)]
        [InlineData("France", 20)]
        [InlineData("NL", 21)]
        [InlineData("Netherlands", 21)]
        [InlineData("DE", 19)]
        [InlineData("Germany", 19)]
        [InlineData("LU", 17)]
        [InlineData("Luxembourg", 17)]
        [InlineData("XX", 21)]
        [InlineData("Unknown Country", 21)]
        [InlineData(null, 21)]
        [InlineData("", 21)]
        public void DefaultRateForCountry_ReturnsExpectedRate(string? country, decimal expected)
        {
            Assert.Equal(expected, VatLocalization.DefaultRateForCountry(country));
        }

        [Fact]
        public void DefaultRateForCountry_IsCaseInsensitive()
        {
            Assert.Equal(20m, VatLocalization.DefaultRateForCountry("fr"));
            Assert.Equal(19m, VatLocalization.DefaultRateForCountry("de"));
        }
    }
}
