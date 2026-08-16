using System;
using Backup.Web.Api.Server.Services.Stock;

namespace Backup.Web.Api.Tests.Business
{
    public class StockForecastCalculatorTests
    {
        private static readonly DateTime Now = new(2026, 8, 14, 12, 0, 0, DateTimeKind.Utc);

        [Fact]
        public void High_velocity_low_cover_is_critical()
        {
            var r = StockForecastCalculator.Compute(
                onHand: 3, reserved: 0, minStock: 20,
                qtyOutLookback: 14, qtyOutLast7: 7, qtyOutPrev21: 7,
                nowUtc: Now);

            Assert.Equal(StockForecastRisk.Critical, r.Risk);
            Assert.Equal(3m, r.Available);
            Assert.Equal(0.5m, r.AvgDailyOut);
            Assert.Equal(6.0m, r.DaysOfCover);
            Assert.Equal(StockForecastTrend.Up, r.Trend);
            Assert.Equal(4m, r.SuggestedQty);
        }

        [Fact]
        public void Cover_between_7_and_horizon_is_warning()
        {
            var r = StockForecastCalculator.Compute(
                onHand: 10, reserved: 0, minStock: 0,
                qtyOutLookback: 28, qtyOutLast7: 7, qtyOutPrev21: 21,
                nowUtc: Now);

            Assert.Equal(StockForecastRisk.Warning, r.Risk);
            Assert.Equal(10.0m, r.DaysOfCover);
            Assert.Equal(StockForecastTrend.Stable, r.Trend);
        }

        [Fact]
        public void MinStock_breach_without_cover_risk_is_watch()
        {
            var r = StockForecastCalculator.Compute(
                onHand: 20, reserved: 0, minStock: 25,
                qtyOutLookback: 28, qtyOutLast7: 7, qtyOutPrev21: 21,
                nowUtc: Now);

            Assert.Equal(StockForecastRisk.Watch, r.Risk);
            Assert.Equal(20.0m, r.DaysOfCover);
        }

        [Fact]
        public void No_velocity_is_ok()
        {
            var r = StockForecastCalculator.Compute(
                onHand: 8, reserved: 0, minStock: 0,
                qtyOutLookback: 0, qtyOutLast7: 0, qtyOutPrev21: 0,
                nowUtc: Now);

            Assert.Equal(StockForecastRisk.Ok, r.Risk);
            Assert.Null(r.DaysOfCover);
            Assert.Equal(StockForecastTrend.Stable, r.Trend);
        }

        [Fact]
        public void Zero_available_with_demand_is_critical()
        {
            var r = StockForecastCalculator.Compute(
                onHand: 0, reserved: 0, minStock: 0,
                qtyOutLookback: 14, qtyOutLast7: 7, qtyOutPrev21: 7,
                nowUtc: Now);

            Assert.Equal(StockForecastRisk.Critical, r.Risk);
            Assert.Equal(Now, r.StockoutAt);
        }

        [Fact]
        public void Demand_drop_is_down_trend()
        {
            var r = StockForecastCalculator.Compute(
                onHand: 50, reserved: 0, minStock: 0,
                qtyOutLookback: 28, qtyOutLast7: 1, qtyOutPrev21: 27,
                nowUtc: Now);

            Assert.Equal(StockForecastTrend.Down, r.Trend);
            Assert.Equal(StockForecastRisk.Ok, r.Risk);
        }
    }
}
