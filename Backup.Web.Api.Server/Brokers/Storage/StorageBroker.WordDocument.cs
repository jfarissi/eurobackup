using Backup.Web.Api.Server.Models.WordDocuments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Backup.Web.Api.Server.Models;
using System;
using System.Collections.Generic;
using System.Linq;

using System.Threading.Tasks;

namespace Backup.Web.Api.Server.Brokers.Storage
{
    public partial class StorageBroker
    {
        public DbSet<WordDocument> WordDocuments { get; set; }

        public async ValueTask<WordDocument> InsertWordDocumentAsync(WordDocument WordDocument)
        {
            EntityEntry<WordDocument> WordDocumentEntityEntry = await this.WordDocuments.AddAsync(WordDocument);
            await this.SaveChangesAsync();

            return WordDocumentEntityEntry.Entity;
        }

        public IQueryable<WordDocument> SelectAllWordDocuments(Pager queryObj)
        {
            //IQueryable<WordDocument> query = DbSet;

            if (queryObj.Page <= 0)
                queryObj.Page = 1;

            if (queryObj.PageSize <= 0)
                queryObj.PageSize = 10;
            return this.WordDocuments.AsQueryable()
                .Include(c => c.WordDocumentContents)
                .Skip((queryObj.Page - 1) * queryObj.PageSize)
                .Take(queryObj.PageSize);

            //return query;
            //this.WordDocuments.AsQueryable().Include(c => c.WordDocumentContents);
        }

        public IQueryable<WordDocument> SelectAllWordDocuments() => this.WordDocuments.AsQueryable();

        public async ValueTask<WordDocument> SelectWordDocumentByIdAsync(Guid WordDocumentId)
        {
            this.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;

            return await WordDocuments.FindAsync(WordDocumentId);
        }

        public async ValueTask<WordDocument> UpdateWordDocumentAsync(WordDocument WordDocument)
        {
            EntityEntry<WordDocument> WordDocumentEntityEntry = this.WordDocuments.Update(WordDocument);
            await this.SaveChangesAsync();

            return WordDocumentEntityEntry.Entity;
        }

        public async ValueTask<WordDocument> DeleteWordDocumentAsync(WordDocument WordDocument)
        {
            EntityEntry<WordDocument> WordDocumentEntityEntry = this.WordDocuments.Remove(WordDocument);
            await this.SaveChangesAsync();
            return WordDocumentEntityEntry.Entity;
        }
    }
}
