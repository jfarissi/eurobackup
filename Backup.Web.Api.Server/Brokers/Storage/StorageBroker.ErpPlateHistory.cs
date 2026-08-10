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
        public DbSet<ErpPlateHistory> ErpPlateHistories { get; set; } = null!;

        public IQueryable<ErpPlateHistory> SelectAllErpPlateHistories() =>
            this.ErpPlateHistories.AsQueryable();

        public async ValueTask<ErpPlateHistory> InsertErpPlateHistoryAsync(ErpPlateHistory history)
        {
            if (history.Id == Guid.Empty) history.Id = Guid.NewGuid();
            if (history.SearchedAt == default) history.SearchedAt = DateTime.UtcNow;
            EntityEntry<ErpPlateHistory> entry = await this.ErpPlateHistories.AddAsync(history);
            await this.SaveChangesAsync();
            return entry.Entity;
        }
    }
}
