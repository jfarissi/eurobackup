using System;
using System.Collections.Generic;
using System.Linq;
using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Models.Entities;
using Backup.Web.Api.Server.Services.Sales;
using Moq;

namespace Backup.Web.Api.Tests.Business
{
    public class SalesBusinessRulesTests
    {
        [Theory]
        [InlineData("Cancelled")]
        [InlineData("Canceled")]
        [InlineData("Deleted")]
        [InlineData("Expired")]
        [InlineData("Rejected")]
        [InlineData("Refused")]
        public void RejectIfParentUnusable_ReturnsError_ForUnusableStatuses(string status)
        {
            var result = SalesBusinessRules.RejectIfParentUnusable(status, "Devis DV-001");

            Assert.NotNull(result);
            Assert.Contains(status, result);
        }

        [Theory]
        [InlineData("Confirmed")]
        [InlineData("Draft")]
        [InlineData(null)]
        [InlineData("")]
        public void RejectIfParentUnusable_ReturnsNull_ForUsableStatuses(string? status)
        {
            var result = SalesBusinessRules.RejectIfParentUnusable(status, "Devis DV-001");

            Assert.Null(result);
        }

        [Theory]
        [InlineData("Blocked")]
        [InlineData("Closed")]
        public void RejectIfPartyNotActive_ReturnsError_ForInactiveStatuses(string status)
        {
            var result = SalesBusinessRules.RejectIfPartyNotActive(status, "Client ACME");

            Assert.NotNull(result);
            Assert.Contains(status, result);
        }

        [Theory]
        [InlineData("Active")]
        [InlineData("Actif")]
        [InlineData(null)]
        [InlineData("")]
        public void RejectIfPartyNotActive_ReturnsNull_ForActiveOrEmptyStatuses(string? status)
        {
            var result = SalesBusinessRules.RejectIfPartyNotActive(status, "Client ACME");

            Assert.Null(result);
        }

        [Theory]
        [InlineData(null, true)]
        [InlineData("", true)]
        [InlineData("Draft", true)]
        [InlineData("Sent", true)]
        [InlineData("Confirmed", false)]
        [InlineData("Closed", false)]
        [InlineData("Cancelled", false)]
        public void CanFullyEdit_ReturnsExpected(string? status, bool expected)
        {
            Assert.Equal(expected, SalesBusinessRules.CanFullyEdit(status));
        }

        [Theory]
        [InlineData(null, true)]
        [InlineData("", true)]
        [InlineData("Draft", true)]
        [InlineData("Confirmed", false)]
        [InlineData("Sent", false)]
        [InlineData("Closed", false)]
        public void CanPhysicallyDelete_ReturnsExpected(string? status, bool expected)
        {
            Assert.Equal(expected, SalesBusinessRules.CanPhysicallyDelete(status));
        }

        private static Mock<IStorageBroker> CreateStorageWithCreditNotes(params CreditNoteEntity[] creditNotes)
        {
            var storage = new Mock<IStorageBroker>();
            storage.Setup(s => s.SelectAllCreditNotes()).Returns(creditNotes.AsQueryable());
            return storage;
        }

        [Fact]
        public void ValidateCreditCap_Fails_WhenSumExceedsTotalTTC()
        {
            var invoice = new SalesInvoice { Id = 1, InvoiceNumber = "FAC-2026-0001", TotalTTC = 100m };
            var existing = new CreditNoteEntity { Id = 10, SalesInvoiceId = 1, Status = "Applied", TotalTTC = 60m };
            var storage = CreateStorageWithCreditNotes(existing);

            var result = SalesBusinessRules.ValidateCreditCap(storage.Object, invoice, 50m);

            Assert.NotNull(result);
            Assert.Contains("FAC-2026-0001", result);
        }

        [Fact]
        public void ValidateCreditCap_ReturnsNull_WhenSumWithinTotalTTC()
        {
            var invoice = new SalesInvoice { Id = 1, InvoiceNumber = "FAC-2026-0002", TotalTTC = 100m };
            var existing = new CreditNoteEntity { Id = 10, SalesInvoiceId = 1, Status = "Applied", TotalTTC = 40m };
            var storage = CreateStorageWithCreditNotes(existing);

            var result = SalesBusinessRules.ValidateCreditCap(storage.Object, invoice, 50m);

            Assert.Null(result);
        }

