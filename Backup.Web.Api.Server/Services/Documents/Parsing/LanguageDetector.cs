using System.Linq;

namespace Backup.Web.Api.Server.Services.Documents.Parsing
{
	public class LanguageDetector
	{
		private readonly DocumentParserConfig config;
		public LanguageDetector(DocumentParserConfig config) => this.config = config;

		public DocumentLanguage Detect(string text)
		{
			if (string.IsNullOrWhiteSpace(text)) return DocumentLanguage.Unknown;
			var lower = text.ToLowerInvariant();

			// fast keyword heuristics
			if (lower.Contains("bon de livraison") || lower.Contains("facture")) return DocumentLanguage.Fr;
			if (lower.Contains("leveringsbevestiging") || lower.Contains("factuur")) return DocumentLanguage.Nl;
			if (lower.Contains("delivery note") || lower.Contains("invoice")) return DocumentLanguage.En;
			if (lower.Contains("lieferschein") || lower.Contains("rechnung")) return DocumentLanguage.De;
			if (lower.Contains("albarán") || lower.Contains("factura")) return DocumentLanguage.Es;
			if (lower.Contains("fatura")) return DocumentLanguage.Tr;
			if (lower.Contains("faktura")) return DocumentLanguage.Pl;

			// fallback: check configured keywords
			if (config.DeliveryKeywords.Any(k => lower.Contains(k))) return DocumentLanguage.En;
			if (config.InvoiceKeywords.Any(k => lower.Contains(k))) return DocumentLanguage.En;

			return DocumentLanguage.Unknown;
		}
	}
}


