using System;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Models.Entities.SaaS;
using Backup.Web.Api.Server.Services.Accounting;
using Moq;

namespace Backup.Web.Api.Tests.Business
{
    /// <summary>RG-CO3 : exercice comptable ouvert.</summary>
    public class P1AccountingFiscalPeriodTests
    {
        [Fact]
        public async Task ValidateOpenFiscalPeriodAsync_ReturnsNull_WhenCompanyIdMissing()
        {
            var storage = new Mock<IStorageBroker>();

            var err = await AccountingLedger.ValidateOpenFiscalPeriodAsync(storage.Object, null, DateTime.UtcNow);

            Assert.Null(err);
        }

        [Fact]
        public async Task ValidateOpenFiscalPeriodAsync_ReturnsNull_WhenNoBounds()
        {
            var storage = new Mock<IStorageBroker>();
            storage.Setup(s => s.SelectCompanyByIdAsync("c1"))
                .ReturnsAsync(new Company { Id = "c1" });

            var err = await AccountingLedger.ValidateOpenFiscalPeriodAsync(storage.Object, "c1", new DateTime(2026, 6, 1));

            Assert.Null(err);
        }

        [Fact]
        public async Task ValidateOpenFiscalPeriodAsync_Fails_WhenBeforeStart()
        {
            var storage = new Mock<IStorageBroker>();
            storage.Setup(s => s.SelectCompanyByIdAsync("c1"))
                .ReturnsAsync(new Company
                {
                    Id = "c1",
                    OpenFiscalPeriodStart = new DateTime(2026, 1, 1),
                    OpenFiscalPeriodEnd = new DateTime(2026, 12, 31)
                });

            var err = await AccountingLedger.ValidateOpenFiscalPeriodAsync(
                storage.Object, "c1", new DateTime(2025, 12, 31));

            Assert.NotNull(err);
            Assert.Contains("antérieure", err);
        }

        [Fact]
        public async Task ValidateOpenFiscalPeriodAsync_Fails_WhenAfterEnd()
        {
            var storage = new Mock<IStorageBroker>();
            storage.Setup(s => s.SelectCompanyByIdAsync("c1"))
                .ReturnsAsync(new Company
                {
                    Id = "c1",
                    OpenFiscalPeriodStart = new DateTime(2026, 1, 1),
                    OpenFiscalPeriodEnd = new DateTime(2026, 12, 31)
                });

            var err = await AccountingLedger.ValidateOpenFiscalPeriodAsync(
                storage.Object, "c1", new DateTime(2027, 1, 1));

            Assert.NotNull(err);
            Assert.Contains("postérieure", err);
        }

        [Fact]
        public async Task ValidateOpenFiscalPeriodAsync_Allows_DateInsideWindow()
        {
            var storage = new Mock<IStorageBroker>();
            storage.Setup(s => s.SelectCompanyByIdAsync("c1"))
                .ReturnsAsync(new Company
                {
                    Id = "c1",
                    OpenFiscalPeriodStart = new DateTime(2026, 1, 1),
                    OpenFiscalPeriodEnd = new DateTime(2026, 12, 31)
                });

            var err = await AccountingLedger.ValidateOpenFiscalPeriodAsync(
                storage.Object, "c1", new DateTime(2026, 7, 15));

            Assert.Null(err);
        }
    }
}
