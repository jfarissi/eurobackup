using System;
using System.Linq;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Models.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Backup.Web.Api.Server.Brokers.Storage
{
    public partial class StorageBroker
    {
        public DbSet<ErpPlateVehicle> ErpPlateVehicles { get; set; } = null!;

        public IQueryable<ErpPlateVehicle> SelectAllErpPlateVehicles() =>
            this.ErpPlateVehicles.AsQueryable();

        public async ValueTask<ErpPlateVehicle?> SelectErpPlateVehicleAsync(
            string companyId, string plateNumber, string country)
        {
            var plate = (plateNumber ?? string.Empty).Trim().ToUpperInvariant();
            var ctry = (country ?? "MA").Trim().ToUpperInvariant();
            return await this.ErpPlateVehicles
                .FirstOrDefaultAsync(v =>
                    v.CompanyId == companyId
                    && v.PlateNumber == plate
                    && v.Country == ctry);
        }

        public async ValueTask<ErpPlateVehicle> UpsertErpPlateVehicleAsync(ErpPlateVehicle row)
        {
            row.PlateNumber = (row.PlateNumber ?? string.Empty).Trim().ToUpperInvariant();
            row.Country = string.IsNullOrWhiteSpace(row.Country)
                ? "MA"
                : row.Country.Trim().ToUpperInvariant();
            if (!string.IsNullOrWhiteSpace(row.Vin))
                row.Vin = row.Vin.Trim().ToUpperInvariant();

            var existing = await SelectErpPlateVehicleAsync(
                row.CompanyId ?? string.Empty, row.PlateNumber, row.Country);

            if (existing == null)
            {
                if (row.Id == Guid.Empty) row.Id = Guid.NewGuid();
                row.CreatedAt = DateTime.UtcNow;
                row.UpdatedAt = row.CreatedAt;
                row.HitCount = Math.Max(1, row.HitCount);
                row.LastHitAt = row.CreatedAt;
                EntityEntry<ErpPlateVehicle> entry = await this.ErpPlateVehicles.AddAsync(row);
                await this.SaveChangesAsync();
                return entry.Entity;
            }

            existing.Vin = row.Vin ?? existing.Vin;
            existing.KType = row.KType ?? existing.KType;
            existing.Make = row.Make ?? existing.Make;
            existing.Model = row.Model ?? existing.Model;
            existing.Year = row.Year ?? existing.Year;
            existing.EngineCode = row.EngineCode ?? existing.EngineCode;
            existing.FuelType = row.FuelType ?? existing.FuelType;
            existing.PowerHP = row.PowerHP ?? existing.PowerHP;
            if (!string.IsNullOrWhiteSpace(row.Source)) existing.Source = row.Source;
            if (row.CustomerId.HasValue) existing.CustomerId = row.CustomerId;
            existing.UpdatedBy = row.UpdatedBy ?? existing.UpdatedBy;
            existing.UpdatedAt = DateTime.UtcNow;
            existing.HitCount += 1;
            existing.LastHitAt = existing.UpdatedAt;
            await this.SaveChangesAsync();
            return existing;
        }

        public async ValueTask TouchErpPlateVehicleHitAsync(ErpPlateVehicle row)
        {
            row.HitCount += 1;
            row.LastHitAt = DateTime.UtcNow;
            row.UpdatedAt = row.LastHitAt.Value;
            await this.SaveChangesAsync();
        }
    }
}
