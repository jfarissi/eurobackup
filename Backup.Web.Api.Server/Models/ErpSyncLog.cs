using System;

namespace Backup.Web.Api.Server.Models
{
    public class ErpSyncLog
    {
        public int Id { get; set; }
        public string JobId { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime StartedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }
        public int TotalProducts { get; set; }
        /// <summary>Nombre de produits déjà traités dans le job en cours.</summary>
        public int ProcessedProducts { get; set; }
        public int UpdatedProducts { get; set; }
        public int NewProducts { get; set; }
        public int FailedProducts { get; set; }
        public int TotalChanges { get; set; }
        public string? ErrorMessage { get; set; }
        public string? Details { get; set; }
    }
}
