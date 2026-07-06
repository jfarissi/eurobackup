using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using Backup.Web.Api.Server.Models;
using Microsoft.Extensions.Options;

namespace Backup.Web.Api.Server.Services.Documents.Python
{
    public class PythonExtractorClient : IPythonExtractorClient
    {
        private readonly HttpClient http;
        private readonly PythonExtractorOptions options;
        private static readonly string[] HeaderLikeTokens = new[]
        {
            "tva", "vat", "btw", "nummer", "number", "numéro", "nr", "klant", "client", "customer"
        };

		// Ancien format (pour compatibilité)
		private record ExtractedLineDto(string raw, string normalized, int quantity, string product_code, string ean, string unit, double? unit_price, double? total_value);
		private record PythonMetaDto(string? doc_type, string? number, string? client, string? date, string? supplier);
		
		// Nouveau format /parse
		private record ParsedItemDto(
			string? sku,
			string? supplier_sku,
			string? ean,
			string? barcode_raw,
            string? barcode_normalized,
			string? description,
			double? qty,
			string? unit,
			double? unit_price,
			double? line_total
		);
		private record ParsedMetadataDto(
			string? type,
			string? doc_type,
			string? number,
			string? client,
			string? supplier,
			string? date,
			int? count,
			string? method,
            string? supplier_code,
            string? supplier_address,
            string? supplier_phone,
            string? supplier_email,
            string? supplier_contact,
            string? supplier_payment_terms
		);
		private record ParseResultDto(
			List<ParsedItemDto> items,
			ParsedMetadataDto metadata
		);

        private static string? CleanMetaValue(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            var s = value.Trim().TrimStart('-', ':', ';', ',', '.').Trim();
            return string.IsNullOrWhiteSpace(s) ? null : s;
        }

        private static bool LooksLikeHeaderLabel(string? value)
        {
            var s = CleanMetaValue(value);
            if (string.IsNullOrWhiteSpace(s)) return true;
            var lower = s.ToLowerInvariant();
            foreach (var token in HeaderLikeTokens)
            {
                if (lower.Contains(token)) return true;
            }
            return s.Length < 3;
        }

        private static bool LooksLikeDocumentNumber(string? value)
        {
            var s = CleanMetaValue(value);
            if (string.IsNullOrWhiteSpace(s)) return false;
            if (LooksLikeHeaderLabel(s)) return false;
            return s.Any(char.IsDigit);
        }

        public PythonExtractorClient(HttpClient http, IOptions<PythonExtractorOptions> options)
        {
            this.http = http;
            this.options = options.Value ?? new PythonExtractorOptions();
        }

        private static string GetParsePath(PythonExtractorOptions o)
        {
            var engine = (o.ParseEngine ?? "auto").Trim().ToLowerInvariant();
            return engine switch
            {
                "ollama" => "/parse/ollama",
                "factory" => "/parse/factory",
                "classifier" => "/parse/classifier",
                _ => "/parse"
            };
        }

