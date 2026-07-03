using System;

namespace Backup.Web.Api.Server.Models
{
	public class DocumentRelation
	{
		public int Id { get; set; }
		public int InvoiceId { get; set; }
		public int DeliveryId { get; set; }
		public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
		/// <summary>
		/// Date à laquelle ce BL a été utilisé pour alimenter le stock.
		/// Null si le BL n'a pas encore été utilisé pour le stock.
		/// </summary>
		public DateTime? StockUpdatedAt { get; set; }
	}
}

