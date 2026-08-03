using System.Collections.Generic;
using System.Linq;
using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Models.Entities;
using Backup.Web.Api.Server.Services.Sales;
using Moq;

namespace Backup.Web.Api.Tests.Business
{
    /// <summary>RG-CC4/CC9, RG-V8 : gel commande + suivi livraison/facturation.</summary>
    public class P1OrderFreezeAndFulfillmentTests
    {
        [Fact]
        public void RejectIfCustomerChangedAfterCommit_BlocksWhenCommitted()
        {
            var err = SalesBusinessRules.RejectIfCustomerChangedAfterCommit("Confirmed", 1, 2);

            Assert.NotNull(err);
            Assert.Contains("client", err);
        }

        [Fact]
        public void RejectIfCustomerChangedAfterCommit_AllowsSameCustomer()
        {
            Assert.Null(SalesBusinessRules.RejectIfCustomerChangedAfterCommit("Confirmed", 1, 1));
        }

        [Fact]
        public void RejectIfCustomerChangedAfterCommit_AllowsChangeInDraft()
        {
            Assert.Null(SalesBusinessRules.RejectIfCustomerChangedAfterCommit("Draft", 1, 2));
        }

        [Fact]
        public void RejectIfLockedOrderLineViolation_BlocksDelete()
        {
            var locked = new SalesOrderLine { ProductKey = "SKU1", Quantity = 10, DeliveredQuantity = 3, UnitPrice = 5, VatRate = 21 };

            var err = SalesBusinessRules.RejectIfLockedOrderLineViolation(locked, null);

            Assert.NotNull(err);
            Assert.Contains("supprimer", err);
        }

        [Fact]
        public void RejectIfLockedOrderLineViolation_BlocksQtyBelowDelivered()
        {
            var locked = new SalesOrderLine { ProductKey = "SKU1", Quantity = 10, DeliveredQuantity = 5, UnitPrice = 5, VatRate = 21 };
            var match = new SalesOrderLine { ProductKey = "SKU1", Quantity = 4, UnitPrice = 5, VatRate = 21 };

            var err = SalesBusinessRules.RejectIfLockedOrderLineViolation(locked, match);

            Assert.NotNull(err);
            Assert.Contains("livrée", err);
        }

        [Fact]
        public void RejectIfLockedOrderLineViolation_BlocksQtyIncreaseAfterPartialDelivery()
        {
            var locked = new SalesOrderLine { ProductKey = "SKU1", Quantity = 10, DeliveredQuantity = 2, UnitPrice = 5, VatRate = 21 };
            var match = new SalesOrderLine { ProductKey = "SKU1", Quantity = 12, UnitPrice = 5, VatRate = 21 };

            var err = SalesBusinessRules.RejectIfLockedOrderLineViolation(locked, match);

            Assert.NotNull(err);
            Assert.Contains("augmenter", err);
        }

        [Fact]
        public void RejectIfLockedOrderLineViolation_BlocksPriceChange()
        {
            var locked = new SalesOrderLine { ProductKey = "SKU1", Quantity = 10, DeliveredQuantity = 1, UnitPrice = 5, VatRate = 21 };
            var match = new SalesOrderLine { ProductKey = "SKU1", Quantity = 10, UnitPrice = 6, VatRate = 21 };

            var err = SalesBusinessRules.RejectIfLockedOrderLineViolation(locked, match);

            Assert.NotNull(err);
            Assert.Contains("figés", err);
        }

        [Fact]
        public void RejectIfLockedOrderLineViolation_AllowsUnchangedLockedLine()
        {
            var locked = new SalesOrderLine { ProductKey = "SKU1", Quantity = 10, DeliveredQuantity = 3, UnitPrice = 5, VatRate = 21 };
            var match = new SalesOrderLine { ProductKey = "SKU1", Quantity = 10, UnitPrice = 5, VatRate = 21 };

            Assert.Null(SalesBusinessRules.RejectIfLockedOrderLineViolation(locked, match));
        }

        [Fact]
        public void RejectIfLockedOrderLineViolation_SkipsUnlockedLine()
        {
            var unlocked = new SalesOrderLine { ProductKey = "SKU1", Quantity = 10, DeliveredQuantity = 0, InvoicedQuantity = 0 };

            Assert.Null(SalesBusinessRules.RejectIfLockedOrderLineViolation(unlocked, null));
        }

        [Fact]
        public void RemainingQuantity_SubtractsDelivered()
        {
            var line = new SalesOrderLine { Quantity = 10, DeliveredQuantity = 4 };

            Assert.Equal(6m, SalesBusinessRules.RemainingQuantity(line));
        }

        [Fact]
        public void RemainingToShip_AccountsForDraftDeliveryNotes()
        {
            var order = new SalesOrder
            {
                Id = 1,
                Lines = new List<SalesOrderLine>
                {
                    new() { ProductKey = "SKU1", Quantity = 10, DeliveredQuantity = 0 }
                }
            };
            var notes = new[]
            {
                new SalesDeliveryNote
                {
                    SalesOrderId = 1,
                    Status = "Draft",
                    Lines = new List<SalesDeliveryNoteLine>
                    {
                        new() { ProductKey = "SKU1", DeliveredQuantity = 4 }
                    }
                },
                new SalesDeliveryNote
                {
                    SalesOrderId = 1,
                    Status = "Cancelled",
                    Lines = new List<SalesDeliveryNoteLine>
                    {
                        new() { ProductKey = "SKU1", DeliveredQuantity = 99 }
                    }
                }
            };
            var storage = new Mock<IStorageBroker>();
            storage.Setup(s => s.SelectAllSalesDeliveryNotes()).Returns(notes.AsQueryable());

            var remaining = SalesBusinessRules.RemainingToShip(storage.Object, order, order.Lines[0]);

            Assert.Equal(6m, remaining);
        }