        [Fact]
        public void ValidateCreditCap_IgnoresCancelledCreditNotes()
        {
            var invoice = new SalesInvoice { Id = 1, InvoiceNumber = "FAC-2026-0003", TotalTTC = 100m };
            var cancelled = new CreditNoteEntity { Id = 11, SalesInvoiceId = 1, Status = "Cancelled", TotalTTC = 90m };
            var storage = CreateStorageWithCreditNotes(cancelled);

            var result = SalesBusinessRules.ValidateCreditCap(storage.Object, invoice, 50m);

            Assert.Null(result);
        }

        [Fact]
        public void ValidateCreditCap_ExcludesSpecifiedCreditNoteId()
        {
            var invoice = new SalesInvoice { Id = 1, InvoiceNumber = "FAC-2026-0004", TotalTTC = 100m };
            var existing = new CreditNoteEntity { Id = 12, SalesInvoiceId = 1, Status = "Applied", TotalTTC = 90m };
            var storage = CreateStorageWithCreditNotes(existing);

            var result = SalesBusinessRules.ValidateCreditCap(storage.Object, invoice, 50m, excludeCreditNoteId: 12);

            Assert.Null(result);
        }

        private static Quote CreateQuote(string status, DateTime? expirationDate = null) => new Quote
        {
            Id = 1,
            QuoteNumber = "DV-2026-0001",
            Status = status,
            ExpirationDate = expirationDate ?? DateTime.UtcNow.AddDays(10)
        };

        [Fact]
        public void ValidateQuoteConvertible_ReturnsNull_ForAcceptedQuote()
        {
            var quote = CreateQuote("Accepted");

            var result = SalesBusinessRules.ValidateQuoteConvertible(quote);

            Assert.Null(result);
        }

        [Fact]
        public void ValidateQuoteConvertible_ReturnsNull_ForPartiallyConvertedQuote()
        {
            var quote = CreateQuote("PartiallyConverted");

            var result = SalesBusinessRules.ValidateQuoteConvertible(quote);

            Assert.Null(result);
        }

        [Fact]
        public void ValidateQuoteConvertible_Fails_ForConvertedQuote()
        {
            var quote = CreateQuote("Converted");

            var result = SalesBusinessRules.ValidateQuoteConvertible(quote);

            Assert.NotNull(result);
            Assert.Contains("déjà été entièrement converti", result);
        }

        [Fact]
        public void ValidateQuoteConvertible_Fails_ForDraftQuote()
        {
            var quote = CreateQuote("Draft");

            var result = SalesBusinessRules.ValidateQuoteConvertible(quote);

            Assert.NotNull(result);
        }

        [Fact]
        public void ValidateQuoteConvertible_Fails_WhenExpired()
        {
            var quote = CreateQuote("Accepted", DateTime.UtcNow.AddDays(-5));

            var result = SalesBusinessRules.ValidateQuoteConvertible(quote);

            Assert.NotNull(result);
            Assert.Contains("expiré", result);
        }

        [Fact]
        public void ValidateQuoteConvertible_Fails_ForExpiredStatus()
        {
            var quote = CreateQuote("Expired");

            var result = SalesBusinessRules.ValidateQuoteConvertible(quote);

            Assert.NotNull(result);
        }

        [Theory]
        [InlineData(null, false)]
        [InlineData("", false)]
        [InlineData("Draft", false)]
        [InlineData("Pending", false)]
        [InlineData("Cancelled", false)]
        [InlineData("Confirmed", true)]
        [InlineData("Delivered", true)]
        [InlineData("Closed", true)]
        public void IsOrderCommitted_ReturnsExpected(string? status, bool expected)
        {
            Assert.Equal(expected, SalesBusinessRules.IsOrderCommitted(status));
        }

        [Fact]
        public void ValidateSameCustomer_ReturnsNull_WhenIdsMatch()
        {
            var result = SalesBusinessRules.ValidateSameCustomer(5, 5, "commande");

            Assert.Null(result);
        }

        [Fact]
        public void ValidateSameCustomer_ReturnsError_WhenIdsDiffer()
        {
            var result = SalesBusinessRules.ValidateSameCustomer(5, 6, "commande");

            Assert.NotNull(result);
            Assert.Contains("commande", result);
        }

        [Fact]
        public void ApplyHeaderDiscount_DoesNothing_WhenDiscountIsZeroOrNegative()
        {
            decimal ht = 100m, vat = 21m, ttc = 121m;

            SalesBusinessRules.ApplyHeaderDiscount(0m, ref ht, ref vat, ref ttc);

            Assert.Equal(100m, ht);
            Assert.Equal(21m, vat);
            Assert.Equal(121m, ttc);
        }

