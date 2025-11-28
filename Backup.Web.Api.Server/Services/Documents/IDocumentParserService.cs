using System.Collections.Generic;
using Backup.Web.Api.Server.Models;

namespace Backup.Web.Api.Server.Services.Documents
{
	public interface IDocumentParserService
	{
		/// <summary>
		/// Parse structured TXT content into document lines (without DocumentId set).
		/// </summary>
		IReadOnlyList<DocumentLine> Parse(string txt);
	}
}


