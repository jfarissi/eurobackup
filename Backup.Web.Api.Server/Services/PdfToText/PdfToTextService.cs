using System.Diagnostics;

namespace Backup.Web.Api.Server.Services
{
	public class PdfToTextService : IPdfToTextService
	{
		private readonly IConfiguration configuration;

		public PdfToTextService(IConfiguration configuration)
		{
			this.configuration = configuration;
		}

		public async Task<string> TryExtractAsync(string absolutePdfPath, CancellationToken cancellationToken = default)
		{
			try
			{
				if (!File.Exists(absolutePdfPath)) return string.Empty;
				if (!string.Equals(Path.GetExtension(absolutePdfPath), ".pdf", StringComparison.OrdinalIgnoreCase)) return string.Empty;

				var exePath = this.configuration.GetValue<string>("PdfToText:ExecutablePath");
				if (string.IsNullOrWhiteSpace(exePath))
				{
					// fallback to 'pdftotext' in PATH (Linux/WSL/installed)
					exePath = "pdftotext";
				}

				// write .txt next to the PDF
				var txtPath = Path.ChangeExtension(absolutePdfPath, ".txt")!;

				// default args: -layout -enc UTF-8 \"pdf\" \"txt\"
				var defaultArgs = "-layout -enc UTF-8";
				var extraArgs = this.configuration.GetValue<string>("PdfToText:Args");
				var finalArgs = string.IsNullOrWhiteSpace(extraArgs) ? defaultArgs : extraArgs;
				var args = $"{finalArgs} \"{absolutePdfPath}\" \"{txtPath}\"";

				var startInfo = new ProcessStartInfo
				{
					FileName = exePath,
					Arguments = args,
					RedirectStandardError = true,
					RedirectStandardOutput = true,
					UseShellExecute = false,
					CreateNoWindow = true
				};

				using var process = new Process { StartInfo = startInfo };
				process.Start();

				var timeoutSeconds = Math.Max(5, this.configuration.GetValue<int?>("PdfToText:TimeoutSeconds") ?? 30);
				var exited = await Task.Run(() => process.WaitForExit(timeoutSeconds * 1000), cancellationToken);
				if (!exited)
				{
					try { process.Kill(true); } catch { }
					return string.Empty;
				}

				// If exit code non-zero, still check if txt exists and has content (some versions still write)
				if (!File.Exists(txtPath)) return string.Empty;
				var content = await File.ReadAllTextAsync(txtPath, cancellationToken);

				// Heuristic: consider empty/very short content as failure
				if (string.IsNullOrWhiteSpace(content) || content.Trim().Length < 10) return string.Empty;

				return content;
			}
			catch
			{
				return string.Empty;
			}
		}
	}
}


