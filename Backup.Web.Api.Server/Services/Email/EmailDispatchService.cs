using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Models.Entities.Email;
using Backup.Web.Api.Server.Services.Sales;
using Backup.Web.Api.Server.Services.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Backup.Web.Api.Server.Services.Email
{
    public interface IEmailDispatchService
    {
        Task<EmailPreviewDto?> PreviewAsync(string? companyId, string documentType, int documentId, string? templateCode = null);
        Task<EmailMessage> QueueAsync(string? companyId, SendEmailRequest request, string? actor);
        Task<EmailMessage> QueueTemplateAsync(string? companyId, QueueTemplateEmailRequest request, string? actor);
        Task<EmailMessage?> SendQueuedAsync(long messageId, CancellationToken ct = default);
        Task<int> ProcessPendingAsync(int batchSize = 20, CancellationToken ct = default);
        Task<bool> TestConnectionAsync(string companyId, CancellationToken ct = default);
        Task TestConnectionWithSettingsAsync(CompanyEmailSettings settings, CancellationToken ct = default);
    }

    public class EmailDispatchService : IEmailDispatchService
    {
        private readonly IStorageBroker storage;
        private readonly IEmailDocumentService documentService;
        private readonly ISmtpEmailSender smtpSender;

        public EmailDispatchService(IStorageBroker storage, IEmailDocumentService documentService, ISmtpEmailSender smtpSender)
        {
            this.storage = storage;
            this.documentService = documentService;
            this.smtpSender = smtpSender;
        }

        public async Task<EmailPreviewDto?> PreviewAsync(string? companyId, string documentType, int documentId, string? templateCode = null)
        {
            var payload = await this.documentService.BuildAsync(companyId, documentType, documentId, templateCode);
            if (payload == null) return null;

            var to = EmailAddressValidator.Normalize(payload.RecipientEmail);
            return new EmailPreviewDto
            {
                TemplateCode = payload.TemplateCode,
                DocumentType = payload.DocumentType,
                DocumentId = payload.DocumentId,
                DocumentNumber = payload.DocumentNumber,
                ToEmail = to,
                RecipientName = payload.RecipientName,
                Subject = payload.Subject,
                BodyHtml = payload.BodyHtml,
                AttachmentFileName = payload.AttachmentFileName,
                AttachmentSize = payload.AttachmentBytes?.LongLength,
                HasValidRecipient = EmailAddressValidator.IsValid(to)
            };
        }

        public async Task<EmailMessage> QueueAsync(string? companyId, SendEmailRequest request, string? actor)
        {
            companyId = await ResolveCompanyIdAsync(companyId);
            var payload = await this.documentService.BuildAsync(companyId, request.DocumentType, request.DocumentId, request.TemplateCode);
            if (payload == null)
                throw new InvalidOperationException("Document introuvable ou type non supporté.");

            var settings = await this.storage.SelectCompanyEmailSettingsByCompanyIdAsync(companyId);
            if (settings == null)
                throw new InvalidOperationException("Paramètres SMTP introuvables. Configurez Admin → Email.");
            if (!settings.Enabled)
                throw new InvalidOperationException("La messagerie est désactivée. Cochez « Messagerie activée » dans Admin → Email, puis Enregistrer.");
            if (string.IsNullOrWhiteSpace(settings.SmtpHost))
                throw new InvalidOperationException("Serveur SMTP manquant. Configurez Admin → Email.");

            var to = EmailAddressValidator.Normalize(request.ToEmail) ?? EmailAddressValidator.Normalize(payload.RecipientEmail);
            if (!EmailAddressValidator.IsValid(to))
            {
                var waiting = new EmailMessage
                {
                    CompanyId = companyId,
                    TemplateCode = payload.TemplateCode,
                    DocumentType = payload.DocumentType,
                    DocumentId = payload.DocumentId,
                    DocumentNumber = payload.DocumentNumber,
                    ToEmail = to ?? string.Empty,
                    Subject = request.Subject ?? payload.Subject,
                    BodyHtml = request.BodyHtml ?? payload.BodyHtml,
                    AttachmentFileName = payload.AttachmentFileName,
                    AttachmentBytes = payload.AttachmentBytes,
                    Status = EmailStatuses.WaitingForEmail,
                    LastError = "Adresse email destinataire invalide ou absente (RG-EM1).",
                    CreatedBy = actor ?? "System"
                };
                return await this.storage.InsertEmailMessageAsync(waiting);
            }

            var ccList = EmailAddressValidator.ParseList(request.CcEmails, 5);
            var attachment = payload.AttachmentBytes;
            if (attachment != null && attachment.Length > settings.MaxAttachmentBytes)
                throw new InvalidOperationException($"Pièce jointe trop volumineuse (max {settings.MaxAttachmentBytes / (1024 * 1024)} Mo).");

            await EnsureRateLimitAsync(companyId, settings.MaxEmailsPerHour);

            var scheduled = request.ScheduledAt.HasValue && request.ScheduledAt.Value > DateTime.UtcNow.AddMinutes(1);
            var message = new EmailMessage
            {
                CompanyId = companyId,
                TemplateCode = payload.TemplateCode,
                DocumentType = payload.DocumentType,
                DocumentId = payload.DocumentId,
                DocumentNumber = payload.DocumentNumber,
                ToEmail = to!,
                CcEmails = ccList.Count > 0 ? string.Join(";", ccList) : null,
                ReplyTo = EmailAddressValidator.Normalize(request.ReplyTo)
                    ?? EmailAddressValidator.Normalize(settings.DefaultReplyTo),
                Subject = request.Subject ?? payload.Subject,
                BodyHtml = request.BodyHtml ?? payload.BodyHtml,
                AttachmentFileName = payload.AttachmentFileName,
                AttachmentBytes = attachment,
                Status = scheduled ? EmailStatuses.Scheduled : EmailStatuses.Pending,
                ScheduledAt = scheduled ? request.ScheduledAt : null,
                CreatedBy = actor ?? "System"
            };

            message = await this.storage.InsertEmailMessageAsync(message);

            if (request.SendNow && !scheduled)
                await SendMessageAsync(message, settings);

            return message;
        }

        public async Task<EmailMessage> QueueTemplateAsync(string? companyId, QueueTemplateEmailRequest request, string? actor)
        {
            companyId = await ResolveCompanyIdAsync(companyId);
            var settings = await this.storage.SelectCompanyEmailSettingsByCompanyIdAsync(companyId);
            if (settings == null)
                throw new InvalidOperationException("Paramètres SMTP introuvables. Configurez Admin → Email.");
            if (!settings.Enabled)
                throw new InvalidOperationException("La messagerie est désactivée. Cochez « Messagerie activée » dans Admin → Email, puis Enregistrer.");

            var to = EmailAddressValidator.Normalize(request.ToEmail);
            if (!EmailAddressValidator.IsValid(to))
                throw new InvalidOperationException("Adresse email destinataire invalide.");

            var vars = request.Variables ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var template = EmailTemplateCatalog.Get(request.TemplateCode);
            var subject = EmailTemplateRenderer.Render(template.SubjectPattern, vars);
            var body = EmailTemplateRenderer.Render(template.BodyHtmlPattern, vars);
            if (!string.IsNullOrWhiteSpace(settings.FooterHtml))
                body += settings.FooterHtml;

            await EnsureRateLimitAsync(companyId, settings.MaxEmailsPerHour);

            var message = new EmailMessage
            {
                CompanyId = companyId,
                TemplateCode = template.Code,
                DocumentType = request.DocumentType,
                DocumentId = request.DocumentId,
                DocumentNumber = request.DocumentNumber,
                ToEmail = to!,
                CcEmails = EmailAddressValidator.ParseList(request.CcEmails, 5).Count > 0
                    ? string.Join(";", EmailAddressValidator.ParseList(request.CcEmails, 5))
                    : null,
                ReplyTo = settings.DefaultReplyTo,
                Subject = subject,
                BodyHtml = body,
                AttachmentFileName = request.AttachmentFileName,
                AttachmentBytes = request.AttachmentBytes,
                Status = EmailStatuses.Pending,
                CreatedBy = actor ?? "System"
            };

            message = await this.storage.InsertEmailMessageAsync(message);
            if (request.SendNow)
                await SendMessageAsync(message, settings);
            return message;
        }

        public async Task<EmailMessage?> SendQueuedAsync(long messageId, CancellationToken ct = default)
        {
            var message = await this.storage.SelectEmailMessageByIdAsync(messageId);
            if (message == null) return null;
            var settings = await this.storage.SelectCompanyEmailSettingsByCompanyIdAsync(message.CompanyId);
            if (settings == null) throw new InvalidOperationException("Paramètres SMTP introuvables.");
            await SendMessageAsync(message, settings, ct);
            return message;
        }

        public async Task<int> ProcessPendingAsync(int batchSize = 20, CancellationToken ct = default)
        {
            var now = DateTime.UtcNow;
            var pending = await this.storage.SelectPendingEmailMessagesAsync(batchSize, now);
            var sent = 0;
            foreach (var msg in pending)
            {
                var settings = await this.storage.SelectCompanyEmailSettingsByCompanyIdAsync(msg.CompanyId);
                if (settings == null || !settings.Enabled) continue;
                try
                {
                    await SendMessageAsync(msg, settings, ct);
                    sent++;
                }
                catch
                {
                    // logged in SendMessageAsync
                }
            }
            return sent;
        }

        public async Task<bool> TestConnectionAsync(string companyId, CancellationToken ct = default)
        {
            var settings = await this.storage.SelectCompanyEmailSettingsByCompanyIdAsync(companyId);
            if (settings == null || string.IsNullOrWhiteSpace(settings.SmtpHost))
                return false;
            await TestConnectionWithSettingsAsync(settings, ct);
            return true;
        }

        public async Task TestConnectionWithSettingsAsync(CompanyEmailSettings settings, CancellationToken ct = default)
        {
            if (settings == null || string.IsNullOrWhiteSpace(settings.SmtpHost))
                throw new InvalidOperationException("Serveur SMTP (SmtpHost) requis.");
            if (settings.SmtpPort <= 0)
                throw new InvalidOperationException("Port SMTP invalide.");

            using var client = new MailKit.Net.Smtp.SmtpClient();
            if (settings.IgnoreSslErrors)
                client.ServerCertificateValidationCallback = (_, _, _, _) => true;

            var socketOptions = ResolveSocketOptions(settings);
            await client.ConnectAsync(settings.SmtpHost.Trim(), settings.SmtpPort, socketOptions, ct);
            if (!string.IsNullOrWhiteSpace(settings.Username))
                await client.AuthenticateAsync(settings.Username, settings.Password ?? string.Empty, ct);
            await client.DisconnectAsync(true, ct);
        }

        private static MailKit.Security.SecureSocketOptions ResolveSocketOptions(CompanyEmailSettings settings)
        {
            if (!settings.UseSsl)
                return MailKit.Security.SecureSocketOptions.None;
            // Port 465 = SSL implicite ; 587/25 = STARTTLS
            if (settings.SmtpPort == 465)
                return MailKit.Security.SecureSocketOptions.SslOnConnect;
            return MailKit.Security.SecureSocketOptions.StartTls;
        }

        private async Task SendMessageAsync(EmailMessage message, CompanyEmailSettings settings, CancellationToken ct = default)
        {
            try
            {
                await this.smtpSender.SendAsync(settings, message, ct);
                message.Status = EmailStatuses.Sent;
                message.SentAt = DateTime.UtcNow;
                message.LastError = null;
                await this.storage.UpdateEmailMessageAsync(message);
                await LogDocumentEmailAsync(message);
            }
            catch (Exception ex)
            {
                message.RetryCount++;
                message.LastError = ex.Message.Length > 500 ? ex.Message[..500] : ex.Message;
                if (message.RetryCount >= 3)
                    message.Status = EmailStatuses.Failed;
                await this.storage.UpdateEmailMessageAsync(message);
                throw;
            }
        }

        private async Task LogDocumentEmailAsync(EmailMessage message)
        {
            if (string.IsNullOrWhiteSpace(message.DocumentType) || !message.DocumentId.HasValue) return;
            await SalesDocumentAudit.LogAsync(
                this.storage,
                message.CompanyId,
                message.DocumentType,
                message.DocumentId.Value,
                "EmailSent",
                message.CreatedBy,
                $"Email envoyé à {message.ToEmail} — {message.Subject} (tracking {message.TrackingId})");
        }

        private async Task EnsureRateLimitAsync(string companyId, int maxPerHour)
        {
            if (maxPerHour <= 0) return;
            var since = DateTime.UtcNow.AddHours(-1);
            var count = await this.storage.SelectAllEmailMessages()
                .Where(m => m.CompanyId == companyId && m.SentAt >= since && m.Status == EmailStatuses.Sent)
                .CountAsync();
            if (count >= maxPerHour)
                throw new InvalidOperationException($"Limite d'envoi atteinte ({maxPerHour}/heure, RG-EM15).");
        }

        private async Task<string> ResolveCompanyIdAsync(string? companyId)
        {
            if (!string.IsNullOrWhiteSpace(companyId)) return companyId;
            var first = await this.storage.SelectAllCompanies().Select(c => c.Id).FirstOrDefaultAsync();
            if (string.IsNullOrWhiteSpace(first))
                throw new InvalidOperationException("Aucune société configurée.");
            return first;
        }
    }
}
