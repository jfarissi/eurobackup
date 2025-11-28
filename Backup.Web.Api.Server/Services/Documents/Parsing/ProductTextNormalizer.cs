using System.Text;
using System.Text.RegularExpressions;
using System.Globalization;

namespace Backup.Web.Api.Server.Services.Documents.Parsing
{
	public static class ProductTextNormalizer
	{
		public static string Normalize(string input)
		{
			if (string.IsNullOrWhiteSpace(input))
				return string.Empty;

			string text = input.ToLowerInvariant();

			// Remove accents
			text = RemoveDiacritics(text);

			// Remove content in parentheses/brackets/braces
			text = Regex.Replace(text, @"[\(\[\{].*?[\)\]\}]", " ");

			// Remove units and sizes
			text = Regex.Replace(text,
				@"\b(\d+[,\.]?\d*)\s?(kg|g|l|ml|cm|mm|m2|m3|szt|op|pcs|st|mb)\b",
				" ",
				RegexOptions.IgnoreCase);

			// Remove product-like codes (abc123, ker00022, 002327, #40pcs, etc.)
			text = Regex.Replace(text, @"\b[a-z]{2,4}\d{3,6}\b", " ");
			text = Regex.Replace(text, @"#\d+\w*", " ");

			// Remove isolated numbers (06, 25, 3, 14, 216…)
			text = Regex.Replace(text, @"\b\d{1,4}\b", " ");

			// Remove separators/symbols
			text = Regex.Replace(text, @"[-_/\\]", " ");

			// Keep only letters and spaces
			text = Regex.Replace(text, @"[^a-z\s]", " ");

			// Collapse spaces
			text = Regex.Replace(text, @"\s+", " ").Trim();

			return text;
		}

		private static string RemoveDiacritics(string text)
		{
			var normalized = text.Normalize(NormalizationForm.FormD);
			var builder = new StringBuilder();

			foreach (var c in normalized)
			{
				var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
				if (unicodeCategory != UnicodeCategory.NonSpacingMark)
					builder.Append(c);
			}

			return builder.ToString().Normalize(NormalizationForm.FormC);
		}
	}
}


