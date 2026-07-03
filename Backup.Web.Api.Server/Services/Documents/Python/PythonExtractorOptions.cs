using System.Collections.Generic;

namespace Backup.Web.Api.Server.Services.Documents.Python
{
	public sealed class DocumentAiOllamaProfileOptions
	{
		public string Model { get; set; } = string.Empty;
		public string? Description { get; set; }
	}

	public sealed class DocumentAiOptions
	{
		public string OllamaHost { get; set; } = "http://localhost:11434";
		public string ActiveProfile { get; set; } = "Primary";
		public Dictionary<string, DocumentAiOllamaProfileOptions> Profiles { get; set; } = new();
		public List<string> FallbackProfiles { get; set; } = new();
	}

	public class PythonExtractorOptions
	{
		public bool Enabled { get; set; } = false;
		public string Url { get; set; } = "http://localhost:8000";
		public string OpenAiApiKey { get; set; } = string.Empty;
		public string GeminiApiKey { get; set; } = string.Empty;

		/// <summary>Appelle /parse avec use_ai=true lorsque activé.</summary>
		public bool UseAiForPythonParse { get; set; }

		/// <summary>openai, gemini ou ollama (défaut : ollama, local).</summary>
		public string DefaultAiProvider { get; set; } = "ollama";
		/// <summary>Pipeline Python à appeler: auto | ollama | factory | classifier.</summary>
		public string ParseEngine { get; set; } = "ollama";

		public DocumentAiOptions DocumentAi { get; set; } = new();
	}
}