        /// <summary>Query string pour parse auto / ollama / factory.</summary>
        private static string BuildParseQueryString(PythonExtractorOptions o)
        {
            var engine = (o.ParseEngine ?? "auto").Trim().ToLowerInvariant();
            var useAi = o.UseAiForPythonParse ? "true" : "false";
            var provider = string.IsNullOrWhiteSpace(o.DefaultAiProvider) ? "ollama" : o.DefaultAiProvider.Trim();
            var parts = new List<string>();
            if (engine == "auto")
            {
                parts.Add($"use_ai={Uri.EscapeDataString(useAi)}");
                parts.Add($"ai_provider={Uri.EscapeDataString(provider)}");
            }
            var da = o.DocumentAi;
            if (da != null)
            {
                if ((engine == "auto" || engine == "ollama") && !string.IsNullOrWhiteSpace(da.OllamaHost))
                    parts.Add($"ollama_host={Uri.EscapeDataString(da.OllamaHost.Trim().TrimEnd('/'))}");
                var prof = da.ActiveProfile?.Trim();
                if ((engine == "auto" || engine == "ollama") && !string.IsNullOrEmpty(prof))
                    parts.Add($"ollama_profile={Uri.EscapeDataString(prof)}");
                if ((engine == "auto" || engine == "ollama") && !string.IsNullOrEmpty(prof) && da.Profiles != null && da.Profiles.TryGetValue(prof, out var p) && !string.IsNullOrWhiteSpace(p?.Model))
                    parts.Add($"ollama_model={Uri.EscapeDataString(p.Model.Trim())}");
            }
            return parts.Count == 0 ? string.Empty : ("?" + string.Join("&", parts));
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

				var url = $"{this.options.Url.TrimEnd('/')}{GetParsePath(this.options)}{BuildParseQueryString(this.options)}";
				using var resp = await this.http.PostAsync(url, multipart, ct);
				if (!resp.IsSuccessStatusCode) return null;

				var json = await resp.Content.ReadAsStringAsync(ct);
				System.Diagnostics.Debug.WriteLine($"[PythonExtractor] JSON brut reçu: {json}");
				
				var parseResult = JsonSerializer.Deserialize<ParseResultDto>(json, new JsonSerializerOptions
				{
					ReadCommentHandling = JsonCommentHandling.Skip,
					AllowTrailingCommas = true,
					PropertyNameCaseInsensitive = true
				});
				
				if (parseResult?.metadata == null) 
				{
					System.Diagnostics.Debug.WriteLine("[PythonExtractor] Désérialisation échouée - metadata est NULL");
					return null;
				}
				
				var meta = parseResult.metadata;
				var parsedDocType = (meta.type ?? meta.doc_type ?? string.Empty).Trim();
				System.Diagnostics.Debug.WriteLine($"[PythonExtractor] Désérialisation réussie - type: '{parsedDocType}', number: '{meta.number}', client: '{meta.client}', date: '{meta.date}', supplier: '{meta.supplier}'");

				var result = new Backup.Web.Api.Server.Services.Documents.DocumentMetadata();
				// Map type
				var type = (meta.type ?? meta.doc_type ?? string.Empty).Trim().ToLowerInvariant();
				result.TypeDocument = type switch
				{
					"delivery" or "delivery_note" or "delivery note" or "bl" or "bon de livraison" or "verzendnota" or "leveringsbon" or "leveringsbevestiging" or "delivery confirmation" => "BonLivraison",
					"invoice" or "facture" or "factuur" => "Facture",
					_ => "Facture" // Par défaut
				};
				// Numero (filtrage anti faux positifs d'entête)
				var cleanedNumber = CleanMetaValue(meta.number);
				if (LooksLikeDocumentNumber(cleanedNumber))
					result.Numero = cleanedNumber!;
				// Client (filtrage anti faux positifs d'entête)
				var cleanedClient = CleanMetaValue(meta.client);
				if (!LooksLikeHeaderLabel(cleanedClient))
				{
					result.Client = cleanedClient!;
					System.Diagnostics.Debug.WriteLine($"[PythonExtractor] Client from Python: '{result.Client}'");
				}
				// Supplier
				var cleanedSupplier = CleanMetaValue(meta.supplier);
				if (!string.IsNullOrWhiteSpace(cleanedSupplier) && !cleanedSupplier.Equals("unknown", StringComparison.OrdinalIgnoreCase))
					result.Supplier = cleanedSupplier;
                // Extra Supplier Info
                if (!string.IsNullOrWhiteSpace(meta.supplier_code)) result.SupplierCode = meta.supplier_code!.Trim();
                if (!string.IsNullOrWhiteSpace(meta.supplier_address)) result.SupplierAddress = meta.supplier_address!.Trim();
                if (!string.IsNullOrWhiteSpace(meta.supplier_phone)) result.SupplierPhone = meta.supplier_phone!.Trim();
                if (!string.IsNullOrWhiteSpace(meta.supplier_email)) result.SupplierEmail = meta.supplier_email!.Trim();
                if (!string.IsNullOrWhiteSpace(meta.supplier_contact)) result.SupplierContact = meta.supplier_contact!.Trim();
                if (!string.IsNullOrWhiteSpace(meta.supplier_payment_terms)) result.SupplierPaymentTerms = meta.supplier_payment_terms!.Trim();
				// Date
				if (!string.IsNullOrWhiteSpace(meta.date))
				{
					// Essayer plusieurs formats de date
					if (DateTime.TryParse(meta.date, out var dt))
						result.DateDocument = dt.Date;
					else if (System.Text.RegularExpressions.Regex.IsMatch(meta.date, @"^\d{2}\.\d{2}\.\d{4}$"))
					{
						// Format DD.MM.YYYY (STG)
						if (DateTime.TryParseExact(meta.date, "dd.MM.yyyy", null, System.Globalization.DateTimeStyles.None, out var dt2))
							result.DateDocument = dt2.Date;
					}
				}
				System.Diagnostics.Debug.WriteLine($"[PythonExtractor] Final result - Type: {result.TypeDocument}, Numero: {result.Numero}, Client: {result.Client}, Date: {result.DateDocument}, Supplier: {result.Supplier}");
				return result;
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"[PythonExtractor] Erreur lors de l'inspection: {ex.Message}");
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

