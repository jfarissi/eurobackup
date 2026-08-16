using System;
using Backup.Web.Api.Server.Services.Tenancy;

namespace Backup.Web.Api.Server.Models.Catalog
{
    /// <summary>
    /// File d'attente des K-Types identifiés (VIN / RapidAPI) mais absents du catalogue local.
    /// Alimente les sync Python ciblés (sync_rapidapi_morocco.py --ktype).
    /// </summary>
    public class ErpKTypeEnrichmentQueue : IHasCompanyId
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string? CompanyId { get; set; }
        public string KType { get; set; } = string.Empty;
        public string? Vin { get; set; }
        public string? Make { get; set; }
        public string? Model { get; set; }
        public int? Year { get; set; }
        public string? EngineCode { get; set; }
        /// <summary>VinLookup | PlateScan | VinLink | Manual</summary>
        public string Source { get; set; } = "VinLookup";
        /// <summary>Pending | Syncing | Done | Failed</summary>
        public string Status { get; set; } = "Pending";
        public int HitCount { get; set; } = 1;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastRequestedAt { get; set; }
        public DateTime? SyncedAt { get; set; }
    }
}
