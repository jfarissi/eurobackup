using Backup.Web.Api.Server.Models.Entities.Email;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace Backup.Web.Api.Server.Services.Email
{
    public interface ISmtpEmailSender
    {
        Task SendAsync(CompanyEmailSettings settings, EmailMessage message, CancellationToken ct = default);
    }

    public class SmtpEmailSender : ISmtpEmailSender
    {
        public async Task SendAsync(CompanyEmailSettings settings, EmailMessage message, CancellationToken ct = default)
        {
            if (!EmailAddressValidator.IsValid(settings.FromEmail))
                throw new InvalidOperationException("Email expéditeur (From) invalide. Corrigez Admin → Email.");

            var mime = new MimeMessage();
            mime.From.Add(new MailboxAddress(settings.FromDisplayName?.Trim() ?? string.Empty, settings.FromEmail.Trim()));

            if (!EmailAddressValidator.IsValid(message.ToEmail))
                throw new InvalidOperationException($"Destinataire invalide : {message.ToEmail}");
            mime.To.Add(MailboxAddress.Parse(message.ToEmail.Trim()));
            mime.Subject = message.Subject ?? string.Empty;

            var replyTo = EmailAddressValidator.Normalize(message.ReplyTo)
                ?? EmailAddressValidator.Normalize(settings.DefaultReplyTo);
            if (replyTo != null)
                mime.ReplyTo.Add(MailboxAddress.Parse(replyTo));

            foreach (var cc in EmailAddressValidator.ParseList(message.CcEmails))
                mime.Cc.Add(MailboxAddress.Parse(cc));

            var builder = new BodyBuilder
            {
                HtmlBody = message.BodyHtml,
                TextBody = message.BodyText ?? StripHtml(message.BodyHtml)
            };

            if (!string.IsNullOrWhiteSpace(settings.FooterHtml))
                builder.HtmlBody += settings.FooterHtml;

            if (!string.IsNullOrWhiteSpace(message.AttachmentFileName) && message.AttachmentBytes is { Length: > 0 })
                builder.Attachments.Add(message.AttachmentFileName, message.AttachmentBytes);

            mime.Body = builder.ToMessageBody();

            using var client = new SmtpClient();
            if (settings.IgnoreSslErrors)
                client.ServerCertificateValidationCallback = (_, _, _, _) => true;

            var socketOptions = settings.UseSsl
                ? (settings.SmtpPort == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls)
                : SecureSocketOptions.None;
            await client.ConnectAsync(settings.SmtpHost, settings.SmtpPort, socketOptions, ct);

            if (!string.IsNullOrWhiteSpace(settings.Username))
                await client.AuthenticateAsync(settings.Username, settings.Password ?? string.Empty, ct);

            await client.SendAsync(mime, ct);
            await client.DisconnectAsync(true, ct);
        }

        private static string StripHtml(string html)
        {
            if (string.IsNullOrWhiteSpace(html)) return string.Empty;
            return System.Text.RegularExpressions.Regex.Replace(html, "<[^>]+>", " ").Trim();
        }
    }
}
