using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Backup.Web.Api.Server.Models;

namespace Backup.Web.Api.Server.Services.Documents.Parsing
{
	public class SpanishDeliveryNoteParser : IDocumentParser
	{
		public bool CanParse(DocumentLanguage language, string text)
		{
			if (string.IsNullOrWhiteSpace(text)) return false;
			var lower = text.ToLowerInvariant();
			// Nota de entrega / Lista de productos / encabezados Producto-Cantidad
			return lower.Contains("nota de entrega") || (lower.Contains("lista de productos") && lower.Contains("producto") && (lower.Contains("cantidad") || lower.Contains("cant.")));
		}

		public List<DocumentLine> Parse(DocumentLanguage language, string text)
		{
			var results = new List<DocumentLine>();
			if (string.IsNullOrWhiteSpace(text)) return results;
			var lines = text.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');

			// detect header row
			int start = -1;
			for (int i = 0; i < lines.Length; i++)
			{
				var l = (lines[i] ?? string.Empty).Trim();
				if (string.IsNullOrWhiteSpace(l)) continue;
				var lower = l.ToLowerInvariant();
				if (lower.Contains("producto") && (lower.Contains("cantidad") || lower.Contains("cant.")))
				{
					start = i + 1;
					break;
				}
			}
			if (start == -1) return results;

			for (int i = start; i < lines.Length; i++)
			{
				var raw = (lines[i] ?? string.Empty).TrimEnd();
				if (string.IsNullOrWhiteSpace(raw)) continue;
				// stop on totals/footer markers
				var low = raw.ToLowerInvariant();
				if (low.Contains("total")) break;
				if (Regex.IsMatch(low, @"^\f")) break; // page break

				// Split by 2+ spaces into columns; product left, quantity right
				var cols = Regex.Split(raw.Trim(), @"\s{2,}").Where(c => !string.IsNullOrWhiteSpace(c)).ToArray();
				if (cols.Length < 1) continue;

				string product = cols[0].Trim();
				decimal qty = 0m;
				// try last column first
				for (int c = cols.Length - 1; c >= 0; c--)
				{
					var token = cols[c].Trim();
					var m = Regex.Match(token, @"\b(\d+)\b");
					if (m.Success && decimal.TryParse(m.Groups[1].Value, out var q) && q > 0)
					{
						qty = q;
						break;
					}
				}
				if (qty == 0m) continue;

				results.Add(new DocumentLine
				{
					LineNumber = 0,
					Product = product,
					ProductCode = string.Empty,
					Ean = null,
					Quantity = qty,
					Unit = "ST",
					RawLine = raw
				});
			}

			return results;
		}
	}
}


