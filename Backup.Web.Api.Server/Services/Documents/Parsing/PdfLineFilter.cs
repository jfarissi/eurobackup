using System.Linq;
using System.Text.RegularExpressions;

namespace Backup.Web.Api.Server.Services.Documents.Parsing
{
	public static class PdfLineFilter
	{
		private static readonly string[] Exclusions = new[] {
			"iban","bic","bank","www.","knauf.com","@","fax","tel","algemene",
			"verkoopsvoorwaarden","blad","factuur","invoice","datum","boekingsdatum",
			"euro","totaal","btw","gewicht","betalingsvoorwaarden","leveringscondities",
			"destination","rpm","scomm","pagina","rue du parc industriel"
		};

		public static bool IsProductLine(string line)
		{
			if (string.IsNullOrWhiteSpace(line)) return false;
			var l = line.Trim().ToLowerInvariant();

			if (Exclusions.Any(e => l.Contains(e))) return false;
			if (l.Length < 8) return false;
			if (l.All(c => c == '_' || c == '-')) return false;

			// Must contain a feature code like "(164)"
			if (!Regex.IsMatch(l, @"\(\d+\)")) return false;

			// Must contain at least one isolated integer (quantity candidate)
			if (!Regex.IsMatch(l, @"\b\d{1,3}\b")) return false;

			return true;
		}
	}
}


