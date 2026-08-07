using System.Linq;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Models.Entities.Email;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Backup.Web.Api.Server.Brokers.Storage
{
    public partial class StorageBroker
    {
        public DbSet<CompanyEmailSettings> CompanyEmailSettings { get; set; } = null!;
        public DbSet<EmailMessage> EmailMessages { get; set; } = null!;

        public IQueryable<CompanyEmailSettings> SelectAllCompanyEmailSettings() =>
            this.CompanyEmailSettings.AsQueryable();

        public IQueryable<EmailMessage> SelectAllEmailMessages() =>
            this.EmailMessages.AsQueryable();

        public async ValueTask<CompanyEmailSettings?> SelectCompanyEmailSettingsByCompanyIdAsync(string companyId) =>
            await this.CompanyEmailSettings.FirstOrDefaultAsync(s => s.CompanyId == companyId);

        public async ValueTask<CompanyEmailSettings> UpsertCompanyEmailSettingsAsync(CompanyEmailSettings settings)
        {
            var existing = await this.CompanyEmailSettings.FirstOrDefaultAsync(s => s.CompanyId == settings.CompanyId);
            if (existing == null)
            {
                var entry = await this.CompanyEmailSettings.AddAsync(settings);
                await this.SaveChangesAsync();
                return entry.Entity;
            }

            existing.Enabled = settings.Enabled;
            existing.SmtpHost = settings.SmtpHost;
            existing.SmtpPort = settings.SmtpPort;
            existing.UseSsl = settings.UseSsl;
            existing.IgnoreSslErrors = settings.IgnoreSslErrors;
            existing.Username = settings.Username;
            if (!string.IsNullOrWhiteSpace(settings.Password))
                existing.Password = settings.Password;
            existing.FromEmail = settings.FromEmail;
            existing.FromDisplayName = settings.FromDisplayName;
            existing.DefaultReplyTo = settings.DefaultReplyTo;
            existing.MaxEmailsPerHour = settings.MaxEmailsPerHour;
            existing.MaxAttachmentBytes = settings.MaxAttachmentBytes;
            existing.FooterHtml = settings.FooterHtml;
            existing.AutoPaymentRemindersEnabled = settings.AutoPaymentRemindersEnabled;
            existing.PaymentReminderDaysN1 = settings.PaymentReminderDaysN1;
            existing.PaymentReminderDaysN2 = settings.PaymentReminderDaysN2;
            existing.PaymentReminderDaysN3 = settings.PaymentReminderDaysN3;
            existing.AutoStockAlertsEnabled = settings.AutoStockAlertsEnabled;
            existing.StockAlertRecipients = settings.StockAlertRecipients;
            existing.StockAlertCooldownHours = settings.StockAlertCooldownHours;
            existing.AutoEmailOnPurchaseOrderSend = settings.AutoEmailOnPurchaseOrderSend;
            existing.UpdatedAt = settings.UpdatedAt;
            existing.UpdatedBy = settings.UpdatedBy;
            await this.SaveChangesAsync();
            return existing;
        }

        public async ValueTask<EmailMessage> InsertEmailMessageAsync(EmailMessage message)
        {
            EntityEntry<EmailMessage> entry = await this.EmailMessages.AddAsync(message);
            await this.SaveChangesAsync();
            return entry.Entity;
        }

        public async ValueTask<EmailMessage> UpdateEmailMessageAsync(EmailMessage message)
        {
            this.EmailMessages.Update(message);
            await this.SaveChangesAsync();
            return message;
        }

        public async ValueTask<EmailMessage?> SelectEmailMessageByIdAsync(long id) =>
            await this.EmailMessages.FirstOrDefaultAsync(m => m.Id == id);

        public async ValueTask<List<EmailMessage>> SelectPendingEmailMessagesAsync(int batchSize, DateTime nowUtc)
        {
            return await this.EmailMessages
                .Where(m =>
                    (m.Status == EmailStatuses.Pending
                     || (m.Status == EmailStatuses.Scheduled && m.ScheduledAt != null && m.ScheduledAt <= nowUtc))
                    && m.RetryCount < 3)
                .OrderBy(m => m.CreatedAt)
                .Take(batchSize)
                .ToListAsync();
        }
    }
}
