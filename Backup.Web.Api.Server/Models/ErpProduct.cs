using System;
using System.Collections.Generic;

namespace Backup.Web.Api.Server.Models
{
    public class ErpProduct
    {
        public int Id { get; set; }
        public string ErpProductId { get; set; } = string.Empty;
        public string? Name { get; set; }
        public string? Name2 { get; set; }
        public string? Reference { get; set; }
        public string? Ean { get; set; }
        public string? Brand { get; set; }
        public string? Manufacturer { get; set; }
        public string? Model { get; set; }
        public string? Comment { get; set; }
        public string? Link { get; set; }
        public string? PicName { get; set; }

        public decimal? PriceHT { get; set; }
        public decimal? UnitPrice { get; set; }
        public decimal? CPrice { get; set; }
        public decimal? RPrice { get; set; }
        public bool VatIncluded { get; set; }
        public decimal? TypeVatPerc { get; set; }

        public decimal? DiscountPerc { get; set; }
        public decimal? DiscountPrice { get; set; }
        public decimal? ProductDiscountPerc { get; set; }
        public decimal? TypeDiscountPerc { get; set; }

        public bool PromoActive { get; set; }
        public decimal? PromoPrice { get; set; }
        public DateTime? PromoStartDate { get; set; }
        public DateTime? PromoEndDate { get; set; }

        public decimal? StockQuantity { get; set; }
        public DateTime? StockDate { get; set; }
        public decimal? Quantity { get; set; }
        public string? PerUnit { get; set; }
        public string? PieceID { get; set; }

        public decimal? Weight { get; set; }
        public decimal? Height { get; set; }
        public decimal? Width { get; set; }
        public decimal? Depth { get; set; }

        public string? MainTypeID { get; set; }
        public string? MainTypeName { get; set; }
        public string? MainSubTypeID { get; set; }
        public string? MainSubTypeName { get; set; }
        public string? TypeID { get; set; }
        public string? TypeName { get; set; }
        public string? SubTypeID { get; set; }
        public string? SubTypeName { get; set; }
        public string? SubProductID { get; set; }

        public string? Label { get; set; }
        public string? ColorCode { get; set; }
        public bool? Archived { get; set; }

        /// <summary>Excel | Erp | Merged</summary>
        public string? DataSource { get; set; }
        /// <summary>Nom du fichier Excel source (si importé).</summary>
        public string? SourceFile { get; set; }
        /// <summary>True si la fiche a été créée/mise à jour depuis Excel (champs Excel protégés).</summary>
        public bool FromExcel { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastSyncAt { get; set; }

        public ICollection<ErpProductChangeLog> ChangeLogs { get; set; } = new List<ErpProductChangeLog>();
    }
}
