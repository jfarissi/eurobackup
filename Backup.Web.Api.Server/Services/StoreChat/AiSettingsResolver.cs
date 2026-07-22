using Microsoft.Extensions.Configuration;

namespace Backup.Web.Api.Server.Services.StoreChat
{
    public static class AiSettingsResolver
    {
        public static AiSettingsOptions Resolve(IConfiguration configuration)
        {
            var provider = configuration["AISettings:Provider"] ?? "Ollama";
            var normalized = provider.Trim();
            var section = $"AISettings:{normalized}";

            var apiKey = configuration[$"{section}:ApiKey"]
                ?? configuration["AISettings:ApiKey"]
                ?? string.Empty;

            if (string.Equals(normalized, "Ollama", StringComparison.OrdinalIgnoreCase)
                && string.IsNullOrWhiteSpace(apiKey))
            {
                apiKey = "ollama";
            }

            var model = configuration[$"{section}:Model"]
                ?? configuration["AISettings:Model"]
                ?? "qwen2.5:14b";

            var endpoint = configuration[$"{section}:Endpoint"]
                ?? configuration["AISettings:Endpoint"]
                ?? (string.Equals(normalized, "Ollama", StringComparison.OrdinalIgnoreCase)
                    ? "http://localhost:11434"
                    : string.Empty);

            var embedding = configuration[$"{section}:EmbeddingModel"]
                ?? configuration["AISettings:EmbeddingModel"]
                ?? "nomic-embed-text";

            return new AiSettingsOptions
            {
                Provider = normalized,
                ApiKey = apiKey,
                Model = model,
                Endpoint = endpoint,
                SystemPrompt = configuration["AISettings:SystemPrompt"],
                EmbeddingModel = embedding
            };
        }
    }
}
