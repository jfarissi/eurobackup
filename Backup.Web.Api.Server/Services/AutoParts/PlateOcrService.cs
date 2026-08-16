using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Backup.Web.Api.Server.Services.AutoParts
{
    public record PlateOcrResult(
        string? PlateNumber,
        string Provider,
        bool IsDemo,
        double? Score,
        string? RawMessage);

    public interface IPlateOcrService
    {
        Task<PlateOcrResult> RecognizeAsync(IFormFile image, string? countryHint, CancellationToken ct = default);
    }

    /// <summary>
    /// OCR plaque pluggable : PlateRecognizer (recommandé), OpenAlpr, Custom webhook, Demo.
    /// </summary>
    public class PlateOcrService : IPlateOcrService
    {
        private readonly IHttpClientFactory httpFactory;
        private readonly IOptions<PlateScanOptions> options;
        private readonly ILogger<PlateOcrService> logger;

        public PlateOcrService(
            IHttpClientFactory httpFactory,
            IOptions<PlateScanOptions> options,
            ILogger<PlateOcrService> logger)
        {
            this.httpFactory = httpFactory;
            this.options = options;
            this.logger = logger;
        }

        public async Task<PlateOcrResult> RecognizeAsync(
            IFormFile image, string? countryHint, CancellationToken ct = default)
        {
            if (image == null || image.Length == 0)
                throw new InvalidOperationException("Image de plaque requise.");

            var cfg = options.Value;
            var provider = (cfg.OcrProvider ?? "Demo").Trim();

            try
            {
                if (string.Equals(provider, "PlateRecognizer", StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(cfg.PlateRecognizerToken))
                {
                    var result = await RecognizePlateRecognizerAsync(image, countryHint, ct);
                    if (!string.IsNullOrWhiteSpace(result.PlateNumber))
                        return result;
                    if (cfg.RequireRealOcr)
                        throw new InvalidOperationException(
                            "OCR Plate Recognizer : aucune plaque détectée sur l’image.");
                }
                else if (string.Equals(provider, "OpenAlpr", StringComparison.OrdinalIgnoreCase)
                         && !string.IsNullOrWhiteSpace(cfg.OpenAlprSecretKey))
                {
                    var result = await RecognizeOpenAlprAsync(image, ct);
                    if (!string.IsNullOrWhiteSpace(result.PlateNumber))
                        return result;
                    if (cfg.RequireRealOcr)
                        throw new InvalidOperationException(
                            "OCR OpenALPR : aucune plaque détectée sur l’image.");
                }
                else if (string.Equals(provider, "Custom", StringComparison.OrdinalIgnoreCase)
                         && !string.IsNullOrWhiteSpace(cfg.ApiKey)
                         && !string.IsNullOrWhiteSpace(cfg.ProviderBaseUrl))
                {
                    var result = await RecognizeCustomAsync(image, ct);
                    if (!string.IsNullOrWhiteSpace(result.PlateNumber))
                        return result;
                    if (cfg.RequireRealOcr)
                        throw new InvalidOperationException(
                            "OCR custom : aucune plaque détectée sur l’image.");
                }
                else if (!string.Equals(provider, "Demo", StringComparison.OrdinalIgnoreCase)
                         && cfg.RequireRealOcr)
                {
                    throw new InvalidOperationException(
                        $"OCR « {provider} » non configuré (token / clé manquant). " +
                        "Renseignez PlateScan dans appsettings ou passez OcrProvider=Demo.");
                }
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "OCR plaque ({Provider}) a échoué", provider);
                if (cfg.RequireRealOcr && !string.Equals(provider, "Demo", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(
                        $"Échec OCR plaque ({provider}). Vérifiez la clé API et le réseau.");
            }

            return BuildDemoPlate(image);
        }

        private async Task<PlateOcrResult> RecognizePlateRecognizerAsync(
            IFormFile image, string? countryHint, CancellationToken ct)
        {
            var cfg = options.Value;
            var client = httpFactory.CreateClient("PlateScan");
            await using var stream = image.OpenReadStream();
            using var content = new MultipartFormDataContent();
            var fileContent = new StreamContent(stream);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(
                string.IsNullOrWhiteSpace(image.ContentType) ? "image/jpeg" : image.ContentType);
            content.Add(fileContent, "upload", image.FileName ?? "plate.jpg");

            var regions = ResolveRegions(countryHint, cfg.PlateRecognizerRegions);
            foreach (var region in regions)
                content.Add(new StringContent(region), "regions");

            using var request = new HttpRequestMessage(HttpMethod.Post, EnsureTrailingSlash(cfg.PlateRecognizerUrl));
            request.Headers.TryAddWithoutValidation("Authorization", $"Token {cfg.PlateRecognizerToken!.Trim()}");
            request.Content = content;

            var response = await client.SendAsync(request, ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Plate Recognizer HTTP {Status}: {Body}", (int)response.StatusCode, Truncate(body));
                throw new InvalidOperationException(
                    $"Plate Recognizer a répondu {(int)response.StatusCode}. Vérifiez le token.");
            }

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (!root.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array)
                return new PlateOcrResult(null, "PlateRecognizer", false, null, "Pas de résultats OCR.");

            string? bestPlate = null;
            double bestScore = -1;
            foreach (var item in results.EnumerateArray())
            {
                var plate = item.TryGetProperty("plate", out var p) ? p.GetString() : null;
                double score = 0;
                if (item.TryGetProperty("score", out var s) && s.ValueKind == JsonValueKind.Number)
                    score = s.GetDouble();
                else if (item.TryGetProperty("dscore", out var ds) && ds.ValueKind == JsonValueKind.Number)
                    score = ds.GetDouble();

                if (!string.IsNullOrWhiteSpace(plate) && score >= bestScore)
                {
                    bestScore = score;
                    bestPlate = plate;
                }
            }

            return new PlateOcrResult(
                bestPlate,
                "PlateRecognizer",
                false,
                bestScore >= 0 ? bestScore : null,
                bestPlate == null ? "Aucune plaque dans results[]" : null);
        }

        private async Task<PlateOcrResult> RecognizeOpenAlprAsync(IFormFile image, CancellationToken ct)
        {
            var cfg = options.Value;
            await using var stream = image.OpenReadStream();
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms, ct);
            var b64 = Convert.ToBase64String(ms.ToArray());

            var client = httpFactory.CreateClient("PlateScan");
            var url =
                $"{cfg.OpenAlprUrl.TrimEnd('/')}?secret_key={Uri.EscapeDataString(cfg.OpenAlprSecretKey!.Trim())}" +
                $"&country={Uri.EscapeDataString(cfg.OpenAlprCountry ?? "eu")}&recognize_vehicle=0";

            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(b64, Encoding.UTF8, "application/json")
            };

            var response = await client.SendAsync(request, ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("OpenALPR HTTP {Status}: {Body}", (int)response.StatusCode, Truncate(body));
                throw new InvalidOperationException($"OpenALPR a répondu {(int)response.StatusCode}.");
            }

            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array)
                return new PlateOcrResult(null, "OpenAlpr", false, null, "Pas de résultats.");

            string? best = null;
            double bestConf = -1;
            foreach (var item in results.EnumerateArray())
            {
                var plate = item.TryGetProperty("plate", out var p) ? p.GetString() : null;
                var conf = item.TryGetProperty("confidence", out var c) && c.ValueKind == JsonValueKind.Number
                    ? c.GetDouble()
                    : 0;
                if (!string.IsNullOrWhiteSpace(plate) && conf >= bestConf)
                {
                    bestConf = conf;
                    best = plate;
                }
            }

            return new PlateOcrResult(best, "OpenAlpr", false, bestConf >= 0 ? bestConf / 100.0 : null, null);
        }

        private async Task<PlateOcrResult> RecognizeCustomAsync(IFormFile image, CancellationToken ct)
        {
            var cfg = options.Value;
            var client = httpFactory.CreateClient("PlateScan");
            using var content = new MultipartFormDataContent();
            await using var stream = image.OpenReadStream();
            content.Add(new StreamContent(stream), "image", image.FileName ?? "plate.jpg");

            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"{cfg.ProviderBaseUrl!.TrimEnd('/')}/plate/ocr");
            request.Headers.TryAddWithoutValidation("X-Api-Key", cfg.ApiKey);
            request.Content = content;

            var response = await client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Custom OCR HTTP {Status}", (int)response.StatusCode);
                throw new InvalidOperationException($"OCR custom a répondu {(int)response.StatusCode}.");
            }

            var payload = await response.Content.ReadFromJsonAsync<CustomOcrResponse>(cancellationToken: ct);
            return new PlateOcrResult(
                payload?.PlateNumber,
                "Custom",
                false,
                payload?.Score,
                null);
        }

        private static PlateOcrResult BuildDemoPlate(IFormFile image)
        {
            var seed = Math.Abs((image.FileName ?? "plate").GetHashCode(StringComparison.Ordinal) + (int)image.Length);
            var region = (seed % 80) + 1;
            var serial = (seed % 90000) + 10000;
            var letter = (char)('A' + (seed % 26));
            return new PlateOcrResult(
                $"{serial}-{letter}-{region}",
                "Demo",
                true,
                null,
                "OCR démo (aucune clé PlateScan configurée).");
        }

        private static IEnumerable<string> ResolveRegions(string? countryHint, string[]? configured)
        {
            var list = new List<string>();
            if (!string.IsNullOrWhiteSpace(countryHint))
                list.Add(countryHint.Trim().ToLowerInvariant());
            if (configured != null)
            {
                foreach (var r in configured)
                {
                    if (!string.IsNullOrWhiteSpace(r) && !list.Contains(r.Trim().ToLowerInvariant()))
                        list.Add(r.Trim().ToLowerInvariant());
                }
            }
            return list;
        }

        private static string EnsureTrailingSlash(string url)
        {
            var u = (url ?? string.Empty).Trim();
            return u.EndsWith('/') ? u : u + "/";
        }

        private static string Truncate(string? s) =>
            string.IsNullOrEmpty(s) ? string.Empty : (s.Length <= 300 ? s : s[..300]);

        private sealed class CustomOcrResponse
        {
            public string? PlateNumber { get; set; }
            public double? Score { get; set; }
        }
    }
}
