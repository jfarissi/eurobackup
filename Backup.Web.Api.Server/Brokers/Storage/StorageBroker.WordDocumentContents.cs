using Backup.Web.Api.Server.Models.WordDocumentContents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Backup.Web.Api.Server.Brokers.Storage
{
    public partial class StorageBroker
    {
        public DbSet<WordDocumentContent> WordDocumentContents { get; set; }

        public async ValueTask<WordDocumentContent> InsertWordDocumentContentsAsync(WordDocumentContent WordDocumentContents)
        {
            EntityEntry<WordDocumentContent> WordDocumentContentsEntityEntry = await this.WordDocumentContents.AddAsync(WordDocumentContents);
            await this.SaveChangesAsync();

            return WordDocumentContentsEntityEntry.Entity;
        }

        public IQueryable<WordDocumentContent> SelectAllWordDocumentContents() => this.WordDocumentContents.AsQueryable();

        public async ValueTask<WordDocumentContent> SelectWordDocumentContentsByIdAsync(Guid WordDocumentContentsId)
        {
            this.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;

            return await WordDocumentContents.FindAsync(WordDocumentContentsId);
        }

        public async ValueTask<WordDocumentContent> UpdateWordDocumentContentsAsync(WordDocumentContent WordDocumentContents)
        {
            EntityEntry<WordDocumentContent> WordDocumentContentsEntityEntry = this.WordDocumentContents.Update(WordDocumentContents);
            await this.SaveChangesAsync();

            return WordDocumentContentsEntityEntry.Entity;
        }

        public async ValueTask<WordDocumentContent> DeleteWordDocumentContentsAsync(WordDocumentContent WordDocumentContents)
        {
            EntityEntry<WordDocumentContent> WordDocumentContentsEntityEntry = this.WordDocumentContents.Remove(WordDocumentContents);
            await this.SaveChangesAsync();
            return WordDocumentContentsEntityEntry.Entity;
        }
    }
}
