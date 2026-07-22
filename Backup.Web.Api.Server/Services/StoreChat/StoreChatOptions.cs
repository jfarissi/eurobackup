namespace Backup.Web.Api.Server.Services.StoreChat
{
    public class StoreChatOptions
    {
        public const string SectionName = "StoreChat";

        public string BrandName { get; set; } = "EuroBrico";
        public string ReturnBaseUrl { get; set; } = "http://localhost";
        public int MaxProductResults { get; set; } = 5;
        /// <summary>Nombre de produits montrés d'emblée (style vendeur).</summary>
        public int InitialProductResults { get; set; } = 3;
        public int ChatHistoryLimit { get; set; } = 12;
    }

    public class AiSettingsOptions
    {
        public const string SectionName = "AISettings";

        public string Provider { get; set; } = "Ollama";
        public string ApiKey { get; set; } = string.Empty;
        public string Model { get; set; } = "qwen2.5:14b";
        public string Endpoint { get; set; } = "http://host.docker.internal:11434";
        public string? SystemPrompt { get; set; }
        public string EmbeddingModel { get; set; } = "nomic-embed-text";
    }

    public class StripeOptions
    {
        public const string SectionName = "Stripe";

        public string SecretKey { get; set; } = string.Empty;
        public string PublishableKey { get; set; } = string.Empty;
        public bool Enabled => !string.IsNullOrWhiteSpace(SecretKey);
    }
}
