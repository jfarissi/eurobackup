using System.Text.Json.Serialization;

namespace Backup.Web.Api.Server.Services.ErpSync
{
    public class ErpCatalogItemDto
    {
        [JsonPropertyName("ID")]
        public string? Id { get; set; }

        [JsonPropertyName("Name")]
        public string? Name { get; set; }

        [JsonPropertyName("PicName")]
        public string? PicName { get; set; }
    }

    public class ErpProductDto
    {
        [JsonPropertyName("ID")]
        public string? Id { get; set; }

        [JsonPropertyName("Name")]
        public string? Name { get; set; }

        [JsonPropertyName("Name2")]
        public string? Name2 { get; set; }

        [JsonPropertyName("Reference")]
        public string? Reference { get; set; }

        [JsonPropertyName("EAN")]
        public string? Ean { get; set; }

        [JsonPropertyName("Brand")]
        public string? Brand { get; set; }

        [JsonPropertyName("Manufacturer")]
        public string? Manufacturer { get; set; }

        [JsonPropertyName("Model")]
        public string? Model { get; set; }

        [JsonPropertyName("Comment")]
        public string? Comment { get; set; }

        [JsonPropertyName("Link")]
        public string? Link { get; set; }

        [JsonPropertyName("PicName")]
        public string? PicName { get; set; }

        [JsonPropertyName("PriceHT")]
        public string? PriceHT { get; set; }

        [JsonPropertyName("UnitPrice")]
        public string? UnitPrice { get; set; }

        [JsonPropertyName("CPrice")]
        public string? CPrice { get; set; }

        [JsonPropertyName("RPrice")]
        public string? RPrice { get; set; }

        [JsonPropertyName("VatIncluded")]
        public string? VatIncluded { get; set; }

        [JsonPropertyName("TypeVatPerc")]
        public string? TypeVatPerc { get; set; }

        [JsonPropertyName("DiscountPerc")]
        public string? DiscountPerc { get; set; }

        [JsonPropertyName("DiscountPrice")]
        public string? DiscountPrice { get; set; }

        [JsonPropertyName("ProductDiscountPerc")]
        public string? ProductDiscountPerc { get; set; }

        [JsonPropertyName("TypeDiscountPerc")]
        public string? TypeDiscountPerc { get; set; }

        [JsonPropertyName("PromoActive")]
        public string? PromoActive { get; set; }

        [JsonPropertyName("PromoPrice")]
        public string? PromoPrice { get; set; }

        [JsonPropertyName("PromoStartDate")]
        public string? PromoStartDate { get; set; }

        [JsonPropertyName("PromoEndDate")]
        public string? PromoEndDate { get; set; }

        [JsonPropertyName("StockQuantity")]
        public string? StockQuantity { get; set; }

        [JsonPropertyName("StockDate")]
        public string? StockDate { get; set; }

        [JsonPropertyName("Quantity")]
        public string? Quantity { get; set; }

        [JsonPropertyName("PerUnit")]
        public string? PerUnit { get; set; }

        [JsonPropertyName("PieceID")]
        public string? PieceID { get; set; }

        [JsonPropertyName("Weight")]
        public string? Weight { get; set; }

        [JsonPropertyName("Height")]
        public string? Height { get; set; }

        [JsonPropertyName("Width")]
        public string? Width { get; set; }

        [JsonPropertyName("Depth")]
        public string? Depth { get; set; }

        [JsonPropertyName("MainTypeID")]
        public string? MainTypeID { get; set; }

        [JsonPropertyName("MainTypeName")]
        public string? MainTypeName { get; set; }

        [JsonPropertyName("MainSubTypeID")]
        public string? MainSubTypeID { get; set; }

        [JsonPropertyName("MainSubTypeName")]
        public string? MainSubTypeName { get; set; }

        [JsonPropertyName("TypeID")]
        public string? TypeID { get; set; }

        [JsonPropertyName("TypeName")]
        public string? TypeName { get; set; }

        [JsonPropertyName("SubTypeID")]
        public string? SubTypeID { get; set; }

        [JsonPropertyName("SubTypeName")]
        public string? SubTypeName { get; set; }

        [JsonPropertyName("SubProductID")]
        public string? SubProductID { get; set; }

        [JsonPropertyName("Label")]
        public string? Label { get; set; }

        [JsonPropertyName("ColorCode")]
        public string? ColorCode { get; set; }

        [JsonPropertyName("Achived")]
        public string? Achived { get; set; }

        [JsonPropertyName("Archived")]
        public string? Archived { get; set; }
    }
}