                var url = $"{this.options.Url.TrimEnd('/')}{GetParsePath(this.options)}{BuildParseQueryString(this.options)}";
                using var resp = await this.http.PostAsync(url, multipart, ct);
                if (!resp.IsSuccessStatusCode) return output;

                var json = await resp.Content.ReadAsStringAsync(ct);
                var parseResult = JsonSerializer.Deserialize<ParseResultDto>(json, new JsonSerializerOptions
                {
                    ReadCommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true,
                    PropertyNameCaseInsensitive = true
                });

                if (parseResult?.items == null) return output;

                int line = 0;
                foreach (var item in parseResult.items)
                {
                    line++;
                    if (item == null) continue;
                    
                    // Quantité
                    var qty = (double)(item.qty ?? 0d);
                    if (qty <= 0) continue;
                    if (qty > 1000) continue; // Sanity check
                    
                    // Description
                    var description = (item.description ?? string.Empty).Trim();
                    if (string.IsNullOrWhiteSpace(description)) continue;
                    if (description.Length < 3) continue;
                    
                    // SKU / ProductCode
                    var productCode = (item.sku ?? string.Empty).Trim();
                    // Pour STG, utiliser supplier_sku si sku n'est pas disponible
                    if (string.IsNullOrWhiteSpace(productCode) && !string.IsNullOrWhiteSpace(item.supplier_sku))
                    {
                        productCode = item.supplier_sku.Trim();
                    }
                    
                    // EAN
                    string? ean = null;
                    if (!string.IsNullOrWhiteSpace(item.ean))
                    {
                        var m = System.Text.RegularExpressions.Regex.Match(item.ean, @"\b(\d{8,14})\b");
                        if (m.Success) ean = m.Groups[1].Value;
                        // Normaliser à 13 chiffres si possible
                        if (ean != null && ean.Length == 13) { } // OK
                        else if (ean != null && ean.Length > 13) ean = ean.Substring(0, 13);
                    }
                    // Fallback barcode token translation (ex: Pardaen custom barcode text) when no numeric EAN.
                    if (string.IsNullOrWhiteSpace(ean) && !string.IsNullOrWhiteSpace(item.barcode_normalized))
                    {
                        ean = item.barcode_normalized.Trim();
                    }
                    else if (string.IsNullOrWhiteSpace(ean) && !string.IsNullOrWhiteSpace(item.barcode_raw))
                    {
                        ean = item.barcode_raw.Trim();
                    }

                    // Unité
                    var unit = (item.unit ?? "ST").Trim().ToUpperInvariant();
                    var allowedUnits = new HashSet<string>(StringComparer.OrdinalIgnoreCase) 
                    { 
                        "ST", "PAK", "PC", "PAC", "KG", "PACK", "SET", "PCS" 
                    };
                    if (!allowedUnits.Contains(unit)) unit = "ST";

                    // Prix
                    var unitPrice = (decimal)(item.unit_price ?? 0d);
                    var totalValue = (decimal)(item.line_total ?? 0d);
                    // Regelbedrag prioritaire ; repli qté×PU seulement si absent
                    if (item.line_total == null && unitPrice > 0 && qty > 0)
                    {
                        totalValue = unitPrice * (decimal)qty;
                    }
                    else if (item.line_total != null)
                    {
                        totalValue = (decimal)item.line_total;
                    }

                    // Enforce DB max lengths
                    static string Truncate(string s, int max) => s.Length <= max ? s : s.Substring(0, max);
                    productCode = Truncate(productCode, 128);
                    description = Truncate(description, 1024);
                    unit = Truncate(unit, 16);
                    if (!string.IsNullOrWhiteSpace(ean) && ean!.Length > 13) ean = ean.Substring(0, 13);

                    // RawLine: construire une représentation de la ligne
                    var rawLine = $"{productCode} {description} {qty} {unit}";
                    if (unitPrice > 0) rawLine += $" {unitPrice}";
                    if (totalValue > 0) rawLine += $" {totalValue}";
                    rawLine = Truncate(rawLine, 2048);

                    output.Add(new DocumentLine
                    {
                        LineNumber = line,
                        Product = description,
                        ProductCode = productCode,
                        Ean = ean,
                        Quantity = (decimal)qty,
                        Unit = unit,
                        UnitPrice = unitPrice,
                        TotalValue = totalValue,
                        RawLine = rawLine
                    });
                }
            }
            catch (Exception ex)
            {
                // Log l'erreur pour debug mais fail silently
                System.Diagnostics.Debug.WriteLine($"[PythonExtractor] Erreur lors de l'extraction: {ex.Message}");
            }

            return output;
        }
    }
}