using Backup.Web.Api.Server.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Backup.Web.Api.Server.Brokers.Storage
{
    public partial class StorageBroker
    {
        public DbSet<Document> Documents { get; set; }
        public DbSet<DocumentLine> DocumentLines { get; set; }
        public DbSet<StockItem> Stock { get; set; }
        public DbSet<Backup.Web.Api.Server.Models.DocumentRelation> DocumentRelations { get; set; }

        public async ValueTask<Document> InsertDocumentAsync(Document document)
        {
            EntityEntry<Document> entry = await this.Documents.AddAsync(document);
            await this.SaveChangesAsync();
            return entry.Entity;
        }

        public async ValueTask<Backup.Web.Api.Server.Models.DocumentRelation> InsertRelationAsync(Backup.Web.Api.Server.Models.DocumentRelation relation)
        {
            var entry = await this.DocumentRelations.AddAsync(relation);
            await this.SaveChangesAsync();
            return entry.Entity;
        }

        public IQueryable<Backup.Web.Api.Server.Models.DocumentRelation> SelectAllRelations() => this.DocumentRelations.AsQueryable();

        public async ValueTask DeleteRelationAsync(int relationId)
        {
            var rel = await this.DocumentRelations.FindAsync(relationId);
            if (rel != null)
            {
                this.DocumentRelations.Remove(rel);
                await this.SaveChangesAsync();
            }
        }

        public IQueryable<Document> SelectAllDocuments() => this.Documents.AsQueryable();

        public async ValueTask<Document?> SelectDocumentByIdAsync(int id)
        {
            this.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
            return await this.Documents.FindAsync(id);
        }

        public IQueryable<Document> SearchDocuments(string query)
        {
            query = (query ?? string.Empty).Trim();
            var qLower = query.ToLower();

            return this.Documents.Where(d =>
                (d.Numero != null && d.Numero.ToLower().Contains(qLower)) ||
                (d.Client != null && d.Client.ToLower().Contains(qLower)) ||
                (d.Supplier != null && d.Supplier.ToLower().Contains(qLower)) ||
                d.ContentText.ToLower().Contains(qLower));
        }

        public async ValueTask InsertDocumentLinesAsync(IEnumerable<DocumentLine> lines)
        {
            if (lines == null) return;
            await this.DocumentLines.AddRangeAsync(lines);
            await this.SaveChangesAsync();
        }

        public IQueryable<DocumentLine> SelectLinesByDocumentId(int documentId)
            => this.DocumentLines.Where(l => l.DocumentId == documentId);

        public async ValueTask DeleteLinesByDocumentIdAsync(int documentId)
        {
            var lines = this.DocumentLines.Where(l => l.DocumentId == documentId).ToList();
            if (lines.Count == 0) return;
            this.DocumentLines.RemoveRange(lines);
            await this.SaveChangesAsync();
        }

        public async ValueTask UpsertStockBatchAsync(IEnumerable<(string productKey, decimal quantityDelta)> changes)
        {
            var grouped = changes
                .Where(c => !string.IsNullOrWhiteSpace(c.productKey) && c.quantityDelta != 0)
                .GroupBy(c => c.productKey.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.quantityDelta), StringComparer.OrdinalIgnoreCase);

            if (grouped.Count == 0) return;

            // load existing items in one shot
            var keys = grouped.Keys.ToList();
            var existing = this.Stock.Where(s => keys.Contains(s.ProductKey)).ToList();
            var existingLookup = existing.ToDictionary(s => s.ProductKey, StringComparer.OrdinalIgnoreCase);

            foreach (var kv in grouped)
            {
                if (existingLookup.TryGetValue(kv.Key, out var item))
                {
                    item.QuantityOnHand += kv.Value;
                    item.LastUpdated = DateTime.UtcNow;
                }
                else
                {
                    this.Stock.Add(new StockItem
                    {
                        ProductKey = kv.Key,
                        QuantityOnHand = kv.Value,
                        LastUpdated = DateTime.UtcNow
                    });
                }
            }

            await this.SaveChangesAsync();
        }

        public IQueryable<StockItem> SelectAllStock() => this.Stock.AsQueryable();
    }
}


