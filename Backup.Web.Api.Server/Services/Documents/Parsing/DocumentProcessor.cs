using System.Collections.Generic;
using System.Linq;
using Backup.Web.Api.Server.Models;

namespace Backup.Web.Api.Server.Services.Documents.Parsing
{
	public class DocumentProcessor
	{
		private readonly LanguageDetector detector;
		private readonly IEnumerable<IDocumentParser> parsers;

		public DocumentProcessor(LanguageDetector detector, IEnumerable<IDocumentParser> parsers)
		{
			this.detector = detector;
			this.parsers = parsers;
		}

		public List<DocumentLine> Parse(string text)
		{
			var lang = detector.Detect(text);
			foreach (var p in parsers)
			{
				if (p.CanParse(lang, text))
				{
					var res = p.Parse(lang, text) ?? new List<DocumentLine>();
					if (res.Count > 0) return res;
				}
			}
			return new List<DocumentLine>();
		}
	}
}


