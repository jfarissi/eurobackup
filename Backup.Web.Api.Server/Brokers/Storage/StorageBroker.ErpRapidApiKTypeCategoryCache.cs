using System;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Models.Catalog;
using Microsoft.EntityFrameworkCore;

namespace Backup.Web.Api.Server.Brokers.Storage
{
    public partial class StorageBroker
    {
        public DbSet<ErpRapidApiKTypeCategoryCache> ErpRapidApiKTypeCategoryCaches { get; set; } = null!;

        public async ValueTask<ErpRapidApiKTypeCategoryCache?> SelectRapidApiKTypeCategoryCacheAsync(string kType)
        {
            var key = (kType ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(key)) return null;
            return await this.ErpRapidApiKTypeCategoryCaches.AsNoTracking()
                .FirstOrDefaultAsync(c => c.KType == key);
        }

        public async ValueTask UpsertRapidApiKTypeCategoryCacheAsync(
            string kType, string categoriesJson, int categoryCount)
        {
            var key = (kType ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(categoriesJson))
                return;

            var now = DateTime.UtcNow;
            var existing = await this.ErpRapidApiKTypeCategoryCaches.FirstOrDefaultAsync(c => c.KType == key);
            if (existing == null)
            {
                await this.ErpRapidApiKTypeCategoryCaches.AddAsync(new ErpRapidApiKTypeCategoryCache
                {
                    Id = Guid.NewGuid(),
                    KType = key,
                    CategoriesJson = categoriesJson,
                    CategoryCount = categoryCount,
                    FetchedAt = now,
                    UpdatedAt = now
                });
            }
            else
            {
                existing.CategoriesJson = categoriesJson;
                existing.CategoryCount = categoryCount;
                existing.FetchedAt = now;
                existing.UpdatedAt = now;
            }

            await this.SaveChangesAsync();
        }
    }
}
