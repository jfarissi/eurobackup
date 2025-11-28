using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Models;

namespace Backup.Web.Api.Server.Services.Documents.Ollama
{
	public interface IOllamaParsingService
	{
		Task<List<DocumentLine>> TryParseAsync(string fullText, CancellationToken ct);
	}
}

