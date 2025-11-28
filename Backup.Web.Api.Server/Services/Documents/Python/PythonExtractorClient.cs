using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using Backup.Web.Api.Server.Models;
using Microsoft.Extensions.Options;

namespace Backup.Web.Api.Server.Services.Documents.Python
{
    public class PythonExtractorClient : IPythonExtractorClient
    {
        private readonly HttpClient http;
        private readonly PythonExtractorOptions options;

		private record ExtractedLineDto(string raw, string normalized, int quantity, string product_code, string ean, string unit, double? unit_price, double? total_value);
		private record PythonMetaDto(string? doc_type, string? number, string? client, string? date, string? supplier);

        public PythonExtractorClient(HttpClient http, IOptions<PythonExtractorOptions> options)
        {
            this.http = http;
            this.options = options.Value ?? new PythonExtractorOptions();
        }

		public async Task<Backup.Web.Api.Server.Services.Documents.DocumentMetadata?> InspectMetadataAsync(string absolutePdfPath, CancellationToken ct)
		{
			if (!this.options.Enabled) return null;
			if (string.IsNullOrWhiteSpace(absolutePdfPath) || !File.Exists(absolutePdfPath)) return null;
			try
			{
				using var multipart = new MultipartFormDataContent();
				var fileBytes = await File.ReadAllBytesAsync(absolutePdfPath, ct);
				var fileContent = new ByteArrayContent(fileBytes);
				fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/pdf");
				var fileName = Path.GetFileName(absolutePdfPath);
				multipart.Add(fileContent, "file", fileName);

				using var resp = await this.http.PostAsync($"{this.options.Url.TrimEnd('/')}/inspect", multipart, ct);
				if (!resp.IsSuccessStatusCode) return null;

				var json = await resp.Content.ReadAsStringAsync(ct);
				System.Diagnostics.Debug.WriteLine($"[PythonExtractor] JSON brut reçu: {json}");
				
				var meta = JsonSerializer.Deserialize<PythonMetaDto>(json, new JsonSerializerOptions
				{
					ReadCommentHandling = JsonCommentHandling.Skip,
					AllowTrailingCommas = true,
					PropertyNameCaseInsensitive = true
				});
				if (meta == null) 
				{
					System.Diagnostics.Debug.WriteLine("[PythonExtractor] Désérialisation échouée - meta est NULL");
					return null;
				}
				
				System.Diagnostics.Debug.WriteLine($"[PythonExtractor] Désérialisation réussie - doc_type: '{meta.doc_type}', number: '{meta.number}', client: '{meta.client}', date: '{meta.date}', supplier: '{meta.supplier}'");

				var result = new Backup.Web.Api.Server.Services.Documents.DocumentMetadata();
				// Map type
				var type = (meta.doc_type ?? string.Empty).Trim().ToLowerInvariant();
				result.TypeDocument = type switch
				{
					"delivery" or "delivery_note" or "bl" => "BonLivraison",
					_ => "Facture"
				};
				// Numero
				if (!string.IsNullOrWhiteSpace(meta.number)) result.Numero = meta.number!.Trim();
				// Client - IMPORTANT: utiliser la valeur Python telle quelle
				if (!string.IsNullOrWhiteSpace(meta.client)) 
				{
					result.Client = meta.client!.Trim();
					System.Diagnostics.Debug.WriteLine($"[PythonExtractor] Client from Python: '{result.Client}'");
				}
				// Supplier
				if (!string.IsNullOrWhiteSpace(meta.supplier)) result.Supplier = meta.supplier!.Trim();
				// Date
				if (!string.IsNullOrWhiteSpace(meta.date))
				{
					if (DateTime.TryParse(meta.date, out var dt))
						result.DateDocument = dt.Date;
				}
				System.Diagnostics.Debug.WriteLine($"[PythonExtractor] Final result - Type: {result.TypeDocument}, Numero: {result.Numero}, Client: {result.Client}, Date: {result.DateDocument}, Supplier: {result.Supplier}");
				return result;
			}
			catch
			{
				return null;
			}
		}

