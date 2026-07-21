using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Backup.Web.Api.Server.Services.StoreChat
{
    public interface IStoreChatAiClient
    {
        Task<string?> CompleteAsync(
            IReadOnlyList<StoreChatHistoryMessage> history,
            string userMessage,
            string catalogContext,
            CancellationToken ct = default);
    }

    public class StoreChatAiClient : IStoreChatAiClient
    {
        private readonly HttpClient _http;
        private readonly AiSettingsOptions _ai;
        private readonly StoreChatOptions _store;
        private readonly ILogger<StoreChatAiClient> _logger;

        public StoreChatAiClient(
            HttpClient http,
            IConfiguration configuration,
            IOptions<StoreChatOptions> store,
            ILogger<StoreChatAiClient> logger)
        {
            _http = http;
            _ai = AiSettingsResolver.Resolve(configuration);
            _store = store.Value ?? new StoreChatOptions();
            _logger = logger;
        }

        public async Task<string?> CompleteAsync(
            IReadOnlyList<StoreChatHistoryMessage> history,
            string userMessage,
            string catalogContext,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(_ai.Endpoint) || string.IsNullOrWhiteSpace(_ai.Model))
                return null;

            try
            {
                var endpoint = EnsureOpenAiV1(_ai.Endpoint).TrimEnd('/') + "/chat/completions";
                var system = _ai.SystemPrompt
                    ?? $"Tu es l'assistant magasin {_store.BrandName}. Tu aides les clients en français à trouver des produits de bricolage. "
                       + "Réponds de façon courte et claire. Si des produits catalogue sont fournis, recommande-les.";

                var messages = new List<object>
                {
                    new { role = "system", content = system + "\n\nCatalogue pertinent:\n" + catalogContext }
                };

                foreach (var msg in history.TakeLast(Math.Max(2, _store.ChatHistoryLimit)))
                {
                    messages.Add(new { role = msg.Role, content = msg.Content });
                }

                messages.Add(new { role = "user", content = userMessage });

                using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
                var apiKey = string.IsNullOrWhiteSpace(_ai.ApiKey) && IsOllama(_ai.Provider)
                    ? "ollama"
                    : _ai.ApiKey;
                if (!string.IsNullOrWhiteSpace(apiKey))
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

                var body = new
                {
                    model = _ai.Model,
                    temperature = 0.3,
                    messages
                };

                request.Content = new StringContent(
                    JsonSerializer.Serialize(body),
                    Encoding.UTF8,
                    "application/json");

                using var response = await _http.SendAsync(request, ct);
                var json = await response.Content.ReadAsStringAsync(ct);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("StoreChat AI HTTP {Status}: {Body}", (int)response.StatusCode, Truncate(json));
                    return null;
                }

                using var doc = JsonDocument.Parse(json);
                var content = doc.RootElement
                    .GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString();

                return string.IsNullOrWhiteSpace(content) ? null : content.Trim();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "StoreChat AI completion failed");
                return null;
            }
        }

        private static bool IsOllama(string? provider) =>
            string.Equals(provider, "Ollama", StringComparison.OrdinalIgnoreCase);

        private static string EnsureOpenAiV1(string endpoint)
        {
            var trimmed = endpoint.Trim().TrimEnd('/');
            if (trimmed.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
                return trimmed;
            if (trimmed.Contains(":11434", StringComparison.Ordinal))
                return trimmed + "/v1";
            return trimmed;
        }

        private static string Truncate(string value) =>
            value.Length <= 400 ? value : value[..400] + "…";
    }
}
