using System;

namespace Backup.Web.Api.Server.Models
{
	public class StockItem
	{
		public int Id { get; set; }
		public string ProductKey { get; set; } = string.Empty; // Prefer ProductCode, else Product name
		public decimal QuantityOnHand { get; set; }
		public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
		/// <summary>
		/// ID du dernier BL qui a mis à jour ce produit.
		/// Permet une traçabilité rapide.
		/// </summary>
		public int? LastDeliveryId { get; set; }
		/// <summary>
		/// Fournisseur du produit (extrait du BL)
		/// </summary>
		public string? Supplier { get; set; }
		/// <summary>
		/// Description/libellé du produit
		/// </summary>
		public string? Description { get; set; }
		/// <summary>
		/// Unité du produit (ST, KG, PC, etc.)
		/// </summary>
		public string? Unit { get; set; }
	}
}


