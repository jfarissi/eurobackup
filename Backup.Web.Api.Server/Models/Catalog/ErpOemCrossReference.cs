using System;

namespace Backup.Web.Api.Server.Models.Catalog
{
    /// <summary>Cross-référence OEM (plusieurs numéros pour une même pièce).</summary>
    public class ErpOemCrossReference
    {
        public Guid Id { get; set; }
        public int ProductId { get; set; }
        public string OemNumber { get; set; } = string.Empty;
        public string? Brand { get; set; }
        public bool IsOriginal { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ErpProduct? Product { get; set; }
    }
}
