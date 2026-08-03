using System.Linq;
using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Models.Entities;
using Backup.Web.Api.Server.Services.Sales;
using Moq;

namespace Backup.Web.Api.Tests.Business
{
    /// <summary>Paiement facture : BL livré requis, statut validé.</summary>
    public class P1SalesInvoicePayableTests
    {
        [Fact]
        public void ValidatePayable_RejectsDraft()
        {
            var invoice = new SalesInvoice { Id = 1, Status = "Draft", TotalTTC = 100m };
            var storage = CreateStorageWithNotes();

            var err = SalesInvoiceSettlement.ValidatePayable(invoice, storage.Object);

            Assert.NotNull(err);
            Assert.Contains("Validez", err);
        }

        [Fact]
        public void ValidatePayable_RejectsCancelled()
        {
            var invoice = new SalesInvoice { Id = 1, Status = "Cancelled", TotalTTC = 100m };
            var storage = CreateStorageWithNotes(
                new SalesDeliveryNote { SalesInvoiceId = 1, Status = "Delivered" });

            var err = SalesInvoiceSettlement.ValidatePayable(invoice, storage.Object);

            Assert.NotNull(err);
            Assert.Contains("annulée", err);
        }

        [Fact]
        public void ValidatePayable_RejectsWithoutDeliveredBl()
        {
            var invoice = new SalesInvoice { Id = 1, Status = "Validated", TotalTTC = 100m };
            var storage = CreateStorageWithNotes(
                new SalesDeliveryNote { SalesInvoiceId = 1, Status = "Draft" });

            var err = SalesInvoiceSettlement.ValidatePayable(invoice, storage.Object);

            Assert.NotNull(err);
            Assert.Contains("BL livré", err);
        }

        [Fact]
        public void ValidatePayable_AllowsValidatedWithDeliveredBl()
        {
            var invoice = new SalesInvoice { Id = 1, Status = "Validated", TotalTTC = 100m };
            var storage = CreateStorageWithNotes(
                new SalesDeliveryNote { SalesInvoiceId = 1, Status = "Delivered" });

            Assert.Null(SalesInvoiceSettlement.ValidatePayable(invoice, storage.Object));
        }

        [Fact]
        public void Enrich_ComputesRemainingAndCredited()
        {
            var invoice = new SalesInvoice { Id = 1, TotalTTC = 100m, PaidAmount = 30m };
            var storage = new Mock<IStorageBroker>();
            storage.Setup(s => s.SelectAllCreditNotes()).Returns(new[]
            {
                new CreditNoteEntity { SalesInvoiceId = 1, Status = "Applied", TotalTTC = 20m },
                new CreditNoteEntity { SalesInvoiceId = 1, Status = "Draft", TotalTTC = 50m }
            }.AsQueryable());
            storage.Setup(s => s.SelectAllSalesDeliveryNotes()).Returns(new[]
            {
                new SalesDeliveryNote { SalesInvoiceId = 1, Status = "Invoiced" }
            }.AsQueryable());

            SalesInvoiceSettlement.Enrich(invoice, storage.Object);

            Assert.Equal(20m, invoice.CreditedAmount);
            Assert.Equal(50m, invoice.RemainingAmount);
            Assert.True(invoice.HasDeliveredSource);
        }

        private static Mock<IStorageBroker> CreateStorageWithNotes(params SalesDeliveryNote[] notes)
        {
            var storage = new Mock<IStorageBroker>();
            storage.Setup(s => s.SelectAllSalesDeliveryNotes()).Returns(notes.AsQueryable());
            return storage;
        }
    }
}
