using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Backup.Web.Api.Server.Services.Documents.Ollama
{
    public sealed class OllamaParsingService : IOllamaParsingService
    {
        private readonly HttpClient _httpClient;
        private readonly OllamaOptions _options;
        private readonly ILogger<OllamaParsingService> _logger;

        public OllamaParsingService(HttpClient httpClient, IOptions<OllamaOptions> options, ILogger<OllamaParsingService> logger)
        {
            _httpClient = httpClient;
            _options = options.Value;
            _logger = logger;
        }

        public async Task<List<DocumentLine>> TryParseAsync(string fullText, CancellationToken ct)
        {
            var results = new List<DocumentLine>();
            
            if (!_options.Enabled) 
                return results;
                
            if (string.IsNullOrWhiteSpace(fullText)) 
                return results;

            try
            {
                // Timeout global plus large pour permettre plusieurs requêtes séquentielles (chunks / codes)
                var totalTimeoutSeconds = Math.Max(30, _options.TimeoutSeconds);
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(totalTimeoutSeconds));

                var endpoint = $"{_options.Host.TrimEnd('/')}/api/generate";
                var condensed = CondenseForAi(fullText);
                var prompt = BuildImprovedPrompt(condensed);
                
                var request = new GenerateRequest
                {
                    Model = _options.Model,
                    Prompt = prompt,
                    Stream = false,
                    Format = "json",
                    Options = new GenerateOptions 
                    { 
                        Temperature = 0.0, // stricte déterminisme
                        MaxContext = _options.MaxContext 
                    }
                };

                var json = JsonSerializer.Serialize(request);
                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                // Timeout par requête
                var perRequestSeconds = Math.Min(20, Math.Max(8, _options.TimeoutSeconds));
                using var reqCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token);
                reqCts.CancelAfter(TimeSpan.FromSeconds(perRequestSeconds));

                var response = await _httpClient.PostAsync(endpoint, content, reqCts.Token);
                response.EnsureSuccessStatusCode();
                
                var responseString = await response.Content.ReadAsStringAsync(reqCts.Token);
                var parsedResponse = JsonSerializer.Deserialize<GenerateResponse>(responseString);

                if (parsedResponse == null || string.IsNullOrWhiteSpace(parsedResponse.Response)) 
                    return results;

                var aiLines = ParseAIResponse(parsedResponse.Response);
                results = ValidateAILines(aiLines);

                // Si l'IA ne renvoie que peu de lignes, essayer en chunks (par pages/blocs)
                if (results.Count < 5)
                {
                    var aggregated = new List<DocumentLine>();
                    int processed = 0;
                    foreach (var chunk in SplitIntoChunks(condensed).Take(10)) // limite sécurité
                    {
                        if (cts.IsCancellationRequested) break;
                        if (results.Count + aggregated.Count >= 24) break; // assez de rappel
                        processed++;
                        if (string.IsNullOrWhiteSpace(chunk)) continue;
                        var p = BuildImprovedPrompt(chunk);
                        var req = new GenerateRequest
                        {
                            Model = _options.Model,
                            Prompt = p,
                            Stream = false,
                            Format = "json",
                            Options = new GenerateOptions
                            {
                                Temperature = 0.0,
                                MaxContext = _options.MaxContext
                            }
                        };
                        var j = JsonSerializer.Serialize(req);
                        using var cc = new StringContent(j, Encoding.UTF8, "application/json");
                        try
                        {
                            using var chunkReqCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token);
                            chunkReqCts.CancelAfter(TimeSpan.FromSeconds(perRequestSeconds));
                            var r = await _httpClient.PostAsync(endpoint, cc, chunkReqCts.Token);
                            if (!r.IsSuccessStatusCode) continue;
                            var rs = await r.Content.ReadAsStringAsync(chunkReqCts.Token);
                            var pr = JsonSerializer.Deserialize<GenerateResponse>(rs);
                            if (pr == null || string.IsNullOrWhiteSpace(pr.Response)) continue;
                            var chunkLines = ValidateAILines(ParseAIResponse(pr.Response));
                            if (chunkLines.Count > 0) aggregated.AddRange(chunkLines);
                        }
                        catch (OperationCanceledException)
                        {
                            continue;
                        }
                    }

                    if (aggregated.Count > results.Count)
                    {
                        // Dédupliquer par (ProductCode|EAN|Product normalisé)
                        var dedup = aggregated
                            .GroupBy(a => $"{(a.ProductCode ?? "").Trim().ToLowerInvariant()}|{(a.Ean ?? "").Trim()}|{(a.Product ?? "").Trim().ToLowerInvariant()}")
                            .Select(g => g.First())
                            .ToList();
                        results = dedup;
                    }
                }
                
                // Fallback 2: si toujours trop peu, découper par blocs de codes (code+EAN+qty) et interroger par bloc
                if (results.Count < 10)
                {
                    var aggregated = new List<DocumentLine>();
                    int processed = 0;
                    foreach (var codeChunk in SplitIntoCodeBlocks(condensed).Take(40)) // limite sécurité
                    {
                        if (cts.IsCancellationRequested) break;
                        if (results.Count + aggregated.Count >= 28) break; // suffisant pour un BL
                        processed++;
                        if (string.IsNullOrWhiteSpace(codeChunk)) continue;
                        var p = BuildImprovedPrompt(codeChunk);
                        var req = new GenerateRequest
                        {
                            Model = _options.Model,
                            Prompt = p,
                            Stream = false,
                            Format = "json",
                            Options = new GenerateOptions
                            {
                                Temperature = 0.0,
                                MaxContext = _options.MaxContext
                            }
                        };
                        var j = JsonSerializer.Serialize(req);
                        using var cc = new StringContent(j, Encoding.UTF8, "application/json");
                        try
                        {
                            using var codeReqCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token);
                            codeReqCts.CancelAfter(TimeSpan.FromSeconds(perRequestSeconds));
                            var r = await _httpClient.PostAsync(endpoint, cc, codeReqCts.Token);
                            if (!r.IsSuccessStatusCode) continue;
                            var rs = await r.Content.ReadAsStringAsync(codeReqCts.Token);
                            var pr = JsonSerializer.Deserialize<GenerateResponse>(rs);
                            if (pr == null || string.IsNullOrWhiteSpace(pr.Response)) continue;
                            var chunkLines = ValidateAILines(ParseAIResponse(pr.Response));
                            if (chunkLines.Count > 0) aggregated.AddRange(chunkLines);
                        }
                        catch (OperationCanceledException)
                        {
                            continue;
                        }
                    }

                    if (aggregated.Count > results.Count)
                    {
                        var dedup = aggregated
                            .GroupBy(a => $"{(a.ProductCode ?? "").Trim().ToLowerInvariant()}|{(a.Ean ?? "").Trim()}|{(a.Product ?? "").Trim().ToLowerInvariant()}")
                            .Select(g => g.First())
                            .ToList();
                        results = dedup;
                    }
                }

                // Fallback 3: extraction rule-based si IA trop faible
                if (results.Count < 10)
                {
                    var ruleBased = RuleBasedExtract(condensed);
                    if (ruleBased.Count > results.Count)
                    {
                        var dedup = ruleBased
                            .GroupBy(a => $"{(a.ProductCode ?? "").Trim().ToLowerInvariant()}|{(a.Ean ?? "").Trim()}|{(a.Product ?? "").Trim().ToLowerInvariant()}")
                            .Select(g => g.First())
                            .ToList();
                        results = dedup;
                    }
                }

                _logger.LogInformation("IA parsing réussi: {Count} produits", results.Count);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Échec parsing IA (Ollama), continuation sans lignes IA");
            }

            return results;
        }

        private string BuildImprovedPrompt(string text)
        {
            var maxChars = 60000; // Réduit pour stabilité locale
            if (text.Length > maxChars) 
                text = text.Substring(0, maxChars) + "... [TRONQUÉ]";

            var instruction = @"Tu es un extracteur strict de produits pour un Bon de Livraison Knauf.
Retourne UNIQUEMENT un tableau JSON (pas de texte hors JSON), chaque élément avec les clés EXACTES:
- LineNumber (number), Product (string), ProductCode (string), EAN (string), Quantity (number), Unit (string), RawLine (string)
Règles:
- Units autorisées: ST ou PAK uniquement; ignorer KG/M/L/mm/kg.
- Quantity > 0, entière; ne pas deviner ni sommer.
- ProductCode: prends le code article imprimé (4–6+ chiffres) SANS le numéro de ligne; sur une ligne comme ""110 6260"", renvoie ""6260"".
- EAN: 13 chiffres si présent sinon chaîne vide.
- RawLine: la ligne source contenant la quantité (sans prix).
- Ignore toute ligne avec ""/ 1 ST"" (prix unitaire) et les %.
Si incertain, omets l’élément.
Exemple:
[
  {""LineNumber"":10,""Product"":""Flex-voegmortel beige 2kg"",""ProductCode"":""545753"",""EAN"":""5413503590100"",""Quantity"":8,""Unit"":""ST"",""RawLine"":""Flex-voegmortel beige 2kg (360) 8 ST""}
]
Texte à analyser:";

            var sb = new StringBuilder();
            sb.AppendLine(instruction);
            sb.AppendLine();
            sb.AppendLine(text);
            sb.AppendLine();
            // rien d'autre que le JSON attendu

            return sb.ToString();
        }

        private string CondenseForAi(string fullText)
        {
            if (string.IsNullOrWhiteSpace(fullText)) return string.Empty;
            var lines = fullText.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
            var sb = new StringBuilder();
            bool inTable = false;
            foreach (var raw in lines)
            {
                var line = (raw ?? string.Empty).TrimEnd();
                var lower = line.ToLowerInvariant();
                if (!inTable && (lower.Contains("beschrijving") || lower.Contains("artikel") || lower.Contains("pos.")))
                {
                    inTable = true;
                    continue;
                }
                if (inTable && (lower.Contains("totaal posities") || lower.Contains("totaal") || lower.Contains("betalingsvoorwaarden")))
                {
                    inTable = false;
                }
                if (!inTable) continue;

                // Lignes utiles à l’extraction
                if (lower.Contains("bank") || lower.Contains("rekening") || lower.Contains("iban") || lower.Contains("bic") || lower.Contains("swift"))
                {
                    continue;
                }
                if (System.Text.RegularExpressions.Regex.IsMatch(line, @"\b\d{13}\b") ||
                    System.Text.RegularExpressions.Regex.IsMatch(line, @"\b\d{2,3}\s+\d{4,}\b") ||
                    System.Text.RegularExpressions.Regex.IsMatch(line, @"\b\d+\s*(ST|PAK)\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                {
                    sb.AppendLine(line);
                }
            }
            var condensed = sb.ToString();
            return string.IsNullOrWhiteSpace(condensed) ? fullText : condensed;
        }

        private List<AiLine> ParseAIResponse(string aiResponse)
        {
            try
            {
                // Nettoyer la réponse
                var cleanResponse = aiResponse?.Trim() ?? string.Empty;

                // Enlever d'éventuelles fences ```json ... ```
                if (cleanResponse.StartsWith("```"))
                {
                    cleanResponse = cleanResponse.Trim('`');
                    var idx = cleanResponse.IndexOf('\n');
                    if (idx >= 0) cleanResponse = cleanResponse.Substring(idx + 1).Trim();
                }

                // Essayer d'extraire un tableau JSON équilibré; sinon reconstituer à partir d'objets
                cleanResponse = ExtractJsonArray(cleanResponse);

                return JsonSerializer.Deserialize<List<AiLine>>(cleanResponse, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    NumberHandling = JsonNumberHandling.AllowReadingFromString
                }) ?? new List<AiLine>();
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Réponse IA non JSON, tableau vide");
                return new List<AiLine>();
            }
        }

        // Essaie d'obtenir un tableau JSON valide à partir de la sortie du modèle
        private static string ExtractJsonArray(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "[]";

            // 1) Si on trouve un bloc [ ... ], le retourner
            int firstBracket = text.IndexOf('[');
            int lastBracket = text.LastIndexOf(']');
            if (firstBracket >= 0 && lastBracket > firstBracket)
            {
                var segment = text.Substring(firstBracket, lastBracket - firstBracket + 1);
                return segment;
            }

            // 2) Sinon, extraire des objets { ... } équilibrés et construire un tableau
            var items = new List<string>();
            int depth = 0;
            int start = -1;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '{')
                {
                    if (depth == 0) start = i;
                    depth++;
                }
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0 && start >= 0)
                    {
                        int len = i - start + 1;
                        var obj = text.Substring(start, len);
                        items.Add(obj);
                        start = -1;
                    }
                }
            }

            if (items.Count == 0)
            {
                // 3) Dernier recours: si le texte lui-même ressemble à un objet, envelopper dans un tableau
                var trimmed = text.Trim();
                if (trimmed.StartsWith("{") && trimmed.EndsWith("}"))
                {
                    return "[" + trimmed + "]";
                }
                return "[]";
            }

            var sb = new StringBuilder();
            sb.Append('[');
            for (int i = 0; i < items.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(items[i]);
            }
            sb.Append(']');
            return sb.ToString();
        }

        // Découpe le bloc table en sous-blocs par page/sections, ou par taille
        private static IEnumerable<string> SplitIntoChunks(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) yield break;
            var lines = text.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');

            var sb = new StringBuilder();
            int currentLen = 0;
            foreach (var raw in lines)
            {
                var line = raw ?? string.Empty;
                var lower = line.ToLowerInvariant();
                // Délimiteurs de page/section courants
                bool isDelimiter = lower.Contains("leveringsbevestiging") || lower.Contains("pagina");

                if (isDelimiter && sb.Length > 0)
                {
                    yield return sb.ToString();
                    sb.Clear();
                    currentLen = 0;
                    continue;
                }

                sb.AppendLine(line);
                currentLen += line.Length + 1;

                // Garde-fou taille (environ ~2000 chars par chunk)
                if (currentLen >= 2000)
                {
                    yield return sb.ToString();
                    sb.Clear();
                    currentLen = 0;
                }
            }
            if (sb.Length > 0) yield return sb.ToString();
        }

        // Découpe en blocs individuels à partir des lignes de code produit (ex: "10 545753")
        private static IEnumerable<string> SplitIntoCodeBlocks(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) yield break;
            var lines = text.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
            var codeRegex = new System.Text.RegularExpressions.Regex(@"^\s*\d{1,3}\s+\d{4,}\b", System.Text.RegularExpressions.RegexOptions.Compiled);
            int i = 0;
            while (i < lines.Length)
            {
                if (!codeRegex.IsMatch(lines[i] ?? string.Empty)) { i++; continue; }
                var sb = new StringBuilder();
                // Inclure la ligne code
                sb.AppendLine(lines[i]);
                // Inclure jusqu'à 8 lignes suivantes (EAN + description + quantité)
                int limit = System.Math.Min(i + 8, lines.Length - 1);
                for (int k = i + 1; k <= limit; k++)
                {
                    sb.AppendLine(lines[k]);
                    // stop anticipé si on rencontre un autre code
                    if (codeRegex.IsMatch(lines[k] ?? string.Empty)) break;
                }
                yield return sb.ToString();
                i++;
            }
        }

        // Fallback déterministe simple basé sur regex pour code/EAN/quantité
        private static List<DocumentLine> RuleBasedExtract(string text)
        {
            var results = new List<DocumentLine>();
            if (string.IsNullOrWhiteSpace(text)) return results;
            var lines = text.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
            var codeRegex = new System.Text.RegularExpressions.Regex(@"^\s*(\d{1,3})\s+(\d{4,})\b", System.Text.RegularExpressions.RegexOptions.Compiled);
            var eanRegex = new System.Text.RegularExpressions.Regex(@"\b(\d{13})\b", System.Text.RegularExpressions.RegexOptions.Compiled);

            // Units: ST, PAK
            var cfg = new Backup.Web.Api.Server.Services.Documents.Parsing.DocumentParserConfig();

            for (int i = 0; i < lines.Length; i++)
            {
                var m = codeRegex.Match(lines[i] ?? string.Empty);
                if (!m.Success) continue;
                int lineNum = int.TryParse(m.Groups[1].Value, out var ln) ? ln : 0;
                var productCode = m.Groups[2].Value;

                string? ean = null;
                int eanAt = -1;
                for (int k = i + 1; k <= i + 6 && k < lines.Length; k++)
                {
                    var t = (lines[k] ?? string.Empty).TrimEnd();
                    if (string.IsNullOrWhiteSpace(t)) continue;
                    var em = eanRegex.Match(t);
                    if (em.Success) { ean = em.Groups[1].Value; eanAt = k; break; }
                }

                decimal qty = 0m; string? unit = null;
                string desc = string.Empty;
                for (int k = i + 1; k <= i + 10 && k < lines.Length; k++)
                {
                    var t = (lines[k] ?? string.Empty).TrimEnd();
                    if (string.IsNullOrWhiteSpace(t)) continue;
                    if (codeRegex.IsMatch(t)) break;
                    if (qty == 0m &&
                        Backup.Web.Api.Server.Services.Documents.Parsing.QuantityExtractor.TryExtractPieces(cfg, t, out var idx, out var len, out var q, out var u) && q != 0m)
                    {
                        qty = q; unit = u;
                        desc = idx > 0 ? t.Substring(0, idx).Trim() : desc;
                        break;
                    }
                    // mémoriser dernière ligne alphabétique comme description candidate
                    if (System.Text.RegularExpressions.Regex.IsMatch(t, @"[A-Za-z]")) desc = t;
                }

                if (qty != 0m)
                {
                    results.Add(new DocumentLine
                    {
                        LineNumber = lineNum,
                        ProductCode = productCode,
                        Ean = ean,
                        Product = desc,
                        Quantity = qty,
                        Unit = unit,
                        RawLine = desc
                    });
                }
            }

            return results;
        }

        private List<DocumentLine> ValidateAILines(List<AiLine> aiLines)
        {
            var results = new List<DocumentLine>();

            foreach (var aiLine in aiLines)
            {
                if (aiLine == null) 
                    continue;

                // Validation stricte
                var unit = (aiLine.Unit ?? "").Trim().ToUpperInvariant();
                if (unit != "ST" && unit != "PAK") 
                    continue;

                if (aiLine.Quantity <= 0 || aiLine.Quantity > 1000) 
                    continue;

                var productName = (aiLine.Product ?? "").Trim();
                if (string.IsNullOrWhiteSpace(productName)) 
                    continue;
                var lowerName = productName.ToLowerInvariant();
                if (lowerName.Contains("bank") || lowerName.Contains("rekening") || lowerName.Contains("iban") || lowerName.Contains("bic") || lowerName.Contains("swift"))
                    continue;
                // Exclure palettes
                if (lowerName.Contains("euro-palet") || lowerName.Contains("euro palet") || lowerName.Contains("palet") || lowerName.Contains("palette"))
                    continue;
                // Troncature aux tokens de footer/en-tête
                productName = TruncateAtTokens(productName, new[] { "bank", "rekening", "iban", "bic", "swift", "www.knauf.com", "algemene verkoopsvoorwaarden", "leveringsbevestiging", "pagina", "factuur", "invoice" });

                // Normaliser ProductCode: prendre le bloc numérique de droite (4+ chiffres) s'il y a des espaces (ex: "110 6260" => "6260")
                var rawCode = (aiLine.ProductCode ?? string.Empty).Trim();
                string normalizedCode = rawCode;
                var codeMatches = System.Text.RegularExpressions.Regex.Matches(rawCode, @"\b(\d{4,})\b");
                if (codeMatches.Count > 0)
                {
                    normalizedCode = codeMatches[^1].Groups[1].Value;
                }

                // EAN: 13 chiffres uniquement
                var ean = (aiLine.EAN ?? string.Empty).Trim();
                if (!System.Text.RegularExpressions.Regex.IsMatch(ean, @"^\d{13}$")) ean = string.Empty;

                // Quantity entière uniquement
                var qty = Math.Truncate(aiLine.Quantity);
                if (qty <= 0) continue;

                results.Add(new DocumentLine
                {
                    LineNumber = aiLine.LineNumber,
                    Product = productName,
                    ProductCode = string.IsNullOrWhiteSpace(normalizedCode) ? "UNK" : normalizedCode,
                    Ean = ean,
                    Quantity = (decimal)qty,
                    Unit = unit,
                    RawLine = string.IsNullOrWhiteSpace(aiLine.RawLine) ? $"{productName} {qty} {unit}" : aiLine.RawLine
                });
            }

            return results;
        }

        private static string TruncateAtTokens(string s, string[] tokens)
        {
            if (string.IsNullOrWhiteSpace(s)) return s;
            var lower = s.ToLowerInvariant();
            int cut = -1;
            foreach (var t in tokens)
            {
                var idx = lower.IndexOf(t);
                if (idx >= 0) cut = cut == -1 ? idx : Math.Min(cut, idx);
            }
            if (cut <= 0) return s.Trim();
            return s.Substring(0, cut).Trim();
        }

        // Classes internes pour la désérialisation
        private sealed class GenerateRequest
        {
            [JsonPropertyName("model")] public string Model { get; set; } = string.Empty;
            [JsonPropertyName("prompt")] public string Prompt { get; set; } = string.Empty;
            [JsonPropertyName("stream")] public bool Stream { get; set; } = false;
            [JsonPropertyName("format")] public string? Format { get; set; }
            [JsonPropertyName("options")] public GenerateOptions Options { get; set; } = new();
        }

        private sealed class GenerateOptions
        {
            [JsonPropertyName("temperature")] public double Temperature { get; set; }
            [JsonPropertyName("num_ctx")] public int MaxContext { get; set; }
        }

        private sealed class GenerateResponse
        {
            [JsonPropertyName("response")] public string Response { get; set; } = string.Empty;
        }

        private sealed class AiLine
        {
            public int LineNumber { get; set; }
            public string Product { get; set; } = string.Empty;
            public string ProductCode { get; set; } = string.Empty;
            public string EAN { get; set; } = string.Empty;
            public decimal Quantity { get; set; }
            public string Unit { get; set; } = "ST";
            public string RawLine { get; set; } = string.Empty;
        }
    }
}