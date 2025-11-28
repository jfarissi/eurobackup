using System;

namespace Backup.Web.Api.Server.Services.Documents.Ollama
{
	public sealed class OllamaOptions
	{
		public bool Enabled { get; set; } = false;
		public string Host { get; set; } = "http://localhost:11434";
		public string Model { get; set; } = "qwen2.5:7b-instruct";
		public double Temperature { get; set; } = 0.0;
		public int TimeoutSeconds { get; set; } = 30;
		public int MaxContext { get; set; } = 4096;
	}
}

