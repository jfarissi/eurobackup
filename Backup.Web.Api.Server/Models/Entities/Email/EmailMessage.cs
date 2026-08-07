using System;
using Backup.Web.Api.Server.Services.Tenancy;

namespace Backup.Web.Api.Server.Models.Entities.Email
{
    /// <summary>File d'attente + journal d'audit des envois (RG-EM11, RG-EM12).</summary>
    public class EmailMessage : IHasCompanyId
    {
        public long Id { get; set; }
        public string CompanyId { get; set; } = string.Empty;
        public string TrackingId { get; set; } = Guid.NewGuid().ToString("N");
        public string TemplateCode { get; set; } = string.Empty;
        public string? DocumentType { get; set; }
        public int? DocumentId { get; set; }
        public string? DocumentNumber { get; set; }
        public string ToEmail { get; set; } = string.Empty;
        public string? CcEmails { get; set; }
        public string? ReplyTo { get; set; }
        public string Subject { get; set; } = string.Empty;
        public string BodyHtml { get; set; } = string.Empty;
        public string? BodyText { get; set; }
        public string? AttachmentFileName { get; set; }
        public byte[]? AttachmentBytes { get; set; }
        /// <summary>Pending | Scheduled | Sent | Failed | Cancelled | WaitingForEmail</summary>
        public string Status { get; set; } = EmailStatuses.Pending;
        public DateTime? ScheduledAt { get; set; }
        public DateTime? SentAt { get; set; }
        public int RetryCount { get; set; }
        public string? LastError { get; set; }
        public string CreatedBy { get; set; } = "System";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public static class EmailStatuses
    {
        public const string Pending = "Pending";
        public const string Scheduled = "Scheduled";
        public const string Sent = "Sent";
        public const string Failed = "Failed";
        public const string Cancelled = "Cancelled";
        public const string WaitingForEmail = "WaitingForEmail";
    }
}
