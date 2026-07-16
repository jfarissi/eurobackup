using System;

namespace Backup.Web.Api.Server.Models
{
    public class ErpProductChangeLog
    {
        public int Id { get; set; }
        public int ErpProductId { get; set; }
        public string ChangeType { get; set; } = string.Empty;
        public string FieldName { get; set; } = string.Empty;
        public string? OldValue { get; set; }
        public string? NewValue { get; set; }
        public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
        public string? SyncJobId { get; set; }
        public bool IsRead { get; set; }

        public ErpProduct ErpProduct { get; set; } = null!;
    }
}
