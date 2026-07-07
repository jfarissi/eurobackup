using System;
using System.Globalization;
using System.Linq;
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

    public class ErpProductByReference
    {
        [JsonPropertyName("Reference")]
        public string Reference { get; set; } = string.Empty;

        [JsonPropertyName("UnitPrice")]
        public string UnitPrice { get; set; } = string.Empty;
    }

    public class ErpPricingService : IErpPricingService
    {
        private const string StockByReferenceSuffix = "getProductStockByReference";
        private const string ProductsByReferenceSuffix = "getProductsByReference";

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

            productCode = productCode.Trim();

            try
            {
                // WCF/IIS returns 404 for encoded slashes in {Reference}; skip direct call.
                if (!productCode.Contains('/'))
                {
                    var stockPrice = await TryGetPriceFromStockByReferenceAsync(productCode, ct);
                    if (stockPrice.HasValue)
                        return stockPrice;
                }

                return await TryGetPriceFromProductsByReferenceAsync(productCode, ct);
            }
            catch
            {
                return null;
            }
        }

        private async Task<decimal?> TryGetPriceFromStockByReferenceAsync(string productCode, CancellationToken ct)
        {
            var baseUrl = (_options.BaseUrl ?? string.Empty).TrimEnd('/');
            var url = $"{baseUrl}/{Uri.EscapeDataString(productCode)}";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            var response = await _httpClient.SendAsync(request, ct);

            if (!response.IsSuccessStatusCode)
                return null;

            var content = await response.Content.ReadAsStringAsync(ct);
            var items = JsonSerializer.Deserialize<ErpProductStock[]>(content);
            if (items == null || items.Length == 0)
                return null;

            return ParsePrice(items[0].UnitPrice);
        }

        private async Task<decimal?> TryGetPriceFromProductsByReferenceAsync(string productCode, CancellationToken ct)
        {
            var searchPrefix = productCode.Contains('/')
                ? productCode.Split('/')[0]
                : productCode;

            if (string.IsNullOrWhiteSpace(searchPrefix))
                return null;

            var baseUrl = ResolveProductsByReferenceBaseUrl();
            var url = $"{baseUrl}/{Uri.EscapeDataString(searchPrefix)}";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            var response = await _httpClient.SendAsync(request, ct);

            if (!response.IsSuccessStatusCode)
                return null;

            var content = await response.Content.ReadAsStringAsync(ct);
            var items = JsonSerializer.Deserialize<ErpProductByReference[]>(content);
            if (items == null || items.Length == 0)
                return null;

            var match = items.FirstOrDefault(p =>
                string.Equals(p.Reference?.Trim(), productCode, StringComparison.OrdinalIgnoreCase));

            return match == null ? null : ParsePrice(match.UnitPrice);
        }

        private string ResolveProductsByReferenceBaseUrl()
        {
            var stockUrl = (_options.BaseUrl ?? string.Empty).TrimEnd('/');
            if (stockUrl.EndsWith(StockByReferenceSuffix, StringComparison.OrdinalIgnoreCase))
            {
                return stockUrl[..^StockByReferenceSuffix.Length] + ProductsByReferenceSuffix;
            }

            if (!string.IsNullOrWhiteSpace(_options.ProductsByReferenceUrl))
                return _options.ProductsByReferenceUrl.TrimEnd('/');

            return stockUrl.Replace(
                StockByReferenceSuffix,
                ProductsByReferenceSuffix,
                StringComparison.OrdinalIgnoreCase);
        }

        private static decimal? ParsePrice(string? priceStr)
        {
            if (string.IsNullOrWhiteSpace(priceStr))
                return null;

            priceStr = priceStr.Replace(",", ".");
            return decimal.TryParse(priceStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var price)
                ? price
                : null;
        }
    }
}