        [Theory]
        [InlineData(10, 10, 10, "Closed")]
        [InlineData(10, 10, 0, "Delivered")]
        [InlineData(10, 5, 0, "PartiallyDelivered")]
        [InlineData(10, 0, 5, "PartiallyInvoiced")]
        [InlineData(10, 0, 10, "Invoiced")]
        public void RefreshOrderFulfillmentStatus_SetsExpected(
            decimal qty, decimal delivered, decimal invoiced, string expected)
        {
            var order = new SalesOrder
            {
                Status = "Confirmed",
                Lines = new List<SalesOrderLine>
                {
                    new() { Quantity = qty, DeliveredQuantity = delivered, InvoicedQuantity = invoiced }
                }
            };

            SalesBusinessRules.RefreshOrderFulfillmentStatus(order);

            Assert.Equal(expected, order.Status);
        }

        [Fact]
        public void RefreshOrderFulfillmentStatus_PreservesPending()
        {
            var order = new SalesOrder
            {
                Status = "Pending",
                Lines = new List<SalesOrderLine>
                {
                    new() { Quantity = 10, DeliveredQuantity = 10, InvoicedQuantity = 10 }
                }
            };

            SalesBusinessRules.RefreshOrderFulfillmentStatus(order);

            Assert.Equal("Pending", order.Status);
        }

        [Fact]
        public void AddInvoicedQuantities_CapsAndClosesWhenFullyDeliveredAndInvoiced()
        {
            var order = new SalesOrder
            {
                Status = "Delivered",
                Lines = new List<SalesOrderLine>
                {
                    new() { ProductKey = "SKU1", Quantity = 10, DeliveredQuantity = 10, InvoicedQuantity = 0 }
                }
            };

            SalesBusinessRules.AddInvoicedQuantities(order, new[] { ("SKU1", 15m) });

            Assert.Equal(10m, order.Lines[0].InvoicedQuantity);
            Assert.Equal("Closed", order.Status);
        }

        [Fact]
        public void ValidateCreditLimit_FailsWhenProjectedExceedsCeiling()
        {
            var customer = new Customer { Id = 1, Name = "ACME", CreditLimit = 100m, Balance = 40m };
            var openOrders = new[]
            {
                new SalesOrder { Id = 2, CustomerId = 1, Status = "Confirmed", TotalTTC = 50m }
            };
            var storage = new Mock<IStorageBroker>();
            storage.Setup(s => s.SelectAllSalesOrders()).Returns(openOrders.AsQueryable());

            var err = SalesBusinessRules.ValidateCreditLimit(storage.Object, customer, 20m);

            Assert.NotNull(err);
            Assert.Contains("Plafond", err);
        }

        [Fact]
        public void ValidateCreditLimit_AllowsUnlimitedWhenCreditLimitZero()
        {
            var customer = new Customer { Id = 1, Name = "ACME", CreditLimit = 0m, Balance = 9999m };
            var storage = new Mock<IStorageBroker>();
            storage.Setup(s => s.SelectAllSalesOrders()).Returns(Enumerable.Empty<SalesOrder>().AsQueryable());

            Assert.Null(SalesBusinessRules.ValidateCreditLimit(storage.Object, customer, 1000m));
        }

        private sealed class SoftDeletable : IHasSoftDelete
        {
            public bool IsDeleted { get; set; }
            public System.DateTime? DeletedAt { get; set; }
            public string? DeletedBy { get; set; }
        }

        private sealed class Archivable : IHasArchive
        {
            public bool IsArchived { get; set; }
            public System.DateTime? ArchivedAt { get; set; }
            public string? ArchivedBy { get; set; }
        }

        [Fact]
        public void SoftDelete_MarksEntity()
        {
            var entity = new SoftDeletable();

            SalesBusinessRules.SoftDelete(entity, "tester");

            Assert.True(entity.IsDeleted);
            Assert.Equal("tester", entity.DeletedBy);
            Assert.NotNull(entity.DeletedAt);
        }

        [Theory]
        [InlineData("Closed", true)]
        [InlineData("Cancelled", true)]
        [InlineData("Paid", true)]
        [InlineData("Draft", false)]
        [InlineData("Confirmed", false)]
        public void CanArchive_ReturnsExpected(string status, bool expected)
        {
            Assert.Equal(expected, SalesBusinessRules.CanArchive(status));
        }

        [Fact]
        public void Archive_MarksEntity()
        {
            var entity = new Archivable();

            SalesBusinessRules.Archive(entity, "archiver");

            Assert.True(entity.IsArchived);
            Assert.Equal("archiver", entity.ArchivedBy);
            Assert.NotNull(entity.ArchivedAt);
        }
    }
}
