using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Services.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Backup.Web.Api.Server.Services.Stock
{
    public interface IStockForecastService
    {
        Task<StockForecastResultDto> GetForecastAsync(
            string? companyId,
            int horizonDays = StockForecastCalculator.DefaultHorizonDays,
            bool all = false,
            CancellationToken ct = default);
    }

    public sealed class StockForecastResultDto
    {
        public int LookbackDays { get; set; } = StockForecastCalculator.DefaultLookbackDays;
        public int HorizonDays { get; set; } = StockForecastCalculator.DefaultHorizonDays;
        public int CriticalCount { get; set; }
        public int WarningCount { get; set; }
        public int WatchCount { get; set; }
        public List<StockForecastLineDto> Items { get; set; } = new();
    }

    public sealed class StockForecastLineDto
    {
        public int StockItemId { get; set; }
        public string ProductKey { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Supplier { get; set; }
        public decimal QuantityOnHand { get; set; }
        public decimal ReservedQuantity { get; set; }
        public decimal Available { get; set; }
        public decimal MinStock { get; set; }
        public decimal QtyOutLookback { get; set; }
        public decimal AvgDailyOut { get; set; }
        public decimal? DaysOfCover { get; set; }
        public decimal DynamicMin { get; set; }
        public decimal SuggestedQty { get; set; }
        public string Risk { get; set; } = StockForecastRisk.Ok;
        public string Trend { get; set; } = StockForecastTrend.Stable;
        public DateTime? StockoutAt { get; set; }
    }

    public sealed class StockForecastService : IStockForecastService
    {
        private readonly IStorageBroker storage;

        public StockForecastService(IStorageBroker storage) => this.storage = storage;

        public async Task<StockForecastResultDto> GetForecastAsync(
            string? companyId,
            int horizonDays = StockForecastCalculator.DefaultHorizonDays,
            bool all = false,
            CancellationToken ct = default)
        {
            horizonDays = StockForecastCalculator.ClampHorizonDays(horizonDays);
            var lookbackDays = StockForecastCalculator.DefaultLookbackDays;
            var now = DateTime.UtcNow;
            var lookbackStart = now.AddDays(-lookbackDays);
            var last7Start = now.AddDays(-7);

            var stocks = await this.storage.SelectAllStock()
                .ForCompany(companyId)
                .AsNoTracking()
                .ToListAsync(ct);

            var outs = await this.storage.SelectAllStockMovements()
                .ForCompany(companyId)
                .AsNoTracking()
                .Where(m => m.CreatedAt >= lookbackStart && m.MovementType == "Out")
                .Select(m => new { m.ProductKey, m.Quantity, m.CreatedAt })
                .ToListAsync(ct);

            var result = new StockForecastResultDto
            {
                LookbackDays = lookbackDays,
                HorizonDays = horizonDays
            };

            var lines = new List<StockForecastLineDto>(stocks.Count);
            foreach (var item in stocks)
            {
                if (StockLedger.IsShippingFeeKey(item.ProductKey))
                    continue;

                decimal qtyLookback = 0, qtyLast7 = 0, qtyPrev21 = 0;
                foreach (var m in outs)
                {
                    if (!StockLedger.ProductKeysMatch(m.ProductKey, item.ProductKey))
                        continue;
                    var qty = Math.Abs(m.Quantity);
                    qtyLookback += qty;
                    if (m.CreatedAt >= last7Start)
                        qtyLast7 += qty;
                    else
                        qtyPrev21 += qty;
                }

                var computed = StockForecastCalculator.Compute(
                    item.QuantityOnHand,
                    item.ReservedQuantity,
                    item.MinStock,
                    qtyLookback,
                    qtyLast7,
                    qtyPrev21,
                    lookbackDays,
                    horizonDays,
                    now);

                var line = new StockForecastLineDto
                {
                    StockItemId = item.Id,
                    ProductKey = item.ProductKey,
                    Description = item.Description,
                    Supplier = item.Supplier,
                    QuantityOnHand = item.QuantityOnHand,
                    ReservedQuantity = item.ReservedQuantity,
                    Available = computed.Available,
                    MinStock = item.MinStock,
                    QtyOutLookback = qtyLookback,
                    AvgDailyOut = computed.AvgDailyOut,
                    DaysOfCover = computed.DaysOfCover,
                    DynamicMin = computed.DynamicMin,
                    SuggestedQty = computed.SuggestedQty,
                    Risk = computed.Risk,
                    Trend = computed.Trend,
                    StockoutAt = computed.StockoutAt
                };

                if (line.Risk == StockForecastRisk.Critical) result.CriticalCount++;
                else if (line.Risk == StockForecastRisk.Warning) result.WarningCount++;
                else if (line.Risk == StockForecastRisk.Watch) result.WatchCount++;

                if (all || StockForecastCalculator.IsAtRisk(line.Risk))
                    lines.Add(line);
            }

            result.Items = lines
                .OrderBy(l => StockForecastCalculator.RiskRank(l.Risk))
                .ThenBy(l => l.DaysOfCover ?? decimal.MaxValue)
                .ThenBy(l => l.ProductKey, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return result;
        }
    }
}
