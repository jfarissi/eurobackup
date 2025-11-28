using System;

namespace Backup.Web.Api.Server.Models
{
	public class StockItem
	{
		public int Id { get; set; }
		public string ProductKey { get; set; } = string.Empty; // Prefer ProductCode, else Product name
		public decimal QuantityOnHand { get; set; }
		public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
	}
}


