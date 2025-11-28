using System.Text.RegularExpressions;
using Backup.Web.Api.Server.Models;

namespace Backup.Web.Api.Server.Services.Documents
{
	public class DocumentParserService : IDocumentParserService
	{
		private readonly Parsing.DocumentProcessor processor;
		private readonly Parsing.DocumentParserConfig parserConfig;

		public DocumentParserService(Parsing.DocumentProcessor processor, Parsing.DocumentParserConfig parserConfig)
		{
			this.processor = processor;
			this.parserConfig = parserConfig;
		}

		private static readonly Regex MultiSpace = new(@"\s{2,}", RegexOptions.Compiled);
		private static readonly Regex NumberToken = new(@"\d{1,3}(?:[ \u00A0]\d{3})*(?:[.,]\d+)?", RegexOptions.Compiled);
		private static readonly Regex IndexAtStart = new(@"^\s*(\d{1,4})\s+", RegexOptions.Compiled);
		private static readonly Regex ShortCode = new(@"^[A-Z0-9\-]{4,20}$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
		private static readonly Regex QtyWithUnit = new(@"\b(\d{1,3}(?:[ \u00A0]\d{3})*(?:[.,]\d+)?)(?:\s*(szt\.?|pcs|pc|op|pkt|ea|unit|st|stk|stuk|stuks|pak|pkg|set|ud|uds|adet\.?|kg|m2|m|mb|to|l))?\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);

		public IReadOnlyList<DocumentLine> Parse(string txt)
		{
			var results = new List<DocumentLine>();
			if (string.IsNullOrWhiteSpace(txt)) return results;

			// New modular path: detector + specialized parsers
			var modular = this.processor.Parse(txt);
			if (modular != null && modular.Count > 0) return modular;

			// Structured parsers for known layouts (more accurate than generic)
			var lower = txt.ToLowerInvariant();
			if (lower.Contains("leveringsbevestiging") || lower.Contains("verzendpunt") || lower.Contains("shipping point")
				|| lower.Contains("bon de livraison") || lower.Contains("delivery note") || lower.Contains("lieferschein") || lower.Contains("albarán"))
			{
				var parsed = ParseDeliveryNote(txt);
				if (parsed.Count > 0) return parsed;
			}
			if (lower.Contains("factuur") || lower.Contains("invoice") || lower.Contains("facture") || lower.Contains("rechnung") || lower.Contains("fatura") || lower.Contains("faktura"))
			{
				var parsed = ParseInvoice(txt);
				if (parsed.Count > 0) return parsed;
			}

			var lines = txt.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
			DocumentLine? current = null;
			int logicalLine = 0;

			foreach (var raw in lines)
			{
				var line = raw.TrimEnd();
				if (string.IsNullOrWhiteSpace(line)) continue;

				var mIdx = IndexAtStart.Match(line);
				if (mIdx.Success)
				{
					if (current != null) results.Add(current);
					logicalLine++;
					current = new DocumentLine
					{
						LineNumber = logicalLine,
						Product = string.Empty,
						ProductCode = null,
						Quantity = 0,
						Unit = null,
						UnitPrice = 0,
						TotalValue = 0
					};

					var after = line.Substring(mIdx.Length).Trim();
					ExtractInline(after, current);
					current.Product = (current.Product + " " + CleanSpaces(RemoveParsedSegments(after, current))).Trim();
				}
				else if (current != null)
				{
					// continuation: code or more description or trailing numbers
					var trimmed = line.Trim();
					if (ShortCode.IsMatch(trimmed) && string.IsNullOrEmpty(current.ProductCode))
					{
						current.ProductCode = trimmed;
						continue;
					}

					if (current.Quantity == 0)
					{
						if (TryExtractQtyUnit(trimmed, out var qIdx, out var qLen, out var qty, out var unit))
						{
							current.Quantity = qty;
							current.Unit = unit;
							trimmed = RemoveAt(trimmed, qIdx, qLen).Trim();
						}
					}

					// prices/values at the end
					var nums = NumberToken.Matches(trimmed).Cast<Match>().Select(m => m.Value).ToList();
					if (nums.Count > 0)
					{
						if (current.TotalValue == 0) current.TotalValue = ParseDecimal(nums.Last());
						if (nums.Count >= 2 && current.UnitPrice == 0) current.UnitPrice = ParseDecimal(nums[nums.Count - 2]);
						trimmed = TrimRightNumbers(trimmed);
					}

					current.Product = (current.Product + " " + trimmed).Trim();
				}
			}

			if (current != null) results.Add(current);
			return results;
		}

		private List<DocumentLine> ParseDeliveryNote(string txt)
		{
			var results = new List<DocumentLine>();
			var lines = txt.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');

			// Pattern for header/product code line: "10 545753 [EAN]"
			var codeLine = new Regex(@"^\s*(\d{2,3})\s+(\d{4,})(?:\s+(\d{13}))?\s*$", RegexOptions.Compiled);
			// EAN-only line: "5413503590100"
			var eanOnly = new Regex(@"^\s*(\d{13})\s*$", RegexOptions.Compiled);

			for (int i = 0; i < lines.Length; i++)
			{
				var l = lines[i].TrimEnd();
				if (string.IsNullOrWhiteSpace(l)) continue;
				var m = codeLine.Match(l);
				if (!m.Success) continue;

				var lineNum = SafeParseInt(m.Groups[1].Value);
				var productCode = m.Groups[2].Value;
				string? ean = null;
				if (m.Groups[3].Success) ean = m.Groups[3].Value;

				// Look-ahead for description + qty line within next 1-3 lines
				string? descQtyLine = null;
				int j = i + 1;
				for (; j < Math.Min(i + 4, lines.Length); j++)
				{
					var t = lines[j].TrimEnd();
					if (string.IsNullOrWhiteSpace(t)) continue;
					// If this is a pure EAN line, capture EAN and continue
					var em = eanOnly.Match(t);
					if (ean == null && em.Success) { ean = em.Groups[1].Value; continue; }
					descQtyLine = t;
					break;
				}

				if (descQtyLine == null) continue;

				// Extract quantity (pieces) from description+qty line
				if (!TryExtractQtyUnit(descQtyLine, out var qIdx, out var qLen, out var qty, out var unit) || qty == 0)
				{
					continue; // cannot trust line without a piece-quantity
				}

				// Product description: remove trailing numeric columns after the matched qty position
				var product = descQtyLine.Substring(0, qIdx).Trim();
				product = CleanSpaces(product);

				results.Add(new DocumentLine
				{
					LineNumber = lineNum,
					ProductCode = productCode,
					Product = product,
					Quantity = qty,
					Unit = unit,
					// optional weight could be parsed if needed from the right side (".. KG")
				});
			}

			return results;
		}

		private List<DocumentLine> ParseInvoice(string txt)
		{
			var results = new List<DocumentLine>();
			var lines = txt.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');

			// Pattern: "10 545753         8 ST   4,73 /1 ST ..." (quantity follows code on same line)
			var invLine = new Regex(@"^\s*(\d{2,3})\s+(\d{4,})\s+(\d+)\s+(ST|STK|PCS|PAK|EA)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
			var eanTag = new Regex(@"EAN[-\s]*nr\.\s*:\s*(\d{13})", RegexOptions.Compiled | RegexOptions.IgnoreCase);

			DocumentLine? current = null;
			for (int i = 0; i < lines.Length; i++)
			{
				var l = lines[i].TrimEnd();
				if (string.IsNullOrWhiteSpace(l)) continue;
				var m = invLine.Match(l);
				if (m.Success)
				{
					// finalize previous
					if (current != null) results.Add(current);

					current = new DocumentLine
					{
						LineNumber = SafeParseInt(m.Groups[1].Value),
						ProductCode = m.Groups[2].Value,
						Quantity = ParseDecimal(m.Groups[3].Value),
						Unit = m.Groups[4].Value.ToUpperInvariant(),
						Product = string.Empty
					};
					continue;
				}

				if (current != null)
				{
					// EAN line
					var em = eanTag.Match(l);
					if (em.Success)
					{
						// No field for EAN in model, keep in ProductCode concatenation if absent
						if (string.IsNullOrWhiteSpace(current.ProductCode))
							current.ProductCode = em.Groups[1].Value;
						continue;
					}

					// Description continuation
					var trimmed = l.Trim();
					if (!string.IsNullOrWhiteSpace(trimmed))
					{
						current.Product = AppendWithSpace(current.Product, CleanSpaces(trimmed));
					}
				}
			}

			if (current != null) results.Add(current);
			return results;
		}

		private static void ExtractInline(string text, DocumentLine target)
		{
			// try quantity + unit
			if (TryExtractQtyUnit(text, out _, out _, out var qty, out var unit))
			{
				target.Quantity = qty;
				target.Unit = unit;
			}

			// numeric tokens: last as total, previous as unit price
			var nums = NumberToken.Matches(text).Cast<Match>().Select(m => m.Value).ToList();
			if (nums.Count > 0) target.TotalValue = ParseDecimal(nums.Last());
			if (nums.Count >= 2) target.UnitPrice = ParseDecimal(nums[nums.Count - 2]);

			// initial product chunk (before last two numeric tokens heuristically)
			target.Product = CleanSpaces(text);
		}

		private static string RemoveParsedSegments(string text, DocumentLine t)
		{
			// This is a simple heuristic: keep as is, already used as Product.
			return string.Empty;
		}

		private static string CleanSpaces(string s)
		{
			return MultiSpace.Replace(s, " ").Trim();
		}

		private static string AppendWithSpace(string a, string b)
		{
			if (string.IsNullOrWhiteSpace(a)) return b;
			if (string.IsNullOrWhiteSpace(b)) return a;
			return a.TrimEnd() + " " + b.Trim();
		}

		private static string TrimRightNumbers(string s)
		{
			var idx = s.Length;
			foreach (Match m in NumberToken.Matches(s))
			{
				if (m.Index + m.Length == s.Length) { idx = m.Index; break; }
			}
			return s.Substring(0, Math.Max(0, idx)).TrimEnd();
		}

		private static string RemoveAt(string s, int index, int length)
		{
			if (index < 0 || index + length > s.Length) return s;
			return (s.Substring(0, index) + s.Substring(index + length)).Trim();
		}

		private static bool TryExtractQtyUnit(string raw, out int index, out int length, out decimal qty, out string? unit)
		{
			// Remove parenthetical content to avoid catching numbers like (360)
			var text = Regex.Replace(raw, @"\([^\)]*\)", "");
			var matches = QtyWithUnit.Matches(text).Cast<Match>().ToList();
			index = -1; length = 0; qty = 0m; unit = null;
			if (matches.Count == 0) return false;

			// Prefer piece-like units if present; else take the rightmost match
			var pieceUnits = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "st", "stk", "pcs", "pc", "pak", "ea", "unit", "szt", "szt." };

			// Keep only matches that have a piece-like unit
			var pieceMatches = matches.Where(m =>
			{
				var u = m.Groups.Count > 2 ? (m.Groups[2].Value ?? string.Empty) : string.Empty;
				u = u.Trim().TrimEnd('.');
				return pieceUnits.Contains(u);
			}).ToList();

			if (pieceMatches.Count == 0) return false; // do not accept KG/M/L as quantity for comparison

			// Take the leftmost piece-unit match (actual ordered quantity, not '/1 ST' from pricing columns)
			var best = pieceMatches.First();

			index = best.Index; length = best.Length;
			qty = ParseDecimal(best.Groups[1].Value);
			unit = best.Groups.Count > 2 ? best.Groups[2].Value : null;
			return qty != 0m;
		}

		private static decimal ParseDecimal(string token)
		{
			if (string.IsNullOrWhiteSpace(token)) return 0m;
			var cleaned = token.Replace("\u00A0", " ").Replace(" ", "").Replace(",", ".");
			if (decimal.TryParse(cleaned, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var d)) return d;
			return 0m;
		}

		private static int SafeParseInt(string s)
			=> int.TryParse(s, out var v) ? v : 0;
	}
}


