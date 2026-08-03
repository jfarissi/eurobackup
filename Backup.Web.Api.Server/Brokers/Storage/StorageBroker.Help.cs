using System.Linq;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Backup.Web.Api.Server.Brokers.Storage
{
    public partial class StorageBroker
    {
        public DbSet<HelpContent> HelpContents { get; set; } = null!;
        public DbSet<HelpFeedbackEvent> HelpFeedbackEvents { get; set; } = null!;
        public DbSet<HelpAnalyticsEvent> HelpAnalyticsEvents { get; set; } = null!;

        public IQueryable<HelpContent> SelectAllHelpContents() => this.HelpContents;
        public async ValueTask<HelpContent?> SelectHelpContentByIdAsync(int id) =>
            await this.HelpContents.FindAsync(id);
        public async ValueTask<HelpContent> InsertHelpContentAsync(HelpContent entity)
        {
            EntityEntry<HelpContent> entry = await this.HelpContents.AddAsync(entity);
            await this.SaveChangesAsync();
            return entry.Entity;
        }
        public async ValueTask<HelpContent> UpdateHelpContentAsync(HelpContent entity)
        {
            EntityEntry<HelpContent> entry = this.HelpContents.Update(entity);
            await this.SaveChangesAsync();
            return entry.Entity;
        }
        public async ValueTask DeleteHelpContentAsync(HelpContent entity)
        {
            this.HelpContents.Remove(entity);
            await this.SaveChangesAsync();
        }

        public IQueryable<HelpFeedbackEvent> SelectAllHelpFeedbackEvents() => this.HelpFeedbackEvents;
        public async ValueTask<HelpFeedbackEvent> InsertHelpFeedbackEventAsync(HelpFeedbackEvent entity)
        {
            EntityEntry<HelpFeedbackEvent> entry = await this.HelpFeedbackEvents.AddAsync(entity);
            await this.SaveChangesAsync();
            return entry.Entity;
        }

        public IQueryable<HelpAnalyticsEvent> SelectAllHelpAnalyticsEvents() => this.HelpAnalyticsEvents;
        public async ValueTask<HelpAnalyticsEvent> InsertHelpAnalyticsEventAsync(HelpAnalyticsEvent entity)
        {
            EntityEntry<HelpAnalyticsEvent> entry = await this.HelpAnalyticsEvents.AddAsync(entity);
            await this.SaveChangesAsync();
            return entry.Entity;
        }
    }
}
