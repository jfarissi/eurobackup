using System;
using System.Collections.Generic;
using Backup.Web.Api.Server.Services.Audit;

namespace Backup.Web.Api.Server.Models
{
    /// <summary>
    /// Marque dérivée de ErpProducts.Brand (et alignable plus tard avec EuroBrico ErpBrands).
    /// </summary>
    public class ErpBrand : IHasAuditTrail
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string? LogoUrl { get; set; }
        public string? WebsiteUrl { get; set; }
        public string? Description { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public string? CreatedBy { get; set; }
        public string? UpdatedBy { get; set; }

        public ICollection<ErpProduct> Products { get; set; } = new List<ErpProduct>();
    }
}
