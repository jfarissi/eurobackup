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
        public DbSet<ErpVinVehicle> ErpVinVehicles { get; set; } = null!;

        public IQueryable<ErpVinVehicle> SelectAllErpVinVehicles() =>
            this.ErpVinVehicles.AsQueryable();

        public async ValueTask<ErpVinVehicle?> SelectErpVinVehicleByVinAsync(string vin)
        {
            var key = (vin ?? string.Empty).Trim().ToUpperInvariant();
            if (key.Length == 0) return null;
            return await this.ErpVinVehicles.FirstOrDefaultAsync(v => v.Vin == key);
        }

        public async ValueTask<ErpVinVehicle> UpsertErpVinVehicleAsync(ErpVinVehicle row)
        {
            var key = (row.Vin ?? string.Empty).Trim().ToUpperInvariant();
            row.Vin = key;
            var existing = await SelectErpVinVehicleByVinAsync(key);
            if (existing == null)
            {
                if (row.Id == Guid.Empty) row.Id = Guid.NewGuid();
                row.CreatedAt = DateTime.UtcNow;
                row.UpdatedAt = row.CreatedAt;
                row.HitCount = Math.Max(1, row.HitCount);
                row.LastHitAt = row.CreatedAt;
                EntityEntry<ErpVinVehicle> entry = await this.ErpVinVehicles.AddAsync(row);
                await this.SaveChangesAsync();
                return entry.Entity;
            }

            existing.Make = row.Make ?? existing.Make;
            existing.Model = row.Model ?? existing.Model;
            existing.Year = row.Year ?? existing.Year;
            existing.EngineCode = row.EngineCode ?? existing.EngineCode;
            existing.FuelType = row.FuelType ?? existing.FuelType;
            existing.PowerHP = row.PowerHP ?? existing.PowerHP;
            existing.ExternalVehicleId = row.ExternalVehicleId ?? existing.ExternalVehicleId;
            existing.ExternalModelId = row.ExternalModelId ?? existing.ExternalModelId;
            existing.ExternalManufacturerId = row.ExternalManufacturerId ?? existing.ExternalManufacturerId;
            if (!string.IsNullOrWhiteSpace(row.Source)) existing.Source = row.Source;
            if (!string.IsNullOrWhiteSpace(row.RawJson)) existing.RawJson = row.RawJson;
            if (!string.IsNullOrWhiteSpace(row.CompanyId)) existing.CompanyId = row.CompanyId;
            existing.UpdatedAt = DateTime.UtcNow;
            existing.HitCount += 1;
            existing.LastHitAt = existing.UpdatedAt;
            await this.SaveChangesAsync();
            return existing;
        }

        public async ValueTask TouchErpVinVehicleHitAsync(ErpVinVehicle row)
        {
            row.HitCount += 1;
            row.LastHitAt = DateTime.UtcNow;
            await this.SaveChangesAsync();
        }
    }
}
