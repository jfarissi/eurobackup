using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Backup.Web.Api.Server.Models;

namespace Backup.Web.Api.Server.Services.Documents.Python
{
	public interface IPythonExtractorClient
	{
		Task<List<DocumentLine>> TryExtractAsync(string absolutePdfPath, CancellationToken ct);
		Task<Backup.Web.Api.Server.Services.Documents.DocumentMetadata?> InspectMetadataAsync(string absolutePdfPath, CancellationToken ct);
	}
}


