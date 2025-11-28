using System.Collections.Generic;
using Backup.Web.Api.Server.Models;

namespace Backup.Web.Api.Server.Services.Documents.Parsing
{
	public interface IDocumentParser
	{
		bool CanParse(DocumentLanguage language, string text);
		List<DocumentLine> Parse(DocumentLanguage language, string text);
	}
}


