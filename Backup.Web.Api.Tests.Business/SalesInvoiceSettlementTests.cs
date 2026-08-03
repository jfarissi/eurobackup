using Backup.Web.Api.Server.Models.Entities;
using Backup.Web.Api.Server.Services.Sales;

namespace Backup.Web.Api.Tests.Business
{
    public class SalesInvoiceSettlementTests
    {
        private static SalesInvoice CreateInvoice(string status, decimal totalTtc, decimal paidAmount) => new SalesInvoice
        {
            Id = 1,
            InvoiceNumber = "FAC-2026-0001",
            Status = status,
            TotalTTC = totalTtc,
            PaidAmount = paidAmount
        };

        [Fact]
        public void RefreshPaymentStatus_SetsPaid_WhenFullyPaidByCash()
        {
            var invoice = CreateInvoice("Validated", 100m, 100m);

            SalesInvoiceSettlement.RefreshPaymentStatus(invoice, creditedAmount: 0m);

            Assert.Equal("Paid", invoice.Status);
        }

        [Fact]
        public void RefreshPaymentStatus_SetsPaid_WhenFullyPaidByCredit()
        {
            var invoice = CreateInvoice("Validated", 100m, 0m);

            SalesInvoiceSettlement.RefreshPaymentStatus(invoice, creditedAmount: 100m);

            Assert.Equal("Paid", invoice.Status);
        }

        [Fact]
        public void RefreshPaymentStatus_SetsPaid_WhenFullyPaidByCombination()
        {
            var invoice = CreateInvoice("Validated", 100m, 60m);

            SalesInvoiceSettlement.RefreshPaymentStatus(invoice, creditedAmount: 40m);

            Assert.Equal("Paid", invoice.Status);
        }

        [Fact]
        public void RefreshPaymentStatus_SetsPartiallyPaid_WhenSomeAmountPaid()
        {
            var invoice = CreateInvoice("Validated", 100m, 40m);

            SalesInvoiceSettlement.RefreshPaymentStatus(invoice, creditedAmount: 0m);

            Assert.Equal("PartiallyPaid", invoice.Status);
        }

        [Fact]
        public void RefreshPaymentStatus_SetsPartiallyPaid_WhenSomeCreditApplied()
        {
            var invoice = CreateInvoice("Validated", 100m, 0m);

            SalesInvoiceSettlement.RefreshPaymentStatus(invoice, creditedAmount: 30m);

            Assert.Equal("PartiallyPaid", invoice.Status);
        }

        [Fact]
        public void RefreshPaymentStatus_RevertsToValidated_WhenPaymentAndCreditRemoved()
        {
            var invoice = CreateInvoice("PartiallyPaid", 100m, 0m);

            SalesInvoiceSettlement.RefreshPaymentStatus(invoice, creditedAmount: 0m);

            Assert.Equal("Validated", invoice.Status);
        }

        [Fact]
        public void RefreshPaymentStatus_RevertsFromPaidToValidated_WhenPaymentReversed()
        {
            var invoice = CreateInvoice("Paid", 100m, 0m);

            SalesInvoiceSettlement.RefreshPaymentStatus(invoice, creditedAmount: 0m);

            Assert.Equal("Validated", invoice.Status);
        }

        [Fact]
        public void RefreshPaymentStatus_LeavesStatusUnchanged_WhenNotPaidAndNotPreviouslyPaid()
        {
            var invoice = CreateInvoice("Sent", 100m, 0m);

            SalesInvoiceSettlement.RefreshPaymentStatus(invoice, creditedAmount: 0m);

            Assert.Equal("Sent", invoice.Status);
        }
    }
}
