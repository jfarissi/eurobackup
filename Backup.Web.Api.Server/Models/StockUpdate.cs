using System;

namespace Backup.Web.Api.Server.Models
{
	/// <summary>
	/// Enregistre chaque mise à jour de stock avec le BL qui l'a causée.
	/// Permet la traçabilité complète des mises à jour de stock.
	/// </summary>
	public class StockUpdate
	{
		public int Id { get; set; }
		public string ProductKey { get; set; } = string.Empty;
		public decimal QuantityDelta { get; set; } // Quantité ajoutée (peut être négative)
		public decimal QuantityAfter { get; set; } // Quantité totale après cette mise à jour
		public int DeliveryId { get; set; } // ID du BL qui a causé cette mise à jour
		public int? InvoiceId { get; set; } // ID de la facture associée (optionnel)
		public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
	}
}

