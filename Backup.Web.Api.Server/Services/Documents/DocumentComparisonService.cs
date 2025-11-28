using System.Text.RegularExpressions;
using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace Backup.Web.Api.Server.Services.Documents
{
    public interface IDocumentComparisonService
    {
        Task<ComparisonResult> CompareAsync(int invoiceId, int deliveryId, CancellationToken ct);
        Task<InvoicePriceComparisonResult> CompareInvoicesAsync(int invoice1Id, int invoice2Id, CancellationToken ct);
    }

    public class ComparisonResult
    {
        public int InvoiceId { get; set; }
        public int DeliveryId { get; set; }
        public List<ComparisonLine> Lines { get; set; } = new();
    }

    public class ComparisonLine
    {
        public string Product { get; set; } = string.Empty;
        public decimal InvoiceQty { get; set; }
        public decimal DeliveryQty { get; set; }
        public decimal Diff => InvoiceQty - DeliveryQty;
        public string Status => Diff == 0 ? "OK" : Diff > 0 ? "Manquant" : "Surplus";
        public decimal CurrentInvoiceUnitPrice { get; set; }
        public decimal PreviousInvoiceUnitPrice { get; set; }
        public decimal PriceDiff => CurrentInvoiceUnitPrice - PreviousInvoiceUnitPrice;
    }

    public class InvoicePriceComparisonResult
    {
        public int Invoice1Id { get; set; }
        public int Invoice2Id { get; set; }
        public List<InvoicePriceComparisonLine> Lines { get; set; } = new();
    }

    public class InvoicePriceComparisonLine
    {
        public string Product { get; set; } = string.Empty;
        public decimal Invoice1UnitPrice { get; set; }
        public decimal Invoice2UnitPrice { get; set; }
        public decimal PriceDiff => Invoice1UnitPrice - Invoice2UnitPrice;
    }

    public class DocumentComparisonService : IDocumentComparisonService
    {
        private readonly IStorageBroker storage;

        public DocumentComparisonService(IStorageBroker storage)
        {
            this.storage = storage;
        }

        public async Task<ComparisonResult> CompareAsync(int invoiceId, int deliveryId, CancellationToken ct)
        {
            var invoice = await storage.SelectDocumentByIdAsync(invoiceId) ?? throw new InvalidOperationException("Invoice not found");
            var delivery = await storage.SelectDocumentByIdAsync(deliveryId) ?? throw new InvalidOperationException("Delivery note not found");

            // Prefer structured lines if present
            var invLines = storage.SelectLinesByDocumentId(invoiceId).ToList();
            var delLines = storage.SelectLinesByDocumentId(deliveryId).ToList();
            
            // Find previous invoices from the same supplier for price comparison (same logic as price-diff endpoint)
            // Use same Key function as price-diff to ensure matching
            string Key(DocumentLine l)
            {
                if (!string.IsNullOrWhiteSpace(l.ProductCode)) return $"C:{l.ProductCode.Trim()}";
                if (!string.IsNullOrWhiteSpace(l.Ean)) return $"E:{l.Ean.Trim()}";
                var norm = Backup.Web.Api.Server.Services.Documents.Parsing.ProductTextNormalizer.Normalize(l.Product ?? string.Empty);
                return $"N:{norm}";
            }

            var previousInvoiceIds = storage.SelectAllDocuments()
                .Where(d => d.Id != invoiceId
                            && d.DateAdded < invoice.DateAdded
                            && !string.IsNullOrWhiteSpace(d.Supplier)
                            && d.Supplier == invoice.Supplier
                            && (EF.Functions.Like(d.TypeDocument, "%facture%")
                                || EF.Functions.Like(d.TypeDocument, "%invoice%")))
                .OrderByDescending(d => d.DateAdded)
                .Take(50) // limit scan like price-diff
                .Select(d => d.Id)
                .ToList();

            // Build reference price map: most recent unit price per key (same logic as price-diff)
            var refPriceMap = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            var currentKeys = new HashSet<string>(invLines.Select(Key), StringComparer.OrdinalIgnoreCase);
            
            if (previousInvoiceIds.Count > 0)
            {
                foreach (var pid in previousInvoiceIds)
                {
                    var lines = storage.SelectLinesByDocumentId(pid).ToList();
                    foreach (var l in lines)
                    {
                        var k = Key(l);
                        if (!currentKeys.Contains(k)) continue;
                        if (refPriceMap.ContainsKey(k)) continue; // we already have most recent
                        if (l.UnitPrice > 0) refPriceMap[k] = l.UnitPrice;
                    }
                    if (refPriceMap.Count == currentKeys.Count) break;
                }
                System.Diagnostics.Debug.WriteLine($"[Compare] Found {previousInvoiceIds.Count} previous invoices, built price map with {refPriceMap.Count} entries");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[Compare] No previous invoices found for supplier '{invoice.Supplier}' - previous prices will be 0");
            }
            
            // Debug: check if prices are loaded
            System.Diagnostics.Debug.WriteLine($"[Compare] Invoice lines loaded: {invLines.Count}");
            if (invLines.Any())
            {
                var linesWithUnitPrice = invLines.Where(l => l.UnitPrice > 0).ToList();
                var linesWithTotalValue = invLines.Where(l => l.TotalValue > 0).ToList();
                System.Diagnostics.Debug.WriteLine($"[Compare] Invoice lines with UnitPrice > 0: {linesWithUnitPrice.Count}");
                System.Diagnostics.Debug.WriteLine($"[Compare] Invoice lines with TotalValue > 0: {linesWithTotalValue.Count}");
                if (linesWithUnitPrice.Any())
                {
                    var sample = linesWithUnitPrice.First();
                    System.Diagnostics.Debug.WriteLine($"[Compare] Sample - UnitPrice: {sample.UnitPrice}, TotalValue: {sample.TotalValue}, Product: {sample.Product}, ProductCode: {sample.ProductCode}");
                }
            }

            if (invLines.Count > 0 && delLines.Count > 0)
            {
                return CompareUsingLines(invoiceId, deliveryId, invLines, delLines, refPriceMap);
            }

            // Fallback to heuristic text parsing
            var invoiceMap = ParseLinesToMap(invoice.ContentText);
            var deliveryMap = ParseLinesToMap(delivery.ContentText);

            var result = new ComparisonResult { InvoiceId = invoiceId, DeliveryId = deliveryId };

            foreach (var kv in invoiceMap)
            {
                var product = kv.Key;
                var invQty = kv.Value;
                var delQty = deliveryMap.TryGetValue(product, out var q) ? q : 0m;
                result.Lines.Add(new ComparisonLine { Product = product, InvoiceQty = invQty, DeliveryQty = delQty });
            }

            foreach (var kv in deliveryMap)
            {
                if (!invoiceMap.ContainsKey(kv.Key))
                {
                    result.Lines.Add(new ComparisonLine { Product = kv.Key, InvoiceQty = 0m, DeliveryQty = kv.Value });
                }
            }

            return result;
        }

        private static ComparisonResult CompareUsingLines(int invoiceId, int deliveryId, IList<DocumentLine> invLines, IList<DocumentLine> delLines, Dictionary<string, decimal> refPriceMap)
        {
            var result = new ComparisonResult { InvoiceId = invoiceId, DeliveryId = deliveryId };

            // Build maps keyed by a stable product key (ProductCode > extracted code > normalized name)
            var invMapByExtractKey = invLines
                .GroupBy(l => ExtractKey(l), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity), StringComparer.OrdinalIgnoreCase);

            var delMap = delLines
                .GroupBy(l => ExtractKey(l), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity), StringComparer.OrdinalIgnoreCase);

            // Build price maps using Key function (same as price-diff endpoint)
            string Key(DocumentLine l)
            {
                if (!string.IsNullOrWhiteSpace(l.ProductCode)) return $"C:{l.ProductCode.Trim()}";
                if (!string.IsNullOrWhiteSpace(l.Ean)) return $"E:{l.Ean.Trim()}";
                var norm = Backup.Web.Api.Server.Services.Documents.Parsing.ProductTextNormalizer.Normalize(l.Product ?? string.Empty);
                return $"N:{norm}";
            }
            
            // Build current price map using Key function (same format as price-diff)
            var currentInvPriceMap = invLines
                .Where(l => l.UnitPrice > 0)
                .GroupBy(l => Key(l), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Average(x => x.UnitPrice), StringComparer.OrdinalIgnoreCase);
            
            // Use the reference price map passed from CompareAsync (already built using Key function)
            var previousInvPriceMap = refPriceMap;
            
            System.Diagnostics.Debug.WriteLine($"[Compare] Current invoice price map has {currentInvPriceMap.Count} entries");
            System.Diagnostics.Debug.WriteLine($"[Compare] Previous invoice price map has {previousInvPriceMap.Count} entries");

            // For label rendering: remember representative names
            var invName = invLines
                .GroupBy(l => ExtractKey(l), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => FirstNonEmpty(g.Select(x => x.Product)) ?? g.Key, StringComparer.OrdinalIgnoreCase);

            var delName = delLines
                .GroupBy(l => ExtractKey(l), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => FirstNonEmpty(g.Select(x => x.Product)) ?? g.Key, StringComparer.OrdinalIgnoreCase);

            var allKeys = new HashSet<string>(invMapByExtractKey.Keys, StringComparer.OrdinalIgnoreCase);
            foreach (var k in delMap.Keys) allKeys.Add(k);
            
            // Debug: log all price map keys
            System.Diagnostics.Debug.WriteLine($"[Compare] Current invoice price map has {currentInvPriceMap.Count} keys: {string.Join(", ", currentInvPriceMap.Keys.Take(10))}");
            System.Diagnostics.Debug.WriteLine($"[Compare] Previous invoice price map has {previousInvPriceMap.Count} keys: {string.Join(", ", previousInvPriceMap.Keys.Take(10))}");
            System.Diagnostics.Debug.WriteLine($"[Compare] All product keys: {string.Join(", ", allKeys.Take(10))}");

            foreach (var key in allKeys)
            {
                var invQty = invMapByExtractKey.TryGetValue(key, out var iq) ? iq : 0m;
                var delQty = delMap.TryGetValue(key, out var dq) ? dq : 0m;
                
                // Convert ExtractKey to Key format for price lookup
                var sampleLine = invLines.FirstOrDefault(l => ExtractKey(l).Equals(key, StringComparison.OrdinalIgnoreCase));
                var priceKey = sampleLine != null ? Key(sampleLine) : null;
                
                // Get current price from current invoice
                var currentPrice = priceKey != null && currentInvPriceMap.TryGetValue(priceKey, out var cp) ? cp : 0m;
                
                // Get previous price from previous invoice - only if refPriceMap has entries (previous invoices exist)
                // If no previous invoices, previousPrice should be 0, not the current price
                var previousPrice = 0m;
                if (priceKey != null && previousInvPriceMap.Count > 0)
                {
                    previousInvPriceMap.TryGetValue(priceKey, out previousPrice);
                }
                
                // Debug: log prices for troubleshooting
                System.Diagnostics.Debug.WriteLine($"[Compare] ExtractKey: {key}, PriceKey: {priceKey}, CurrentPrice: {currentPrice}, PreviousPrice: {previousPrice} (refPriceMap has {previousInvPriceMap.Count} entries)");
                
                var rawLabel = invName.TryGetValue(key, out var ln) ? ln : (delName.TryGetValue(key, out var dn) ? dn : key);
                var label = CleanLabel(rawLabel);
                if (string.IsNullOrWhiteSpace(label)) continue; // skip noise/palette lines
                result.Lines.Add(new ComparisonLine
                {
                    Product = label,
                    InvoiceQty = invQty,
                    DeliveryQty = delQty,
                    CurrentInvoiceUnitPrice = currentPrice,
                    PreviousInvoiceUnitPrice = previousPrice
                });
            }

            return result;
        }

        public async Task<InvoicePriceComparisonResult> CompareInvoicesAsync(int invoice1Id, int invoice2Id, CancellationToken ct)
        {
            var invoice1 = await storage.SelectDocumentByIdAsync(invoice1Id) ?? throw new InvalidOperationException("Invoice 1 not found");
            var invoice2 = await storage.SelectDocumentByIdAsync(invoice2Id) ?? throw new InvalidOperationException("Invoice 2 not found");

            var inv1Lines = storage.SelectLinesByDocumentId(invoice1Id).ToList();
            var inv2Lines = storage.SelectLinesByDocumentId(invoice2Id).ToList();

            // Use same Key function as price comparison
            string Key(DocumentLine l)
            {
                if (!string.IsNullOrWhiteSpace(l.ProductCode)) return $"C:{l.ProductCode.Trim()}";
                if (!string.IsNullOrWhiteSpace(l.Ean)) return $"E:{l.Ean.Trim()}";
                var norm = Backup.Web.Api.Server.Services.Documents.Parsing.ProductTextNormalizer.Normalize(l.Product ?? string.Empty);
                return $"N:{norm}";
            }

            // Build price maps for both invoices
            var inv1PriceMap = inv1Lines
                .Where(l => l.UnitPrice > 0)
                .GroupBy(l => Key(l), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Average(x => x.UnitPrice), StringComparer.OrdinalIgnoreCase);

            var inv2PriceMap = inv2Lines
                .Where(l => l.UnitPrice > 0)
                .GroupBy(l => Key(l), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Average(x => x.UnitPrice), StringComparer.OrdinalIgnoreCase);

            // Get all product keys from both invoices
            var allKeys = new HashSet<string>(inv1PriceMap.Keys, StringComparer.OrdinalIgnoreCase);
            foreach (var k in inv2PriceMap.Keys) allKeys.Add(k);

            // Build product name map for labels
            var inv1NameMap = inv1Lines
                .GroupBy(l => Key(l), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => FirstNonEmpty(g.Select(x => x.Product)) ?? g.Key, StringComparer.OrdinalIgnoreCase);

            var inv2NameMap = inv2Lines
                .GroupBy(l => Key(l), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => FirstNonEmpty(g.Select(x => x.Product)) ?? g.Key, StringComparer.OrdinalIgnoreCase);

            var result = new InvoicePriceComparisonResult 
            { 
                Invoice1Id = invoice1Id, 
                Invoice2Id = invoice2Id 
            };

            foreach (var key in allKeys)
            {
                var price1 = inv1PriceMap.TryGetValue(key, out var p1) ? p1 : 0m;
                var price2 = inv2PriceMap.TryGetValue(key, out var p2) ? p2 : 0m;

                // Use name from invoice 1 if available, otherwise from invoice 2
                var productName = inv1NameMap.TryGetValue(key, out var n1) ? n1 : 
                                 (inv2NameMap.TryGetValue(key, out var n2) ? n2 : key);
                
                var label = CleanLabel(productName);
                if (string.IsNullOrWhiteSpace(label)) continue; // skip noise/palette lines

                result.Lines.Add(new InvoicePriceComparisonLine
                {
                    Product = label,
                    Invoice1UnitPrice = price1,
                    Invoice2UnitPrice = price2
                });
            }

            // Sort by product name
            result.Lines = result.Lines.OrderBy(l => l.Product).ToList();

            return result;
        }

        private static string CleanLabel(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return string.Empty;
            var t = Regex.Replace(s, @"\s+", " ").Trim();
            var lower = t.ToLowerInvariant();
            // drop palettes
            if (lower.Contains("euro-palet") || lower.Contains("euro palet") || lower.Contains("palet") || lower.Contains("palette"))
                return string.Empty;
            // truncate at common footer/header tokens
            var tokens = new[] { "bank", "rekening", "iban", "bic", "swift", "www.knauf.com", "algemene verkoopsvoorwaarden", "leveringsbevestiging", "pagina", "factuur", "invoice" };
            int cut = -1;
            foreach (var tok in tokens)
            {
                var idx = lower.IndexOf(tok);
                if (idx >= 0) cut = cut == -1 ? idx : System.Math.Min(cut, idx);
            }
            if (cut > 0) t = t.Substring(0, cut).Trim();
            // strip price-like fragments
            t = Regex.Replace(t, @"\b\d{1,3}(?:[.,]\d{2})\s*/\s*1\s*[A-Za-z\.]{2,6}\b", "", RegexOptions.IgnoreCase);
            t = Regex.Replace(t, @"\b\d{1,3}(?:[.,]\d{1,2})\s*-%\b", "", RegexOptions.IgnoreCase);
            // bound length
            if (t.Length > 200) t = t.Substring(0, 200);
            return t.Trim();
        }

        private static string ExtractKey(DocumentLine l)
        {
			// 1) Prefer explicit ProductCode if present (article ref, SKU)
			if (!string.IsNullOrWhiteSpace(l.ProductCode)) return l.ProductCode.Trim();

			// 2) Then prefer EAN if present
			if (!string.IsNullOrWhiteSpace(l.Ean)) return l.Ean.Trim();

			// 3) Then try to extract a stable code from description (EAN or article numbers: long digits or alphanumerics that CONTAIN at least one digit)
            var text = l.Product ?? string.Empty;
			var m = Regex.Match(text, @"\b([0-9]{6,}|[A-Z0-9\-]*\d[A-Z0-9\-]{3,})\b", RegexOptions.IgnoreCase);
            if (m.Success) return m.Groups[1].Value.Trim();

			// 4) Last resort: normalized product name
			return NormalizeName(text);
        }

        private static string NormalizeName(string s)
        {
            // Use robust normalizer to align product names across suppliers/languages
            var t = Backup.Web.Api.Server.Services.Documents.Parsing.ProductTextNormalizer.Normalize(s ?? string.Empty);
            if (t.Length > 120) t = t.Substring(0, 120);
            return t;
        }

        private static string? FirstNonEmpty(IEnumerable<string?> items)
            => items.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

        private static Dictionary<string, decimal> ParseLinesToMap(string content)
        {
            var map = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(content)) return map;

            // Detect language heuristically (fr/nl/en) to adapt labels
            var detected = DetectLanguage(content);
            // Labels per language
            var productLabels = detected switch
            {
                "nl" => new[] { "Product", "Artikel" },
                "en" => new[] { "Product" },
                _ => new[] { "Produit", "Product", "Artikel" } // default: include all
            };
            var quantityLabels = detected switch
            {
                "nl" => new[] { "Aantal" },
                "en" => new[] { "Quantity", "Qty" },
                _ => new[] { "Quantité", "Qte", "Quantity", "Qty", "Aantal" }
            };

            // Normaliser (supprimer entêtes fréquents)
            var text = content.Replace("\r", "");

            // 0) Table-style rows across languages: "<ProductLbl> <name> ... <QuantityLbl>: <qty>"
            var productAlt = string.Join("|", productLabels.Select(Regex.Escape));
            var qtyAlt = string.Join("|", quantityLabels.Select(Regex.Escape));
            var tablePattern = $@"(?is)\b({productAlt})\s+(?<prod>.+?)\b.*?\b({qtyAlt})\s*[: ]\s*(?<qty>\d+(?:[\.,]\d+)?)\b";
            var tablePairs = Regex.Matches(text, tablePattern);
            if (tablePairs.Count > 0)
            {
                foreach (Match m in tablePairs)
                {
                    var prod = CleanupProduct(m.Groups["prod"].Value);
                    if (string.IsNullOrWhiteSpace(prod)) continue;
                    var qtyStr = m.Groups["qty"].Value.Replace(',', '.');
                    if (!decimal.TryParse(qtyStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var qty)) continue;
                    if (map.TryGetValue(prod, out var accT)) map[prod] = accT + qty; else map[prod] = qty;
                }
                return map;
            }

            // Segmenter par blocs "<ProductLbl> ..." jusqu'au prochain label produit
            var produitMatches = Regex.Matches(text, $@"(?i)\b({productAlt})\b");
            if (produitMatches.Count > 0)
            {
                for (int i = 0; i < produitMatches.Count; i++)
                {
                    int start = produitMatches[i].Index;
                    int end = (i + 1 < produitMatches.Count) ? produitMatches[i + 1].Index : text.Length;
                    if (end <= start) continue;
                    var segment = text.Substring(start, end - start);

                    // Extraire "<ProductLbl> <nom> - <qte>" (style simple)
                    var simplePattern = $@"(?is)\b({productAlt})\s+(?<prod>.+?)\s*[-:]\s*(?<qty>\d+(?:[\.,]\d+)?)\b";
                    var m = Regex.Match(segment, simplePattern);
                    if (!m.Success) continue;
                    var prod = CleanupProduct(m.Groups["prod"].Value);
                    if (string.IsNullOrWhiteSpace(prod)) continue;
                    var qtyStr = m.Groups["qty"].Value.Replace(',', '.');
                    if (!decimal.TryParse(qtyStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var qty)) continue;
                    if (map.TryGetValue(prod, out var acc)) map[prod] = acc + qty; else map[prod] = qty;
                }
                return map;
            }

            // Fallback simple si aucun mot "Produit" n'est détecté (dernière chance)
            foreach (var raw in text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var m = Regex.Match(raw, @"(?<prod>.+?)\s*[-:]\s*(?<qty>\d+(?:[\.,]\d+)?)\s*$");
                if (!m.Success) continue;
                var prod = CleanupProduct(m.Groups["prod"].Value);
                var qtyStr = m.Groups["qty"].Value.Replace(',', '.');
                if (!decimal.TryParse(qtyStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var qty)) continue;
                if (map.TryGetValue(prod, out var acc)) map[prod] = acc + qty; else map[prod] = qty;
            }

            return map;
        }

        private static string DetectLanguage(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "fr";
            var t = text;
            // quick keyword heuristics
            if (Regex.IsMatch(t, @"\b(Factuur|Leveringsbon|Aantal|Artikel)\b", RegexOptions.IgnoreCase)) return "nl";
            if (Regex.IsMatch(t, @"\b(Invoice|Delivery\s*Note|Quantity|Qty|Product)\b", RegexOptions.IgnoreCase)) return "en";
            if (Regex.IsMatch(t, @"\b(Facture|Bon\s*de\s*livraison|Quantité|Produit)\b", RegexOptions.IgnoreCase)) return "fr";
            // default to fr but patterns above are language-agnostic enough
            return "fr";
        }

        private static string CleanupProduct(string s)
        {
            // Remove excessive spaces and separators
            var t = Regex.Replace(s, @"\s+", " ").Trim().Trim('-', ':');
            // Bound product name length
            if (t.Length > 200) t = t.Substring(0, 200);
            return t;
        }
    }
}


