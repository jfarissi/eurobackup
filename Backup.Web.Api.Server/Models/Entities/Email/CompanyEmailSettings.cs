using System.ComponentModel.DataAnnotations;
using System;
using Backup.Web.Api.Server.Services.Tenancy;

namespace Backup.Web.Api.Server.Models.Entities.Email
{
    /// <summary>Paramètres SMTP par société (RG-EM7, RG-PA1).</summary>
    public class CompanyEmailSettings : IHasCompanyId
    {
        [Key]
        public string CompanyId { get; set; } = string.Empty;
        public bool Enabled { get; set; }
        public string SmtpHost { get; set; } = string.Empty;
        public int SmtpPort { get; set; } = 587;
        public bool UseSsl { get; set; } = true;
        /// <summary>Ignore les erreurs de certificat SSL (hébergement mutualisé / hostname SMTP ≠ CN du certificat).</summary>
        public bool IgnoreSslErrors { get; set; }
        public string? Username { get; set; }
        public string? Password { get; set; }
        public string FromEmail { get; set; } = string.Empty;
        public string FromDisplayName { get; set; } = string.Empty;
        public string? DefaultReplyTo { get; set; }
        public int MaxEmailsPerHour { get; set; } = 500;
        public int MaxAttachmentBytes { get; set; } = 10 * 1024 * 1024;
        public string? FooterHtml { get; set; }

        /// <summary>Relances automatiques factures impayées (J+N après échéance).</summary>
        public bool AutoPaymentRemindersEnabled { get; set; }
        public int PaymentReminderDaysN1 { get; set; } = 5;
        public int PaymentReminderDaysN2 { get; set; } = 15;
        public int PaymentReminderDaysN3 { get; set; } = 30;

        /// <summary>Alertes email si stock dispo &lt; MinStock.</summary>
        public bool AutoStockAlertsEnabled { get; set; }
        /// <summary>Destinataires alertes stock (séparés par ;).</summary>
        public string? StockAlertRecipients { get; set; }
        public int StockAlertCooldownHours { get; set; } = 24;

        /// <summary>Envoi email CDF lors du passage Confirmed → Sent.</summary>
        public bool AutoEmailOnPurchaseOrderSend { get; set; } = true;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public string? UpdatedBy { get; set; }
    }
}
