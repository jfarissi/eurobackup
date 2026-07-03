using System;

namespace Backup.Web.Api.Server.Models
{
    /// <summary>
    /// Représente un ajustement manuel de quantité pour une ligne de bon de livraison.
    /// Permet au vérificateur de saisir la quantité réelle lors de la vérification.
    /// </summary>
    public class DeliveryLineAdjustment
    {
        public int Id { get; set; }
        
        /// <summary>
        /// ID du bon de livraison
        /// </summary>
        public int DeliveryId { get; set; }
        
        /// <summary>
        /// ID de la facture associée
        /// </summary>
        public int InvoiceId { get; set; }
        
        /// <summary>
        /// ID de la ligne du document (DocumentLine.Id)
        /// </summary>
        public int? DocumentLineId { get; set; }
        
        /// <summary>
        /// Clé produit pour identifier la ligne (ProductCode, EAN, ou nom normalisé)
        /// </summary>
        public string ProductKey { get; set; } = string.Empty;
        
        /// <summary>
        /// Quantité du BL (quantité originale)
        /// </summary>
        public decimal DeliveryQuantity { get; set; }
        
        /// <summary>
        /// Quantité réelle saisie par le vérificateur
        /// </summary>
        public decimal? ActualQuantity { get; set; }
        
        /// <summary>
        /// Indique si l'ajustement a été validé
        /// </summary>
        public bool IsValidated { get; set; }
        
        /// <summary>
        /// Date de création de l'ajustement
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        /// <summary>
        /// Date de validation de l'ajustement
        /// </summary>
        public DateTime? ValidatedAt { get; set; }
        
        /// <summary>
        /// Nom de l'utilisateur qui a créé l'ajustement
        /// </summary>
        public string? CreatedBy { get; set; }
        
        /// <summary>
        /// Nom de l'utilisateur qui a validé l'ajustement
        /// </summary>
        public string? ValidatedBy { get; set; }
    }
}