        [Fact]
        public void ApplyHeaderDiscount_AppliesProportionalDiscount()
        {
            decimal ht = 100m, vat = 21m, ttc = 121m;

            SalesBusinessRules.ApplyHeaderDiscount(10m, ref ht, ref vat, ref ttc);

            Assert.Equal(90m, ht);
            Assert.Equal(18.9m, vat);
            Assert.Equal(108.9m, ttc);
        }

        [Theory]
        [InlineData(-10, 0)]
        [InlineData(150, 100)]
        [InlineData(50, 50)]
        [InlineData(0, 0)]
        [InlineData(100, 100)]
        public void CapDiscountPercent_ClampsToBounds(decimal input, decimal expected)
        {
            Assert.Equal(expected, SalesBusinessRules.CapDiscountPercent(input));
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(101)]
        public void ValidateDiscountPercent_ReturnsError_OutOfBounds(decimal discount)
        {
            var result = SalesBusinessRules.ValidateDiscountPercent(discount, "ligne 1");

            Assert.NotNull(result);
            Assert.Contains("ligne 1", result);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(50)]
        [InlineData(100)]
        public void ValidateDiscountPercent_ReturnsNull_WithinBounds(decimal discount)
        {
            var result = SalesBusinessRules.ValidateDiscountPercent(discount, "ligne 1");

            Assert.Null(result);
        }

        [Fact]
        public void RejectCurrencyChangeIfFrozen_ReturnsNull_WhenStatusDraftOrEmpty()
        {
            Assert.Null(SalesBusinessRules.RejectCurrencyChangeIfFrozen(null, "EUR", "USD"));
            Assert.Null(SalesBusinessRules.RejectCurrencyChangeIfFrozen("Draft", "EUR", "USD"));
        }

        [Fact]
        public void RejectCurrencyChangeIfFrozen_ReturnsNull_WhenCurrencyUnchanged()
        {
            var result = SalesBusinessRules.RejectCurrencyChangeIfFrozen("Confirmed", "EUR", "EUR");

            Assert.Null(result);
        }

        [Fact]
        public void RejectCurrencyChangeIfFrozen_ReturnsError_WhenCurrencyChangedAfterDraft()
        {
            var result = SalesBusinessRules.RejectCurrencyChangeIfFrozen("Confirmed", "EUR", "USD");

            Assert.NotNull(result);
            Assert.Contains("EUR", result);
        }

        [Fact]
        public void RecalculateInvoiceTotals_ComputesLineAndHeaderTotals()
        {
            var invoice = new SalesInvoice
            {
                HeaderDiscountPercent = 0m,
                Lines = new List<SalesInvoiceLine>
                {
                    new SalesInvoiceLine { Quantity = 2, UnitPrice = 50m, DiscountPercent = 0m, VatRate = 21m },
                    new SalesInvoiceLine { Quantity = 1, UnitPrice = 100m, DiscountPercent = 10m, VatRate = 21m }
                }
            };

            SalesBusinessRules.RecalculateInvoiceTotals(invoice);

            // Line 1: 2 * 50 = 100 HT, TTC = 121
            Assert.Equal(100m, invoice.Lines[0].TotalHT);
            Assert.Equal(121m, invoice.Lines[0].TotalTTC);

            // Line 2: 1 * 100 * 0.9 = 90 HT, TTC = 108.9
            Assert.Equal(90m, invoice.Lines[1].TotalHT);
            Assert.Equal(108.9m, invoice.Lines[1].TotalTTC);

            Assert.Equal(190m, invoice.TotalHT);
            Assert.Equal(229.9m, invoice.TotalTTC);
            Assert.Equal(39.9m, invoice.TotalVat);
        }

        [Fact]
        public void RecalculateInvoiceTotals_AppliesHeaderDiscount()
        {
            var invoice = new SalesInvoice
            {
                HeaderDiscountPercent = 10m,
                Lines = new List<SalesInvoiceLine>
                {
                    new SalesInvoiceLine { Quantity = 1, UnitPrice = 100m, DiscountPercent = 0m, VatRate = 21m }
                }
            };

            SalesBusinessRules.RecalculateInvoiceTotals(invoice);

            Assert.Equal(90m, invoice.TotalHT);
            Assert.Equal(18.9m, invoice.TotalVat);
            Assert.Equal(108.9m, invoice.TotalTTC);
        }