        public async Task<List<DocumentLine>> TryExtractAsync(string absolutePdfPath, CancellationToken ct)
        {
            var output = new List<DocumentLine>();
            if (!this.options.Enabled) return output;
            if (string.IsNullOrWhiteSpace(absolutePdfPath) || !File.Exists(absolutePdfPath)) return output;

            try
            {
                using var multipart = new MultipartFormDataContent();
                var fileBytes = await File.ReadAllBytesAsync(absolutePdfPath, ct);
                var fileContent = new ByteArrayContent(fileBytes);
                fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/pdf");
                var fileName = Path.GetFileName(absolutePdfPath);
                multipart.Add(fileContent, "file", fileName);

                using var resp = await this.http.PostAsync($"{this.options.Url.TrimEnd('/')}/extract", multipart, ct);
                if (!resp.IsSuccessStatusCode) return output;

                var json = await resp.Content.ReadAsStringAsync(ct);
                var parsed = JsonSerializer.Deserialize<List<ExtractedLineDto>>(json, new JsonSerializerOptions
                {
                    ReadCommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true,
                    PropertyNameCaseInsensitive = true
                }) ?? new List<ExtractedLineDto>();

                int line = 0;
                foreach (var p in parsed)
                {
                    line++;
                    if (p == null) continue;
                    if (p.quantity <= 0) continue;
                    var raw = p.raw?.Trim() ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(raw)) continue;
                    
                    // Utiliser la description normalisée pour une comparaison cohérente
                    var label = string.IsNullOrWhiteSpace(p.normalized) ? raw : p.normalized.Trim();

                    // Sanitize EAN to 13-digit if present (fallback: try to find in raw)
                    string? ean = null;
                    if (!string.IsNullOrWhiteSpace(p.ean))
                    {
                        var m = System.Text.RegularExpressions.Regex.Match(p.ean, @"\b(\d{13})\b");
                        if (m.Success) ean = m.Groups[1].Value;
                    }
                    if (string.IsNullOrWhiteSpace(ean))
                    {
                        var m2 = System.Text.RegularExpressions.Regex.Match(raw, @"\b(\d{13})\b");
                        if (m2.Success) ean = m2.Groups[1].Value;
                    }

					// Normalize unit
                    var unit = (p.unit ?? "ST").Trim().ToUpperInvariant();
                    // Accept common units; map synonyms to ST
                    var allowedUnits = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "ST", "PAK", "PC", "PACK", "SET", "PCS" };
                    if (!allowedUnits.Contains(unit)) unit = "ST";
                    if (unit is "PC" or "PCS" or "PACK") unit = "ST";

                    // Hard validation: require numeric product code 4-8 digits for FF GROUP style; skip noisy rows
                    var productCode = (p.product_code ?? string.Empty).Trim();
                    if (!System.Text.RegularExpressions.Regex.IsMatch(productCode, @"^\d{4,8}$"))
                    {
                        // allow rows without product code only if label looks like a product (contains letters) and unit is valid
                        if (!System.Text.RegularExpressions.Regex.IsMatch(label, @"[A-Za-z]{3,}"))
                            continue;
                        productCode = string.Empty;
                    }

                    // Quantity sanity (avoid absurd captures from headers)
                    if (p.quantity > 1000) continue;

                    // Enforce DB max lengths
                    static string Truncate(string s, int max) => s.Length <= max ? s : s.Substring(0, max);
                    productCode = Truncate(productCode, 128);
                    raw = Truncate(raw, 2048);
                    label = Truncate(label, 255);
                    unit = Truncate(unit, 16);
                    if (!string.IsNullOrWhiteSpace(ean) && ean!.Length > 13) ean = ean.Substring(0, 13);

                    output.Add(new DocumentLine
                    {
                        LineNumber = line,
                        Product = label,
                        ProductCode = productCode,
                        Ean = ean,
                        Quantity = p.quantity,
                        Unit = unit,
						UnitPrice = (decimal)(p.unit_price ?? 0d),
						TotalValue = (decimal)(p.total_value ?? 0d),
                        RawLine = raw
                    });
                }
            }
            catch
            {
                // Fail silently and let deterministic flow continue
            }

            return output;
        }
    }
}