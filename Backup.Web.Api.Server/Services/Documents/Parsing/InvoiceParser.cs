using System.Collections.Generic;
using System.Text.RegularExpressions;
using Backup.Web.Api.Server.Models;

namespace Backup.Web.Api.Server.Services.Documents.Parsing
{
	public class InvoiceParser : IDocumentParser
	{
		private readonly DocumentParserConfig config;
		public InvoiceParser(DocumentParserConfig config) => this.config = config;

		private static readonly Regex InvLine = new(@"^\s*(\d{2,3})\s+(\d{4,})\s+(\d+)\s+([A-Za-z]{2,10})\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
		private static readonly Regex EanTag = new(@"EAN[-\s]*nr\.\s*:\s*(\d{13})", RegexOptions.Compiled | RegexOptions.IgnoreCase);
		private static readonly string[] NoiseTokens = new[] { "bank", "rekening", "iban", "bic", "swift", "www.knauf.com", "algemene verkoopsvoorwaarden", "leveringsbevestiging", "pagina", "factuur", "invoice" };

		private static string StripPriceNoise(string s)
		{
			if (string.IsNullOrWhiteSpace(s)) return s;
			var t = s;
			// remove "19,43 /1 ST" like tokens
			t = Regex.Replace(t, @"\b\d{1,3}(?:[.,]\d{2})\s*/\s*1\s*[A-Za-z\.]{2,6}\b", "", RegexOptions.IgnoreCase);
			// remove percentages "7,00-%"
			t = Regex.Replace(t, @"\b\d{1,3}(?:[.,]\d{1,2})\s*-%\b", "", RegexOptions.IgnoreCase);
			// remove standalone price numbers at start of segment
			t = Regex.Replace(t, @"^\s*\d{1,3}(?:[.,]\d{2})(?:\s+\d{1,3}(?:[.,]\d{2}))*\s*", "", RegexOptions.IgnoreCase);
			t = Regex.Replace(t, @"\s{2,}", " ").Trim();
			return t;
		}

		private static string TruncateAtTokens(string s, string[] tokens)
		{
			if (string.IsNullOrWhiteSpace(s)) return s;
			var lower = s.ToLowerInvariant();
			int cut = -1;
			foreach (var tok in tokens)
			{
				var idx = lower.IndexOf(tok);
				if (idx >= 0) cut = cut == -1 ? idx : System.Math.Min(cut, idx);
			}
			if (cut <= 0) return s.Trim();
			return s.Substring(0, cut).Trim();
		}

		public bool CanParse(DocumentLanguage language, string text)
		{
			var lower = text?.ToLowerInvariant() ?? string.Empty;
			foreach (var k in config.InvoiceKeywords)
				if (lower.Contains(k)) return true;
			// fallback: presence of header tokens seen in sample
			return lower.Contains("hoeveelheid") || lower.Contains("brutoprijs") || lower.Contains("nettoprijs");
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
					if (em.Success)
					{
						// keep product code as is; EAN not stored separately
						continue;
					}

					var trimmed = l.Trim();
					if (!string.IsNullOrWhiteSpace(trimmed))
					{
						// accumulate but clean line fragments
						var cleaned = StripPriceNoise(trimmed);
						cleaned = TruncateAtTokens(cleaned, NoiseTokens);
						if (!string.IsNullOrWhiteSpace(cleaned))
						{
							current.Product = (current.Product + " " + cleaned).Trim();
						}
					}
				}
			}

			if (current != null) results.Add(current);

			// Final clean and filter palettes
			results = results
				.Select(r =>
				{
					r.Product = TruncateAtTokens(StripPriceNoise(r.Product ?? string.Empty), NoiseTokens);
					return r;
				})
				.Where(r => !string.IsNullOrWhiteSpace(r.Product))
				.Where(r =>
				{
					var l = (r.Product ?? string.Empty).ToLowerInvariant();
					return !(l.Contains("euro-palet") || l.Contains("euro palet") || l.Contains("palet") || l.Contains("palette"));
				})
				.ToList();
			return results;
		}
	}
}