        [Fact]
        public void RecalculateInvoiceTotals_AddsShippingAfterHeaderDiscount()
        {
            var invoice = new SalesInvoice
            {
                HeaderDiscountPercent = 10m,
                ShippingAmountHt = 20m,
                ShippingVatRate = 21m,
                Lines = new List<SalesInvoiceLine>
                {
                    new SalesInvoiceLine { ProductKey = "SKU1", Quantity = 1, UnitPrice = 100m, DiscountPercent = 0m, VatRate = 21m }
                }
            };

            SalesBusinessRules.RecalculateInvoiceTotals(invoice);

            // Marchandises après remise 10% : 90 HT + 18.9 TVA ; + port 20 HT + 4.2 TVA
            Assert.Equal(110m, invoice.TotalHT);
            Assert.Equal(23.1m, invoice.TotalVat);
            Assert.Equal(133.1m, invoice.TotalTTC);
        }

        [Fact]
        public void RecalculateInvoiceTotals_ShippingFeeLine_ExcludedFromHeaderDiscount()
        {
            var invoice = new SalesInvoice
            {
                HeaderDiscountPercent = 10m,
                Lines = new List<SalesInvoiceLine>
                {
                    new SalesInvoiceLine { ProductKey = "SKU1", Quantity = 1, UnitPrice = 100m, DiscountPercent = 0m, VatRate = 21m },
                    new SalesInvoiceLine { ProductKey = "FDP", Quantity = 1, UnitPrice = 20m, DiscountPercent = 0m, VatRate = 21m }
                }
            };

            SalesBusinessRules.RecalculateInvoiceTotals(invoice);

            // Marchandises 90 + 18.9 ; FDP 20 + 4.2 (non remisé)
            Assert.Equal(110m, invoice.TotalHT);
            Assert.Equal(23.1m, invoice.TotalVat);
            Assert.Equal(133.1m, invoice.TotalTTC);
        }

        [Fact]
        public void ValidateShippingAmount_RejectsNegative()
        {
            Assert.NotNull(SalesBusinessRules.ValidateShippingAmount(-1m));
            Assert.Null(SalesBusinessRules.ValidateShippingAmount(0m));
            Assert.Null(SalesBusinessRules.ValidateShippingAmount(15m));
        }

        [Fact]
        public void RecalculateInvoiceTotals_HandlesNullLines()
        {
            var invoice = new SalesInvoice { Lines = null! };

            SalesBusinessRules.RecalculateInvoiceTotals(invoice);

            Assert.NotNull(invoice.Lines);
            Assert.Equal(0m, invoice.TotalHT);
            Assert.Equal(0m, invoice.TotalTTC);
        }

        [Fact]
        public void RecalculatePurchaseOrderTotals_AppliesDiscountAndShipping()
        {
            var order = new PurchaseOrder
            {
                HeaderDiscountPercent = 10m,
                ShippingAmountHt = 15m,
                ShippingVatRate = 21m,
                Lines = new List<PurchaseOrderLine>
                {
                    new PurchaseOrderLine { ProductKey = "SKU1", Quantity = 1, UnitPrice = 100m, DiscountPercent = 0m, VatRate = 21m }
                }
            };

            SalesBusinessRules.RecalculatePurchaseOrderTotals(order);

            // Marchandises 90 + 18.9 ; port 15 + 3.15
            Assert.Equal(105m, order.TotalHT);
            Assert.Equal(22.05m, order.TotalVat);
            Assert.Equal(127.05m, order.TotalTTC);
        }

        [Fact]
        public void FormatPartyAddress_ReturnsNonEmpty_ForFullCustomer()
        {
            var customer = new Customer
            {
                Name = "ACME Corp",
                Address = "Rue de la Paix 1",
                PostalCode = "1000",
                City = "Bruxelles",
                Country = "Belgique"
            };

            var result = SalesBusinessRules.FormatPartyAddress(customer);

            Assert.False(string.IsNullOrWhiteSpace(result));
            Assert.Contains("ACME Corp", result);
            Assert.Contains("Bruxelles", result);
            Assert.Contains("Belgique", result);
        }

        [Fact]
        public void FormatPartyAddress_ReturnsNonEmpty_ForMinimalCustomer()
        {
            var customer = new Customer { Name = "Minimal Client" };

            var result = SalesBusinessRules.FormatPartyAddress(customer);

            Assert.False(string.IsNullOrWhiteSpace(result));
            Assert.Equal("Minimal Client", result);
        }
    }
}
