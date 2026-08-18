using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Models.Entities;
using Backup.Web.Api.Server.Models.Entities.Accounting;
using Backup.Web.Api.Server.Services.Accounting;
using Backup.Web.Api.Server.Services.Numbering;
using Moq;

namespace Backup.Web.Api.Tests.Business
{
    public class DepreciationServiceTests
    {
        [Fact]
        public void Linear_FullFirstMonth_EqualsMonthlyCharges()
        {
            var plan = DepreciationCalculator.Build(
                new DateTime(2026, 1, 1), 1200m, 0m, 12, DepreciationCalculator.ModeLinear);
            Assert.Equal(12, plan.Count);
            Assert.All(plan.Take(11), p => Assert.Equal(100m, p.Charge));
            Assert.Equal(1200m, plan.Sum(p => p.Charge));
            Assert.Equal(0m, plan.Last().NetBookValue);
        }

        [Fact]
        public void Linear_ProrataFirstMonth_LastTakesResidual()
        {
            var plan = DepreciationCalculator.Build(
                new DateTime(2026, 1, 16), 1200m, 0m, 12, DepreciationCalculator.ModeLinear);
            Assert.Equal(12, plan.Count);
            Assert.True(plan[0].Charge < 100m);
            Assert.Equal(1200m, plan.Sum(p => p.Charge));
            Assert.Equal(0m, plan.Last().NetBookValue);
            Assert.Equal(100m * 16 / 31, plan[0].Charge, 2);
        }

        [Fact]
        public void Linear_NeverExceedsAmortizableBase()
        {
            var plan = DepreciationCalculator.Build(
                new DateTime(2026, 3, 10), 10000m, 1000m, 36, DepreciationCalculator.ModeLinear);
            Assert.Equal(9000m, plan.Sum(p => p.Charge));
            Assert.Equal(1000m, plan.Last().NetBookValue);
            Assert.True(plan.Max(p => p.Accumulated) <= 9000m);
        }

        [Fact]
        public void Declining_ChargesDecreaseThenSwitchToLinear()
        {
            var plan = DepreciationCalculator.Build(
                new DateTime(2026, 1, 1), 12000m, 0m, 36, DepreciationCalculator.ModeDeclining);
            Assert.True(plan.Count >= 24);
            Assert.Equal(12000m, plan.Sum(p => p.Charge));
            Assert.True(plan[0].Charge > plan[12].Charge);
            Assert.Equal(0m, plan.Last().NetBookValue);
        }

        private sealed class FakeAssetStorage
        {
            public List<FixedAsset> Assets { get; } = new();
            public List<AccountingEntry> Entries { get; } = new();
            public List<FiscalPeriod> Periods { get; } = new();
            public List<Journal> Journals { get; } = new();
            public List<CompanyAccountingSettings> Settings { get; } = new();
            public Mock<IStorageBroker> Broker { get; }

            public FakeAssetStorage()
            {
                this.Broker = new Mock<IStorageBroker>();
                this.Broker.Setup(s => s.SelectAllFixedAssets()).Returns(() => this.Assets.AsQueryable());
                this.Broker.Setup(s => s.SelectAllAccountingEntries()).Returns(() => this.Entries.AsQueryable());
                this.Broker.Setup(s => s.SelectAllFiscalPeriods()).Returns(() => this.Periods.AsQueryable());
                this.Broker.Setup(s => s.SelectAllJournals()).Returns(() => this.Journals.AsQueryable());
                this.Broker.Setup(s => s.SelectAllCompanyAccountingSettings()).Returns(() => this.Settings.AsQueryable());
                this.Broker.Setup(s => s.InsertFixedAssetAsync(It.IsAny<FixedAsset>()))
                    .ReturnsAsync((FixedAsset a) =>
                    {
                        a.Id = this.Assets.Count + 1;
                        var lineId = 1;
                        foreach (var line in a.Schedule) line.Id = lineId++;
                        this.Assets.Add(a);
                        return a;
                    });
                this.Broker.Setup(s => s.UpdateFixedAssetAsync(It.IsAny<FixedAsset>()))
                    .ReturnsAsync((FixedAsset a) => a);
                this.Broker.Setup(s => s.InsertAccountingEntryAsync(It.IsAny<AccountingEntry>()))
                    .ReturnsAsync((AccountingEntry e) =>
                    {
                        e.Id = this.Entries.Count + 1;
                        this.Entries.Add(e);
                        return e;
                    });
            }
        }

        [Fact]
        public async Task PostMonth_CreatesBalancedOdAndMarksLines()
        {
            var storage = new FakeAssetStorage();
            var numbering = new Mock<INumberingSequenceService>();
            numbering.Setup(n => n.GetNextNumberAsync(It.IsAny<string>(), It.IsAny<string?>()))
                .ReturnsAsync("EC-IM01");

            var (created, error) = await FixedAssetService.CreateAsync(
                storage.Broker.Object, "c1",
                new FixedAssetService.AssetForm
                {
                    Designation = "PC bureau",
                    AcquisitionDate = new DateTime(2026, 1, 1),
                    ServiceDate = new DateTime(2026, 1, 1),
                    OriginValue = 1200m,
                    DurationMonths = 12,
                    Mode = "Lineaire"
                }, "Alice");
            Assert.Null(error);
            Assert.Equal(12, created!.Schedule.Count);

            var (result, postError) = await FixedAssetService.PostMonthAsync(
                storage.Broker.Object, numbering.Object, "c1", 2026, 1, "Alice");
            Assert.Null(postError);
            Assert.Equal(1, result!.PostedLines);
            Assert.Equal(2, storage.Entries.Single().Lines.Count);
            Assert.Equal(0m, storage.Entries.Single().Lines.Sum(l => l.Debit - l.Credit));
            Assert.True(storage.Assets.Single().Schedule.Single(s => s.Month == 1).IsPosted);
        }
    }
}
