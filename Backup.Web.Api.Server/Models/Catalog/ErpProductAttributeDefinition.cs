using System;

namespace Backup.Web.Api.Server.Models.Catalog
{
    /// <summary>Définition d'attribut catalogue (par société).</summary>
    public class ErpProductAttributeDefinition
    {
        public Guid Id { get; set; }
        public string CompanyId { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public string? CreatedBy { get; set; }
        public string? UpdatedBy { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}
