using System.Collections.Generic;
using System.Text.RegularExpressions;
using Backup.Web.Api.Server.Models;

namespace Backup.Web.Api.Server.Services.Documents.Parsing.Suppliers
{
	public class KnaufInvoiceParser : IDocumentParser
	{
		private readonly DocumentParserConfig config;
		public KnaufInvoiceParser(DocumentParserConfig config) => this.config = config;

		private static readonly Regex InvLine = new(@"^\s*(\d{2,3})\s+(\d{4,})\s+(\d+)\s+([A-Za-z]{2,10})\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
		private static readonly Regex EanTag = new(@"EAN[-\s]*nr\.\s*:\s*(\d{13})", RegexOptions.Compiled | RegexOptions.IgnoreCase);

		public bool CanParse(DocumentLanguage language, string text)
		{
			if (string.IsNullOrWhiteSpace(text)) return false;
			var lower = text.ToLowerInvariant();
			return lower.Contains(" knauf ") || lower.Contains("n et b knauf") || lower.Contains("knauf.com");
		}

		public List<DocumentLine> Parse(DocumentLanguage language, string text)
		{
			var results = new List<DocumentLine>();
			if (string.IsNullOrWhiteSpace(text)) return results;

			var lines = text.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
			DocumentLine? current = null;
			for (int i = 0; i < lines.Length; i++)
			{
				var l = lines[i].TrimEnd();
				if (string.IsNullOrWhiteSpace(l)) continue;
				var m = InvLine.Match(l);
				if (m.Success)
				{
					if (current != null) results.Add(current);
					var unit = (m.Groups[4].Value ?? "").Trim().TrimEnd('.');
					if (!config.PieceUnits.Contains(unit)) unit = "ST";
					current = new DocumentLine
					{
						LineNumber = int.TryParse(m.Groups[1].Value, out var ln) ? ln : 0,
						ProductCode = m.Groups[2].Value,
						Quantity = decimal.TryParse(m.Groups[3].Value, out var q) ? q : 0,
						Unit = unit,
						Product = string.Empty
					};
					continue;
				}

				if (current != null)
				{
					var em = EanTag.Match(l);
					if (em.Success) continue;
					var trimmed = l.Trim();
					if (!string.IsNullOrWhiteSpace(trimmed))
						current.Product = (current.Product + " " + trimmed).Trim();
				}
			}

			if (current != null) results.Add(current);
			return results;
		}
	}
}


