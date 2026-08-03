using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Backup.Web.Api.Server.Services.Sales
{
    /// <summary>RG-EC1 / EC2 : calcule la date d'échéance depuis les conditions de paiement du tiers.</summary>
    public static class PaymentTermsHelper
    {
        private static readonly Regex DaysRegex = new(
            @"(?<!\d)(\d{1,3})\s*(?:j(?:ours?)?|d(?:ays?)?|dagen)?(?!\d)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static DateTime ComputeDueDate(DateTime invoiceDate, string? paymentTerms, int defaultDays = 30)
        {
            var baseDate = invoiceDate == default ? DateTime.UtcNow.Date : invoiceDate.Date;
            var days = ParseNetDays(paymentTerms) ?? defaultDays;
            if (days < 0) days = defaultDays;

            var due = baseDate.AddDays(days);
            if (IsEndOfMonth(paymentTerms))
            {
                due = new DateTime(due.Year, due.Month, DateTime.DaysInMonth(due.Year, due.Month),
                    0, 0, 0, DateTimeKind.Utc);
            }

            return EnsureNotBeforeInvoiceDate(baseDate, due);
        }

        /// <summary>RG-EC2 : l'échéance ne peut pas être antérieure à la date de facture.</summary>
        public static DateTime EnsureNotBeforeInvoiceDate(DateTime invoiceDate, DateTime dueDate)
        {
            var inv = invoiceDate == default ? DateTime.UtcNow.Date : invoiceDate.Date;
            var due = dueDate == default ? inv.AddDays(30) : dueDate.Date;
            return due < inv ? inv : due;
        }

        public static int? ParseNetDays(string? paymentTerms)
        {
            if (string.IsNullOrWhiteSpace(paymentTerms)) return null;
            var m = DaysRegex.Match(paymentTerms);
            if (!m.Success) return null;
            if (int.TryParse(m.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var days))
                return days;
            return null;
        }

        public static bool IsEndOfMonth(string? paymentTerms)
        {
            if (string.IsNullOrWhiteSpace(paymentTerms)) return false;
            var t = paymentTerms.ToLowerInvariant();
            return t.Contains("eom")
                || t.Contains("fin de mois")
                || t.Contains("finde mois")
                || t.Contains("einde maand")
                || t.Contains("end of month")
                || t.Contains("fdm");
        }
    }
}
