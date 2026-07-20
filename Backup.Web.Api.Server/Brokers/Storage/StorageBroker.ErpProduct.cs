using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Backup.Web.Api.Server.Brokers.Storage
{
    public partial class StorageBroker
    {
        public DbSet<ErpProduct> ErpProducts { get; set; } = null!;
        public DbSet<ErpProductChangeLog> ErpProductChangeLogs { get; set; } = null!;
        public DbSet<ErpSyncLog> ErpSyncLogs { get; set; } = null!;
        public DbSet<ErpBrand> ErpBrands { get; set; } = null!;
        public DbSet<ErpCategory> ErpCategories { get; set; } = null!;

        public async ValueTask<ErpProduct> InsertErpProductAsync(ErpProduct product)
        {
            EntityEntry<ErpProduct> entry = await this.ErpProducts.AddAsync(product);
            await this.SaveChangesAsync();
            return entry.Entity;
        }

        public async ValueTask<ErpProduct> StageInsertErpProductAsync(ErpProduct product)
        {
            EntityEntry<ErpProduct> entry = await this.ErpProducts.AddAsync(product);
            return entry.Entity;
        }

        public void StageUpdateErpProduct(ErpProduct product) =>
            this.ErpProducts.Update(product);

        public Task FlushChangesAsync(CancellationToken cancellationToken = default) =>
            this.SaveChangesAsync(cancellationToken);

        public IQueryable<ErpProduct> SelectAllErpProducts() => this.ErpProducts.AsQueryable();

        public async ValueTask<ErpProduct?> SelectErpProductByIdAsync(int id) =>
            await this.ErpProducts.FindAsync(id);

        public async ValueTask<ErpProduct?> SelectErpProductByErpIdAsync(string erpProductId) =>
            await this.ErpProducts.AsNoTracking()
                .FirstOrDefaultAsync(p => p.ErpProductId == erpProductId);

        public async ValueTask<ErpProduct> UpdateErpProductAsync(ErpProduct product)
        {
            EntityEntry<ErpProduct> entry = this.ErpProducts.Update(product);
            await this.SaveChangesAsync();
            return entry.Entity;
        }

        public async ValueTask<ErpProductChangeLog> InsertErpProductChangeLogAsync(ErpProductChangeLog changeLog)
        {
            EntityEntry<ErpProductChangeLog> entry = await this.ErpProductChangeLogs.AddAsync(changeLog);
            await this.SaveChangesAsync();
            return entry.Entity;
        }

        public async ValueTask InsertErpProductChangeLogsAsync(IEnumerable<ErpProductChangeLog> changeLogs)
        {
            await this.ErpProductChangeLogs.AddRangeAsync(changeLogs);
            await this.SaveChangesAsync();
        }

        public IQueryable<ErpProductChangeLog> SelectAllErpProductChangeLogs() =>
            this.ErpProductChangeLogs.AsQueryable();

        public async ValueTask MarkErpProductChangeLogsAsReadAsync(IEnumerable<int> changeLogIds)
        {
            var ids = changeLogIds.ToList();
            if (ids.Count == 0)
                return;

            var logs = await this.ErpProductChangeLogs
                .Where(c => ids.Contains(c.Id) && !c.IsRead)
                .ToListAsync();

            foreach (var log in logs)
                log.IsRead = true;

            await this.SaveChangesAsync();
        }

        public async ValueTask<int> DeleteErpProductChangeLogsAsync(IEnumerable<int> changeLogIds)
        {
            var ids = changeLogIds.ToList();
            if (ids.Count == 0)
                return 0;

            var logs = await this.ErpProductChangeLogs
                .Where(c => ids.Contains(c.Id))
                .ToListAsync();

            if (logs.Count == 0)
                return 0;

            this.ErpProductChangeLogs.RemoveRange(logs);
            await this.SaveChangesAsync();
            return logs.Count;
        }

        public async ValueTask<ErpSyncLog> InsertErpSyncLogAsync(ErpSyncLog syncLog)
        {
            EntityEntry<ErpSyncLog> entry = await this.ErpSyncLogs.AddAsync(syncLog);
            await this.SaveChangesAsync();
            return entry.Entity;
        }

        public async ValueTask<ErpSyncLog> UpdateErpSyncLogAsync(ErpSyncLog syncLog)
        {
            EntityEntry<ErpSyncLog> entry = this.ErpSyncLogs.Update(syncLog);
            await this.SaveChangesAsync();
            return entry.Entity;
        }

        public IQueryable<ErpSyncLog> SelectAllErpSyncLogs() => this.ErpSyncLogs.AsQueryable();

        public async ValueTask<ErpSyncLog?> SelectErpSyncLogByJobIdAsync(string jobId) =>
            await this.ErpSyncLogs.AsNoTracking()
                .FirstOrDefaultAsync(s => s.JobId == jobId);

        public IQueryable<ErpBrand> SelectAllErpBrands() => this.ErpBrands.AsQueryable();

        public async ValueTask StageInsertErpBrandAsync(ErpBrand brand)
        {
            await this.ErpBrands.AddAsync(brand);
        }

        public void StageUpdateErpBrand(ErpBrand brand) =>
            this.ErpBrands.Update(brand);

        public async ValueTask<ErpBrand> InsertErpBrandAsync(ErpBrand brand)
        {
            var entry = await this.ErpBrands.AddAsync(brand);
            await this.SaveChangesAsync();
            return entry.Entity;
        }

        public async ValueTask<ErpBrand> UpdateErpBrandAsync(ErpBrand brand)
        {
            var entry = this.ErpBrands.Update(brand);
            await this.SaveChangesAsync();
            return entry.Entity;
        }

        public IQueryable<ErpCategory> SelectAllErpCategories() => this.ErpCategories.AsQueryable();

        public async ValueTask StageInsertErpCategoryAsync(ErpCategory category)
        {
            await this.ErpCategories.AddAsync(category);
        }

        public void StageUpdateErpCategory(ErpCategory category) =>
            this.ErpCategories.Update(category);

        public async ValueTask<ErpCategory> InsertErpCategoryAsync(ErpCategory category)
        {
            var entry = await this.ErpCategories.AddAsync(category);
            await this.SaveChangesAsync();
            return entry.Entity;
        }

        public async ValueTask<ErpCategory> UpdateErpCategoryAsync(ErpCategory category)
        {
            var entry = this.ErpCategories.Update(category);
            await this.SaveChangesAsync();
            return entry.Entity;
        }
    }
}
