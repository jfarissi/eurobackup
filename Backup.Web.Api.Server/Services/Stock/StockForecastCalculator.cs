using System;

namespace Backup.Web.Api.Server.Services.Stock
{
    /// <summary>
    /// Baseline F7 (pas de ML) : vitesse Out 28 j, couverture, seuil dynamique, tendance 7 j vs 21 j.
    /// </summary>
    public static class StockForecastCalculator
    {
        public const int DefaultLookbackDays = 28;
        public const int DefaultHorizonDays = 14;
        public const int CriticalCoverDays = 7;
        public const decimal VelocityEpsilon = 0.001m;
        public const decimal TrendUpFactor = 1.25m;
        public const decimal TrendDownFactor = 0.75m;

        public static int ClampHorizonDays(int horizonDays)
        {
            if (horizonDays < 7) return 7;
            if (horizonDays > 90) return 90;
            return horizonDays;
        }

        public static StockForecastComputeResult Compute(
            decimal onHand,
            decimal reserved,
            decimal minStock,
            decimal qtyOutLookback,
            decimal qtyOutLast7,
            decimal qtyOutPrev21,
            int lookbackDays = DefaultLookbackDays,
            int horizonDays = DefaultHorizonDays,
            DateTime? nowUtc = null)
        {
            lookbackDays = lookbackDays <= 0 ? DefaultLookbackDays : lookbackDays;
            horizonDays = ClampHorizonDays(horizonDays);
            var now = nowUtc ?? DateTime.UtcNow;

            var available = Math.Max(0m, onHand - reserved);
            var avgDaily = qtyOutLookback / lookbackDays;
            if (avgDaily < 0) avgDaily = 0;

            var last7Daily = qtyOutLast7 / 7m;
            var prev21Daily = qtyOutPrev21 / 21m;
            var trend = ResolveTrend(last7Daily, prev21Daily);

            var hasVelocity = avgDaily >= VelocityEpsilon;
            decimal? daysOfCover = hasVelocity ? decimal.Round(available / avgDaily, 1) : null;
            var dynamicMin = decimal.Round(avgDaily * horizonDays, 2);
            var suggestedQty = Math.Max(0m, decimal.Round(dynamicMin - available, 2));
            var risk = ResolveRisk(available, minStock, daysOfCover, hasVelocity, horizonDays);

            DateTime? stockoutAt = null;
            if (hasVelocity)
            {
                if (available <= 0)
                    stockoutAt = now;
                else if (daysOfCover.HasValue)
                    stockoutAt = now.AddDays((double)daysOfCover.Value);
            }

            return new StockForecastComputeResult
            {
                Available = available,
                AvgDailyOut = decimal.Round(avgDaily, 4),
                DaysOfCover = daysOfCover,
                DynamicMin = dynamicMin,
                SuggestedQty = suggestedQty,
                Risk = risk,
                Trend = trend,
                StockoutAt = stockoutAt
            };
        }

        public static int RiskRank(string risk) => risk switch
        {
            StockForecastRisk.Critical => 0,
            StockForecastRisk.Warning => 1,
            StockForecastRisk.Watch => 2,
            _ => 3
        };

        public static bool IsAtRisk(string risk) =>
            risk is StockForecastRisk.Critical or StockForecastRisk.Warning or StockForecastRisk.Watch;

        private static string ResolveTrend(decimal last7Daily, decimal prev21Daily)
        {
            if (prev21Daily < VelocityEpsilon)
                return last7Daily >= VelocityEpsilon ? StockForecastTrend.Up : StockForecastTrend.Stable;
            if (last7Daily > prev21Daily * TrendUpFactor) return StockForecastTrend.Up;
            if (last7Daily < prev21Daily * TrendDownFactor) return StockForecastTrend.Down;
            return StockForecastTrend.Stable;
        }

        private static string ResolveRisk(
            decimal available,
            decimal minStock,
            decimal? daysOfCover,
            bool hasVelocity,
            int horizonDays)
        {
            if (hasVelocity && available <= 0)
                return StockForecastRisk.Critical;
            if (daysOfCover.HasValue && daysOfCover.Value < CriticalCoverDays)
                return StockForecastRisk.Critical;
            if (daysOfCover.HasValue && daysOfCover.Value < horizonDays)
                return StockForecastRisk.Warning;
            if (minStock > 0 && available < minStock)
                return StockForecastRisk.Watch;
            return StockForecastRisk.Ok;
        }
    }

    public static class StockForecastRisk
    {
        public const string Critical = "Critical";
        public const string Warning = "Warning";
        public const string Watch = "Watch";
        public const string Ok = "Ok";
    }

    public static class StockForecastTrend
    {
        public const string Up = "Up";
        public const string Down = "Down";
        public const string Stable = "Stable";
    }

    public sealed class StockForecastComputeResult
    {
        public decimal Available { get; init; }
        public decimal AvgDailyOut { get; init; }
        public decimal? DaysOfCover { get; init; }
        public decimal DynamicMin { get; init; }
        public decimal SuggestedQty { get; init; }
        public string Risk { get; init; } = StockForecastRisk.Ok;
        public string Trend { get; init; } = StockForecastTrend.Stable;
        public DateTime? StockoutAt { get; init; }
    }
}
