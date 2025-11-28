using System.Collections.Generic;

namespace Backup.Web.Api.Server.Services.Documents.Parsing
{
	public class DocumentParserConfig
	{
		public HashSet<string> PieceUnits { get; } = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
		{ "st","stk","stuk","stuks","pcs","pc","pak","pkg","ea","unit","szt","szt.","set","ud","uds","adet","adet.","op","doos","doz","bx","box","karton","ctn" };

		public string[] DeliveryKeywords { get; } = new[]
		{
			"leveringsbevestiging","verzendpunt","shipping point","bon de livraison","delivery note","lieferschein","albarán"
		};

		public string[] InvoiceKeywords { get; } = new[]
		{
			"factuur","invoice","facture","rechnung","fatura","faktura"
		};
	}
}


