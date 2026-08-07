using System;

namespace Backup.Web.Api.Server.Models.Catalog
{
    /// <summary>Variante produit (couleur, taille, etc.) — optionnelle ; Euro Brico n'en utilise pas.</summary>
    public class ErpProductVariant
    {
        public Guid Id { get; set; }
        /// <summary>FK vers <see cref="ErpProduct.Id"/>.</summary>
        public int ProductId { get; set; }
        public string Sku { get; set; } = string.Empty;
        public string? Barcode { get; set; }
        public decimal? CostPrice { get; set; }
        public decimal? PriceOverride { get; set; }
        public decimal StockQuantity { get; set; }
        /// <summary>JSON libre, ex. {"Color":"Red","Size":"M"}.</summary>
        public string AttributesJson { get; set; } = "{}";
        public decimal? Weight { get; set; }
        public decimal? Length { get; set; }
        public decimal? Width { get; set; }
        public decimal? Height { get; set; }
        public bool IsActive { get; set; } = true;
        public string? CreatedBy { get; set; }
        public string? UpdatedBy { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public ErpProduct? Product { get; set; }
    }
}
