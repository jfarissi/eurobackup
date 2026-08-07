using System;

namespace Backup.Web.Api.Server.Models.Entities.SaaS
{
    /// <summary>Module métier activé pour une société (core, auto_parts, erp_catalog_sync…).</summary>
    public class CompanyModule
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string CompanyId { get; set; } = string.Empty;
        public Company? Company { get; set; }
        public string ModuleCode { get; set; } = string.Empty;
        public string ModuleName { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        /// <summary>Config non-secrète (fréquence sync, TVA…). Pas de clés API.</summary>
        public string? ConfigJson { get; set; }
        public DateTime ActivatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ExpiresAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}
