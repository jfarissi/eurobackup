using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace Backup.Web.Api.Server.Services.Pricing
{
    public class ErpProductStock
    {
        [JsonPropertyName("UnitPrice")]
        public string UnitPrice { get; set; } = string.Empty;

        [JsonPropertyName("Reference")]
        public string? Reference { get; set; }
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
            var candidates = BuildReferenceCandidates(productCode);

            try
            {
                foreach (var candidate in candidates)
                {
                    if (candidate.Contains('/'))
                        continue;

                    var stockPrice = await TryGetPriceFromStockByReferenceAsync(candidate, ct);
                    if (stockPrice.HasValue)
                        return stockPrice;
                }

                // Recherche par référence (préfixe) + matching souple (Benman / FF-Group)
                foreach (var candidate in candidates)
                {
                    var price = await TryGetPriceFromProductsByReferenceAsync(candidate, productCode, ct);
                    if (price.HasValue)
                        return price;
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Génère les variantes de référence à tester :
        /// code brut + "Benman {code}" + "FF-Group {code}" + etc.
        /// </summary>
        private IReadOnlyList<string> BuildReferenceCandidates(string productCode)
        {
            var list = new List<string> { productCode };
            var stripped = StripBrandPrefix(productCode);
            if (!string.Equals(stripped, productCode, StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(stripped))
            {
                list.Add(stripped);
            }

            var core = stripped;
            foreach (var prefix in GetBrandPrefixes())
            {
                var withPrefix = $"{prefix} {core}";
                if (!list.Any(c => string.Equals(c, withPrefix, StringComparison.OrdinalIgnoreCase)))
                    list.Add(withPrefix);
            }

            return list;
        }

        private IEnumerable<string> GetBrandPrefixes()
        {
            var prefixes = _options.ReferenceBrandPrefixes;
            if (prefixes == null || prefixes.Length == 0)
            {
                return new[] { "Benman", "FF-Group", "FF Group", "FFGroup" };
            }

            return prefixes
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(p => p.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase);
        }

        private string StripBrandPrefix(string reference)
        {
            var value = reference.Trim();
            foreach (var prefix in GetBrandPrefixes().OrderByDescending(p => p.Length))
            {
                if (value.StartsWith(prefix + " ", StringComparison.OrdinalIgnoreCase))
                    return value[(prefix.Length + 1)..].Trim();
                if (value.StartsWith(prefix + "-", StringComparison.OrdinalIgnoreCase))
                    return value[(prefix.Length + 1)..].Trim();
                if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                    && value.Length > prefix.Length
                    && char.IsDigit(value[prefix.Length]))
                {
                    return value[prefix.Length..].Trim();
                }
            }

            return value;
        }

        /// <summary>
        /// Match exact, ou même code après retrait du préfixe marque,
        /// ou la référence ERP se termine par le code facture (ex: "Benman 70310" ↔ "70310").
        /// </summary>
        internal bool ReferencesMatch(string invoiceCode, string? erpReference)
        {
            if (string.IsNullOrWhiteSpace(erpReference))
                return false;

            var inv = invoiceCode.Trim();
            var erp = erpReference.Trim();

            if (string.Equals(inv, erp, StringComparison.OrdinalIgnoreCase))
                return true;

            var invCore = StripBrandPrefix(inv);
            var erpCore = StripBrandPrefix(erp);

            if (string.Equals(invCore, erpCore, StringComparison.OrdinalIgnoreCase))
                return true;

            // "Benman 70310" se termine par " 70310" ou "70310"
            if (erp.EndsWith(" " + invCore, StringComparison.OrdinalIgnoreCase)
                || erp.EndsWith("-" + invCore, StringComparison.OrdinalIgnoreCase))
                return true;

            // Dernier token numérique / alphanumérique de la ref ERP
            var lastToken = Regex.Split(erp, @"[\s/:]+")
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .LastOrDefault();
            if (!string.IsNullOrWhiteSpace(lastToken)
                && string.Equals(StripBrandPrefix(lastToken!), invCore, StringComparison.OrdinalIgnoreCase))
                return true;

            return false;
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
            if (string.IsNullOrWhiteSpace(content) || content == "null")
                return null;

            var items = JsonSerializer.Deserialize<ErpProductStock[]>(content);
            if (items == null || items.Length == 0)
                return null;

            return ParsePrice(items[0].UnitPrice);
        }

        private async Task<decimal?> TryGetPriceFromProductsByReferenceAsync(
            string searchKey,
            string originalInvoiceCode,
            CancellationToken ct)
        {
            var searchPrefix = searchKey.Contains('/')
                ? searchKey.Split('/')[0]
                : searchKey;

            if (string.IsNullOrWhiteSpace(searchPrefix))
                return null;

            var baseUrl = ResolveProductsByReferenceBaseUrl();
            var url = $"{baseUrl}/{Uri.EscapeDataString(searchPrefix)}";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            var response = await _httpClient.SendAsync(request, ct);

            if (!response.IsSuccessStatusCode)
                return null;

            var content = await response.Content.ReadAsStringAsync(ct);
            if (string.IsNullOrWhiteSpace(content) || content == "null")
                return null;

            var items = JsonSerializer.Deserialize<ErpProductByReference[]>(content);
            if (items == null || items.Length == 0)
                return null;

            // 1) Match exact / préfixe marque / dernier token
            var match = items.FirstOrDefault(p => ReferencesMatch(originalInvoiceCode, p.Reference))
                ?? items.FirstOrDefault(p => ReferencesMatch(searchKey, p.Reference));

            // 2) Un seul résultat après filtre sur le core numérique
            if (match == null)
            {
                var core = StripBrandPrefix(originalInvoiceCode);
                var filtered = items
                    .Where(p => ReferencesMatch(core, p.Reference))
                    .ToList();
                if (filtered.Count == 1)
                    match = filtered[0];
            }

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
