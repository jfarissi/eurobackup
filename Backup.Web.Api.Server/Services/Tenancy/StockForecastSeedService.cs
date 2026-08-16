using System;
using System.Linq;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Models;
using Backup.Web.Api.Server.Models.Entities;
using Backup.Web.Api.Server.Services.Stock;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Backup.Web.Api.Server.Services.Tenancy
{
    /// <summary>Demo F7 : plaquettes DIAG-PAD à haute vélocité / faible couverture (Critical).</summary>
    public sealed class StockForecastSeedService
    {
        public const string ProductKey = "DIAG-PAD";
        public const string Reason = "forecast-seed";

        private readonly IStorageBroker storage;
        private readonly ILogger<StockForecastSeedService> logger;

        public StockForecastSeedService(IStorageBroker storage, ILogger<StockForecastSeedService> logger)
        {
            this.storage = storage;
            this.logger = logger;
        }

        public async Task EnsureDemoForecastAsync()
        {
            var companyId = TenancySeedService.DefaultCompanyId;

            if (await this.storage.SelectAllStockMovements()
                .AnyAsync(m => m.Reason == Reason && (m.CompanyId == null || m.CompanyId == companyId)))
            {
                this.logger.LogInformation("Forecast seed: movements already present");
                return;
            }

            var stock = StockLedger.FindStockItem(this.storage, companyId, ProductKey);
            if (stock == null)
            {
                stock = await this.storage.InsertStockAsync(new StockItem
                {
                    ProductKey = ProductKey,
                    QuantityOnHand = 3,
                    ReservedQuantity = 0,
                    MinStock = 20,
                    Description = "Plaquettes avant (Demo)",
                    Supplier = "Demo",
                    Unit = "ST",
                    CompanyId = companyId,
                    CreatedBy = Reason,
                    UpdatedBy = Reason,
                    LastUpdated = DateTime.UtcNow
                });
            }
            else if (string.Equals(stock.CreatedBy, Reason, StringComparison.OrdinalIgnoreCase))
            {
                stock.QuantityOnHand = 3;
                stock.ReservedQuantity = 0;
                stock.MinStock = 20;
                stock.UpdatedBy = Reason;
                stock.LastUpdated = DateTime.UtcNow;
                await this.storage.UpdateStockAsync(stock);
            }
            else
            {
                this.logger.LogInformation(
                    "Forecast seed: skip {ProductKey} — existing stock row (CreatedBy={CreatedBy})",
                    ProductKey, stock.CreatedBy);
                return;
            }

            var now = DateTime.UtcNow;
            for (var i = 1; i <= 14; i++)
            {
                await this.storage.InsertStockMovementAsync(new StockMovement
                {
                    ProductKey = stock.ProductKey,
                    MovementType = "Out",
                    Quantity = 1,
                    Reason = Reason,
                    ReferenceDocument = "DEMO-FORECAST",
                    CompanyId = companyId,
                    CreatedBy = Reason,
                    CreatedAt = now.AddDays(-i),
                    UpdatedBy = Reason,
                    UpdatedAt = now
                });
            }

            this.logger.LogInformation(
                "Forecast seed: {ProductKey} on-hand {OnHand} + 14 Out (28j) → couverture Demo Critical",
                stock.ProductKey, stock.QuantityOnHand);
        }
    }
}
