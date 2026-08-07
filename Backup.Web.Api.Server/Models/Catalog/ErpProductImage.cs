using System;

namespace Backup.Web.Api.Server.Models.Catalog
{
    /// <summary>Image catalogue liée à un <see cref="ErpProduct"/> (complète PicName, ne le remplace pas).</summary>
    public class ErpProductImage
    {
        public Guid Id { get; set; }
        public int ProductId { get; set; }
        public string Url { get; set; } = string.Empty;
        public string AltText { get; set; } = string.Empty;
        public bool IsMain { get; set; }
        public int SortOrder { get; set; }
        public string? CreatedBy { get; set; }
        public string? UpdatedBy { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public ErpProduct? Product { get; set; }
    }
}
