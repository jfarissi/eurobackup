namespace Backup.Web.Api.Server.Services.Email
{
    public sealed class EmailDocumentPayload
    {
        public string TemplateCode { get; set; } = string.Empty;
        public string DocumentType { get; set; } = string.Empty;
        public int DocumentId { get; set; }
        public string DocumentNumber { get; set; } = string.Empty;
        public string? RecipientEmail { get; set; }
        public string? RecipientName { get; set; }
        public string Subject { get; set; } = string.Empty;
        public string BodyHtml { get; set; } = string.Empty;
        public string? AttachmentFileName { get; set; }
        public byte[]? AttachmentBytes { get; set; }
        public Dictionary<string, string> Variables { get; set; } = new();
    }

    public sealed class SendEmailRequest
    {
        public string DocumentType { get; set; } = string.Empty;
        public int DocumentId { get; set; }
        public string? TemplateCode { get; set; }
        public string? ToEmail { get; set; }
        public string? CcEmails { get; set; }
        public string? ReplyTo { get; set; }
        public string? Subject { get; set; }
        public string? BodyHtml { get; set; }
        public DateTime? ScheduledAt { get; set; }
        public bool SendNow { get; set; } = true;
    }

    public sealed class QueueTemplateEmailRequest
    {
        public string TemplateCode { get; set; } = string.Empty;
        public string ToEmail { get; set; } = string.Empty;
        public string? CcEmails { get; set; }
        public Dictionary<string, string>? Variables { get; set; }
        public string? DocumentType { get; set; }
        public int? DocumentId { get; set; }
        public string? DocumentNumber { get; set; }
        public byte[]? AttachmentBytes { get; set; }
        public string? AttachmentFileName { get; set; }
        public bool SendNow { get; set; } = true;
    }

    public sealed class EmailPreviewDto
    {
        public string TemplateCode { get; set; } = string.Empty;
        public string DocumentType { get; set; } = string.Empty;
        public int DocumentId { get; set; }
        public string DocumentNumber { get; set; } = string.Empty;
        public string? ToEmail { get; set; }
        public string? RecipientName { get; set; }
        public string Subject { get; set; } = string.Empty;
        public string BodyHtml { get; set; } = string.Empty;
        public string? AttachmentFileName { get; set; }
        public long? AttachmentSize { get; set; }
        public bool HasValidRecipient { get; set; }
    }
}
