using System.Collections.Generic;
using System.Linq;

namespace Backup.Web.Api.Server.Services.Documents.Parsing
{
	public static class TableExtractor
	{
		public static IEnumerable<string> ExtractProductTable(IEnumerable<string> lines)
		{
			if (lines == null) yield break;
			bool inTable = false;

			foreach (var raw in lines)
			{
				var line = (raw ?? string.Empty).Trim();
				var lower = line.ToLowerInvariant();

				// Start of table block
				if (!inTable && (lower.Contains("artikel") || lower.Contains("pos.") || lower.Contains("p_o_s")))
				{
					inTable = true;
					continue;
				}

				// End of table block
				if (inTable && (lower.Contains("totaal posities") || lower.Contains("totaal positions") ||
				                lower.Contains("totaal") ||
				                line.Replace("_", "").Trim().Length == 0))
				{
					inTable = false;
				}

				if (inTable) yield return raw;
			}
		}
	}
}


