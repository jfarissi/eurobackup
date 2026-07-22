using System;
using System.Linq;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Backup.Web.Api.Server.Brokers.Storage
{
    public partial interface IStorageBroker
    {
        IQueryable<SalesProject> SelectAllSalesProjects();
        ValueTask<SalesProject?> SelectSalesProjectByIdAsync(Guid id);
        ValueTask<SalesProject> InsertSalesProjectAsync(SalesProject project);
        ValueTask<SalesProject> UpdateSalesProjectAsync(SalesProject project);
        ValueTask StageInsertSalesProjectChecklistItemAsync(SalesProjectChecklistItem item);
    }

    public partial class StorageBroker
    {
        public DbSet<SalesProject> SalesProjects { get; set; } = null!;
        public DbSet<SalesProjectChecklistItem> SalesProjectChecklistItems { get; set; } = null!;

        public IQueryable<SalesProject> SelectAllSalesProjects() => this.SalesProjects.AsQueryable();

        public async ValueTask<SalesProject?> SelectSalesProjectByIdAsync(Guid id) =>
            await this.SalesProjects
                .Include(p => p.ChecklistItems)
                .FirstOrDefaultAsync(p => p.Id == id);

        public async ValueTask<SalesProject> InsertSalesProjectAsync(SalesProject project)
        {
            EntityEntry<SalesProject> entry = await this.SalesProjects.AddAsync(project);
            await this.SaveChangesAsync();
            return entry.Entity;
        }

        public async ValueTask<SalesProject> UpdateSalesProjectAsync(SalesProject project)
        {
            EntityEntry<SalesProject> entry = this.SalesProjects.Update(project);
            await this.SaveChangesAsync();
            return entry.Entity;
        }

        public async ValueTask StageInsertSalesProjectChecklistItemAsync(SalesProjectChecklistItem item)
        {
            await this.SalesProjectChecklistItems.AddAsync(item);
        }
    }
}
