using System;

namespace Backup.Web.Api.Server.Models
{
	public class DocumentRelation
	{
		public int Id { get; set; }
		public int InvoiceId { get; set; }
		public int DeliveryId { get; set; }
		public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
	}
}

