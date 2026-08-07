using Backup.Web.Api.Server.Models.Entities;
using Backup.Web.Api.Server.Models.Entities.Email;

namespace Backup.Web.Api.Server.Services.Email
{
    public static class SalesInvoiceReminderHelper
    {
        public static bool IsOverdue(SalesInvoice invoice)
        {
            var isEligibleStatus = string.Equals(invoice.Status, "Validated", StringComparison.OrdinalIgnoreCase)
                || string.Equals(invoice.Status, "PartiallyPaid", StringComparison.OrdinalIgnoreCase)
                || string.Equals(invoice.Status, "Reminded", StringComparison.OrdinalIgnoreCase);
            if (!isEligibleStatus) return false;
            if (invoice.DueDate.Date >= DateTime.UtcNow.Date) return false;
            return invoice.RemainingAmount > 0.01m;
        }

        public static int GetDaysOverdue(SalesInvoice invoice)
        {
            if (!IsOverdue(invoice)) return 0;
            return Math.Max(0, (DateTime.UtcNow.Date - invoice.DueDate.Date).Days);
        }

        public static string ResolveTemplateCode(int daysOverdue, CompanyEmailSettings settings)
        {
            if (daysOverdue >= Math.Max(1, settings.PaymentReminderDaysN3))
                return EmailTemplateCodes.PaymentReminderN3;
            if (daysOverdue >= Math.Max(1, settings.PaymentReminderDaysN2))
                return EmailTemplateCodes.PaymentReminderN2;
            return EmailTemplateCodes.PaymentReminderN1;
        }

        public static string? ResolveAutoTemplateCode(int daysOverdue, CompanyEmailSettings settings)
        {
            if (daysOverdue < Math.Max(1, settings.PaymentReminderDaysN1)) return null;
            return ResolveTemplateCode(daysOverdue, settings);
        }
    }
}
