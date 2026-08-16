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
        public DbSet<ErpKTypeEnrichmentQueue> ErpKTypeEnrichmentQueues { get; set; } = null!;

        public async ValueTask<bool> ErpProductVehicleKTypeExistsAsync(string kType)
        {
            var k = (kType ?? string.Empty).Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(k)) return false;

            return await this.ErpProductVehicles.AsNoTracking()
                .AnyAsync(v => v.KType != null && v.KType.ToLower() == k);
        }

        public async ValueTask<ErpKTypeEnrichmentQueue> EnqueueErpKTypeEnrichmentAsync(
            ErpKTypeEnrichmentQueue row)
        {
            var kType = (row.KType ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(kType))
                throw new ArgumentException("K-Type requis.", nameof(row));

            row.KType = kType;
            if (!string.IsNullOrWhiteSpace(row.Vin))
                row.Vin = row.Vin.Trim().ToUpperInvariant();

            var existing = await this.ErpKTypeEnrichmentQueues
                .FirstOrDefaultAsync(q => q.KType == kType);

            var now = DateTime.UtcNow;
            if (existing == null)
            {
                if (row.Id == Guid.Empty) row.Id = Guid.NewGuid();
                row.CreatedAt = now;
                row.UpdatedAt = now;
                row.LastRequestedAt = now;
                row.HitCount = Math.Max(1, row.HitCount);
                if (string.IsNullOrWhiteSpace(row.Status)) row.Status = "Pending";
                EntityEntry<ErpKTypeEnrichmentQueue> entry =
                    await this.ErpKTypeEnrichmentQueues.AddAsync(row);
                await this.SaveChangesAsync();
                return entry.Entity;
            }

            existing.HitCount += 1;
            existing.LastRequestedAt = now;
            existing.UpdatedAt = now;
            existing.Vin = row.Vin ?? existing.Vin;
            existing.Make = row.Make ?? existing.Make;
            existing.Model = row.Model ?? existing.Model;
            existing.Year = row.Year ?? existing.Year;
            existing.EngineCode = row.EngineCode ?? existing.EngineCode;
            if (!string.IsNullOrWhiteSpace(row.Source)) existing.Source = row.Source;
            if (!string.IsNullOrWhiteSpace(row.CompanyId)) existing.CompanyId = row.CompanyId;
            if (string.Equals(existing.Status, "Done", StringComparison.OrdinalIgnoreCase))
                existing.Status = "Pending";

            await this.SaveChangesAsync();
            return existing;
        }

        public async ValueTask UpdateErpKTypeEnrichmentStatusAsync(
            string kType, string status, int? productsImported)
        {
            var key = (kType ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(key)) return;

            var row = await this.ErpKTypeEnrichmentQueues.FirstOrDefaultAsync(q => q.KType == key);
            if (row == null) return;

            row.Status = string.IsNullOrWhiteSpace(status) ? row.Status : status.Trim();
            row.UpdatedAt = DateTime.UtcNow;
            if (productsImported.HasValue && productsImported.Value > 0 && string.Equals(status, "Done", StringComparison.OrdinalIgnoreCase))
                row.SyncedAt = row.UpdatedAt;

            await this.SaveChangesAsync();
        }
    }
}
