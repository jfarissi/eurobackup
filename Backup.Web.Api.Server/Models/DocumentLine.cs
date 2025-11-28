using System;

namespace Backup.Web.Api.Server.Models
{
	public class DocumentLine
	{
		public int Id { get; set; }
		public int DocumentId { get; set; }

		public int LineNumber { get; set; }

		public string Product { get; set; } = string.Empty;
		public string? ProductCode { get; set; }
		public string? Ean { get; set; }

		public decimal Quantity { get; set; }
		public string? Unit { get; set; }

		public decimal UnitPrice { get; set; }
		public decimal TotalValue { get; set; }

		// Raw source line(s) for audit/debug
		public string? RawLine { get; set; }
	}
}


