using System;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace Backup.Web.Api.Server.Services.Pricing
{
    public class ErpProductStock
    {
        [JsonPropertyName("UnitPrice")]
        public string UnitPrice { get; set; } = string.Empty;
    }

    public class ErpPricingService : IErpPricingService
    {
        private readonly HttpClient _httpClient;
        private readonly ErpPricingOptions _options;

        public ErpPricingService(HttpClient httpClient, IOptions<ErpPricingOptions> options)
        {
            _httpClient = httpClient;
            _options = options.Value ?? new ErpPricingOptions();
        }

        public async Task<decimal?> GetProductPriceAsync(string productCode, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(productCode))
                return null;

            try
            {
                var baseUrl = (_options.BaseUrl ?? string.Empty).TrimEnd('/');
                var url = $"{baseUrl}/{Uri.EscapeDataString(productCode)}";
                var response = await _httpClient.GetAsync(url, ct);

                if (!response.IsSuccessStatusCode)
                    return null;

                var content = await response.Content.ReadAsStringAsync(ct);
                var items = JsonSerializer.Deserialize<ErpProductStock[]>(content);

                if (items == null || items.Length == 0)
                    return null;

                var priceStr = items[0].UnitPrice;
                if (string.IsNullOrWhiteSpace(priceStr))
                    return null;

                priceStr = priceStr.Replace(",", ".");
                if (decimal.TryParse(priceStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var price))
                {
                    return price;
                }

                return null;
            }
            catch
            {
                // Return null on failure (e.g. timeout, network error)
                return null;
            }
        }
    }
}
