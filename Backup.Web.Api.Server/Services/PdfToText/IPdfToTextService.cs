namespace Backup.Web.Api.Server.Services
{
	public interface IPdfToTextService
	{
		/// <summary>
		/// Tries to extract structured text from a PDF using 'pdftotext -layout'.
		/// Returns empty string on failure.
		/// </summary>
		Task<string> TryExtractAsync(string absolutePdfPath, CancellationToken cancellationToken = default);
	}
}


