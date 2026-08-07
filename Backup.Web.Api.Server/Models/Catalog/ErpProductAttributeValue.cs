using System;

namespace Backup.Web.Api.Server.Models.Catalog
{
    /// <summary>Valeur d'attribut au niveau produit (pas variante).</summary>
    public class ErpProductAttributeValue
    {
        public Guid Id { get; set; }
        public int ProductId { get; set; }
        public Guid AttributeId { get; set; }
        public string Value { get; set; } = string.Empty;
        public string? CreatedBy { get; set; }
        public string? UpdatedBy { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public ErpProduct? Product { get; set; }
        public ErpProductAttributeDefinition? Attribute { get; set; }
    }
}
