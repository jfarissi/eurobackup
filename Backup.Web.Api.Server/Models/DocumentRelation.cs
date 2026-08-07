using System;
using Backup.Web.Api.Server.Services.Audit;

namespace Backup.Web.Api.Server.Models
{
	public class DocumentRelation : IHasAuditTrail
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

		public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
		public string? CreatedBy { get; set; }
		public string? UpdatedBy { get; set; }
	}
}

