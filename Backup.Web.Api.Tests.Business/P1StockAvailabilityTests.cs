using System.Linq;
using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Models;
using Backup.Web.Api.Server.Models.Entities.SaaS;
using Backup.Web.Api.Server.Services.Stock;
using Moq;

namespace Backup.Web.Api.Tests.Business
{
    /// <summary>RG-CS2 / ATP : stock disponible et AllowNegativeStock.</summary>
    public class P1StockAvailabilityTests
    {
        [Fact]
        public void ValidateAvailable_Fails_WhenInsufficientStock()
        {
            var storage = CreateStorage(
                allowNegative: false,
                new StockItem { ProductKey = "SKU1", CompanyId = "c1", QuantityOnHand = 5, ReservedQuantity = 1 });

            var err = StockLedger.ValidateAvailable(storage.Object, "c1", "SKU1", 5m);

            Assert.NotNull(err);
            Assert.Contains("insuffisant", err);
        }

        [Fact]
        public void ValidateAvailable_Allows_WhenStockCoversRequirement()
        {
            var storage = CreateStorage(
                allowNegative: false,
                new StockItem { ProductKey = "SKU1", CompanyId = "c1", QuantityOnHand = 10, ReservedQuantity = 2 });

            Assert.Null(StockLedger.ValidateAvailable(storage.Object, "c1", "SKU1", 8m));
        }

        [Fact]
        public void ValidateAvailable_SkipsCheck_WhenNegativeStockAllowed()
        {
            var storage = CreateStorage(
                allowNegative: true,
                new StockItem { ProductKey = "SKU1", CompanyId = "c1", QuantityOnHand = 0, ReservedQuantity = 0 });

            Assert.Null(StockLedger.ValidateAvailable(storage.Object, "c1", "SKU1", 100m));
        }

        [Fact]
        public void ValidateAvailable_SkipsShippingFeeKeys()
        {
            var storage = CreateStorage(allowNegative: false);

            Assert.Null(StockLedger.ValidateAvailable(storage.Object, "c1", "FDP", 999m));
        }

        [Fact]
        public void ValidateAvailable_IncludesOwnReservationExtra()
        {
            var storage = CreateStorage(
                allowNegative: false,
                new StockItem { ProductKey = "SKU1", CompanyId = "c1", QuantityOnHand = 5, ReservedQuantity = 5 });

            // ATP = 0, but own reservation of 5 can be reused for the same order line.
            Assert.Null(StockLedger.ValidateAvailable(storage.Object, "c1", "SKU1", 5m, extraAvailableFromOwnReservation: 5m));
        }

        private static Mock<IStorageBroker> CreateStorage(bool allowNegative, params StockItem[] items)
        {
            var storage = new Mock<IStorageBroker>();
            storage.Setup(s => s.SelectAllStock()).Returns(items.AsQueryable());
            storage.Setup(s => s.SelectAllCompanies()).Returns(new[]
            {
                new Company { Id = "c1", AllowNegativeStock = allowNegative }
            }.AsQueryable());
            return storage;
        }
    }
}
