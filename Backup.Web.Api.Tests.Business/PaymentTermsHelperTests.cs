using System;
using Backup.Web.Api.Server.Services.Sales;

namespace Backup.Web.Api.Tests.Business
{
    public class PaymentTermsHelperTests
    {
        [Fact]
        public void ComputeDueDate_Adds30Days_ForFrenchWording()
        {
            var invoiceDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            var due = PaymentTermsHelper.ComputeDueDate(invoiceDate, "30 jours");

            Assert.Equal(invoiceDate.AddDays(30), due);
        }

        [Fact]
        public void ComputeDueDate_Adds60Days_ForShortDCode()
        {
            var invoiceDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            var due = PaymentTermsHelper.ComputeDueDate(invoiceDate, "60D");

            Assert.Equal(invoiceDate.AddDays(60), due);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void ComputeDueDate_UsesDefaultDays_WhenPaymentTermsMissing(string? paymentTerms)
        {
            var invoiceDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            var due = PaymentTermsHelper.ComputeDueDate(invoiceDate, paymentTerms);

            Assert.Equal(invoiceDate.AddDays(30), due);
        }

        [Fact]
        public void ComputeDueDate_UsesCustomDefaultDays_WhenPaymentTermsMissing()
        {
            var invoiceDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            var due = PaymentTermsHelper.ComputeDueDate(invoiceDate, null, defaultDays: 45);

            Assert.Equal(invoiceDate.AddDays(45), due);
        }

        [Fact]
        public void EnsureNotBeforeInvoiceDate_ReturnsInvoiceDate_WhenDueDateIsEarlier()
        {
            var invoiceDate = new DateTime(2026, 2, 15);
            var dueDate = new DateTime(2026, 2, 1);

            var result = PaymentTermsHelper.EnsureNotBeforeInvoiceDate(invoiceDate, dueDate);

            Assert.Equal(invoiceDate.Date, result);
        }

        [Fact]
        public void EnsureNotBeforeInvoiceDate_ReturnsDueDate_WhenLaterThanInvoiceDate()
        {
            var invoiceDate = new DateTime(2026, 2, 1);
            var dueDate = new DateTime(2026, 3, 1);

            var result = PaymentTermsHelper.EnsureNotBeforeInvoiceDate(invoiceDate, dueDate);

            Assert.Equal(dueDate.Date, result);
        }

        [Fact]
        public void EnsureNotBeforeInvoiceDate_DefaultsInvoiceDate_WhenDefault()
        {
            var result = PaymentTermsHelper.EnsureNotBeforeInvoiceDate(default, default);

            Assert.True(result >= DateTime.UtcNow.Date.AddDays(29));
        }

        [Theory]
        [InlineData("30 jours", 30)]
        [InlineData("60D", 60)]
        [InlineData("45 days", 45)]
        [InlineData(null, null)]
        [InlineData("", null)]
        [InlineData("comptant", null)]
        public void ParseNetDays_ParsesExpectedValues(string? paymentTerms, int? expected)
        {
            Assert.Equal(expected, PaymentTermsHelper.ParseNetDays(paymentTerms));
        }

        [Theory]
        [InlineData("30 jours fin de mois", true)]
        [InlineData("60 EOM", true)]
        [InlineData("end of month", true)]
        [InlineData("30 jours", false)]
        [InlineData(null, false)]
        public void IsEndOfMonth_DetectsExpectedWording(string? paymentTerms, bool expected)
        {
            Assert.Equal(expected, PaymentTermsHelper.IsEndOfMonth(paymentTerms));
        }
    }
}
