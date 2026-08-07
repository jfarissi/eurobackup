using System;
using Backup.Web.Api.Server.Services.Audit;

namespace Backup.Web.Api.Server.Models
{
	public class StockItem : Backup.Web.Api.Server.Services.Tenancy.IHasCompanyId, IHasAuditTrail
	{
		public int Id { get; set; }
		public string ProductKey { get; set; } = string.Empty; // Prefer ProductCode, else Product name
		public decimal QuantityOnHand { get; set; }
		/// <summary>P4 — quantité réservée par commandes confirmées (ATP = OnHand − Reserved).</summary>
		public decimal ReservedQuantity { get; set; }
		/// <summary>P4 — seuil de réappro auto (0 = désactivé).</summary>
		public decimal MinStock { get; set; }
		/// <summary>CMUP / CMP — coût moyen unitaire pondéré (société). Mis à jour à chaque entrée valorisée.</summary>
		public decimal AverageCost { get; set; }
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
		public string? CompanyId { get; set; }

		public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
		public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
		public string? CreatedBy { get; set; }
		public string? UpdatedBy { get; set; }
	}
}


