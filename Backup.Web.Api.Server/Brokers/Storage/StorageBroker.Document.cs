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
        public DbSet<Backup.Web.Api.Server.Models.StockUpdate> StockUpdates { get; set; }
        public DbSet<Backup.Web.Api.Server.Models.DocumentRelation> DocumentRelations { get; set; }
        public DbSet<DeliveryLineAdjustment> DeliveryLineAdjustments { get; set; }

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

        public async ValueTask<Backup.Web.Api.Server.Models.DocumentRelation?> SelectRelationByInvoiceAndDeliveryAsync(int invoiceId, int deliveryId)
        {
            return await this.DocumentRelations
                .FirstOrDefaultAsync(r => r.InvoiceId == invoiceId && r.DeliveryId == deliveryId);
        }

        public async ValueTask UpdateRelationAsync(Backup.Web.Api.Server.Models.DocumentRelation relation)
        {
            this.DocumentRelations.Update(relation);
            await this.SaveChangesAsync();
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

        public async ValueTask UpsertStockBatchAsync(IEnumerable<(string productKey, decimal quantityDelta, string? supplier, string? description, string? unit)> changes, int deliveryId, int? invoiceId = null)
        {
            // Normaliser toutes les ProductKey (supprimer préfixes si présents)
            // Note: On inclut même les deltas=0 pour mettre à jour LastDeliveryId et LastUpdated
            var normalizedChanges = changes
                .Where(c => !string.IsNullOrWhiteSpace(c.productKey))
                .Select(c => (
                    productKey: Backup.Web.Api.Server.Services.Documents.Parsing.ProductKeyHelper.Normalize(c.productKey),
                    quantityDelta: c.quantityDelta,
                    supplier: c.supplier,
                    description: c.description,
                    unit: c.unit
                ))
                .ToList();

            var grouped = normalizedChanges
                .GroupBy(c => c.productKey, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (grouped.Count == 0) return;

            // load existing items in one shot
            var keys = grouped.Select(g => g.Key).ToList();
            System.Diagnostics.Debug.WriteLine($"[UpsertStockBatch] Looking for stock items with keys: {string.Join(", ", keys)}");
            
            // Charger tous les items et filtrer en mémoire (Entity Framework ne peut pas traduire Contains avec StringComparer)
            // IMPORTANT: Ne pas utiliser ToList() ici car cela charge les entités sans tracking
            // Utiliser AsEnumerable() pour garder le tracking
            var keysLower = keys.Select(k => k.ToLowerInvariant()).ToHashSet();
            var existing = this.Stock
                .AsEnumerable()
                .Where(s => s.ProductKey != null && keysLower.Contains(s.ProductKey.ToLowerInvariant()))
                .ToList();
            
            System.Diagnostics.Debug.WriteLine($"[UpsertStockBatch] Found {existing.Count} existing stock items");
            foreach (var item in existing)
            {
                System.Diagnostics.Debug.WriteLine($"[UpsertStockBatch] Existing stock: ProductKey={item.ProductKey}, QuantityOnHand={item.QuantityOnHand}");
            }
            var existingLookup = existing.ToDictionary(s => s.ProductKey, StringComparer.OrdinalIgnoreCase);

            // Récupérer le fournisseur du BL
            var delivery = await this.SelectDocumentByIdAsync(deliveryId);
            var deliverySupplier = delivery?.Supplier;

            var stockUpdates = new List<Backup.Web.Api.Server.Models.StockUpdate>();

            foreach (var group in grouped)
            {
                var productKey = group.Key;
                var quantityDelta = group.Sum(x => x.quantityDelta);
                System.Diagnostics.Debug.WriteLine($"[UpsertStockBatch] Processing productKey={productKey}, quantityDelta={quantityDelta}");
                // Prendre les infos du premier élément du groupe (supplier, description, unit)
                var firstItem = group.First();
                var supplier = firstItem.supplier ?? deliverySupplier;
                var description = firstItem.description;
                var unit = firstItem.unit;

                decimal quantityAfter;
                if (existingLookup.TryGetValue(productKey, out var item))
                {
                    var quantityBefore = item.QuantityOnHand;
                    // Mettre à jour la quantité seulement si le delta n'est pas 0
                    if (quantityDelta != 0)
                    {
                        item.QuantityOnHand += quantityDelta;
                        System.Diagnostics.Debug.WriteLine($"[UpsertStockBatch] Updated existing stock: ProductKey={productKey}, QuantityBefore={quantityBefore}, QuantityDelta={quantityDelta}, QuantityAfter={item.QuantityOnHand}");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[UpsertStockBatch] No quantity change for ProductKey={productKey}, QuantityOnHand={quantityBefore}, QuantityDelta=0");
                    }
                    // Toujours mettre à jour LastUpdated et LastDeliveryId pour la traçabilité
                    item.LastUpdated = DateTime.UtcNow;
                    item.LastDeliveryId = deliveryId;
                    
                    // Forcer Entity Framework à détecter les changements en utilisant Update()
                    this.Stock.Update(item);
                    // Mettre à jour les infos si elles sont manquantes
                    if (string.IsNullOrWhiteSpace(item.Supplier) && !string.IsNullOrWhiteSpace(supplier))
                        item.Supplier = supplier;
                    if (string.IsNullOrWhiteSpace(item.Description) && !string.IsNullOrWhiteSpace(description))
                        item.Description = description;
                    if (string.IsNullOrWhiteSpace(item.Unit) && !string.IsNullOrWhiteSpace(unit))
                        item.Unit = unit;
                    quantityAfter = item.QuantityOnHand;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[UpsertStockBatch] Creating new stock item: ProductKey={productKey}, QuantityOnHand={quantityDelta}");
                    var newItem = new StockItem
                    {
                        ProductKey = productKey,
                        QuantityOnHand = quantityDelta,
                        LastUpdated = DateTime.UtcNow,
                        LastDeliveryId = deliveryId,
                        Supplier = supplier,
                        Description = description,
                        Unit = unit
                    };
                    this.Stock.Add(newItem);
                    quantityAfter = quantityDelta;
                }

                // Enregistrer l'historique de mise à jour (même si delta=0 pour la traçabilité)
                stockUpdates.Add(new Backup.Web.Api.Server.Models.StockUpdate
                {
                    ProductKey = productKey,
                    QuantityDelta = quantityDelta,
                    QuantityAfter = quantityAfter,
                    DeliveryId = deliveryId,
                    InvoiceId = invoiceId,
                    UpdatedAt = DateTime.UtcNow
                });
            }

            // Ajouter tous les enregistrements d'historique
            if (stockUpdates.Count > 0)
            {
                await this.StockUpdates.AddRangeAsync(stockUpdates);
            }

            var saveResult = await this.SaveChangesAsync();
            System.Diagnostics.Debug.WriteLine($"[UpsertStockBatch] SaveChangesAsync completed: {saveResult} entities saved");
            
            // Vérifier que les changements ont bien été sauvegardés
            foreach (var group in grouped)
            {
                var productKey = group.Key;
                var item = existingLookup.TryGetValue(productKey, out var i) ? i : null;
                if (item != null)
                {
                    // Recharger depuis la base pour vérifier
                    var reloaded = await this.Stock.FindAsync(item.Id);
                    if (reloaded != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"[UpsertStockBatch] Verification - ProductKey={productKey}, QuantityOnHand in DB={reloaded.QuantityOnHand}");
                    }
                }
            }
        }

        public IQueryable<StockItem> SelectAllStock() => this.Stock.AsQueryable();

        public IQueryable<Backup.Web.Api.Server.Models.StockUpdate> SelectStockUpdatesByDeliveryId(int deliveryId)
            => this.StockUpdates.Where(s => s.DeliveryId == deliveryId).OrderByDescending(s => s.UpdatedAt);

        public IQueryable<Backup.Web.Api.Server.Models.StockUpdate> SelectStockUpdatesByProductKey(string productKey)
            => this.StockUpdates.Where(s => s.ProductKey == productKey).OrderByDescending(s => s.UpdatedAt);

        // Delivery Line Adjustments
        public async ValueTask<DeliveryLineAdjustment> InsertAdjustmentAsync(DeliveryLineAdjustment adjustment)
        {
            var entry = await this.DeliveryLineAdjustments.AddAsync(adjustment);
            await this.SaveChangesAsync();
            return entry.Entity;
        }

        public async ValueTask<DeliveryLineAdjustment> UpdateAdjustmentAsync(DeliveryLineAdjustment adjustment)
        {
            var entry = this.DeliveryLineAdjustments.Update(adjustment);
            await this.SaveChangesAsync();
            return entry.Entity;
        }

        public IQueryable<DeliveryLineAdjustment> SelectAdjustmentsByDeliveryId(int deliveryId)
        {
            return this.DeliveryLineAdjustments.Where(a => a.DeliveryId == deliveryId);
        }

        public async ValueTask<DeliveryLineAdjustment?> SelectAdjustmentByDeliveryAndProductKeyAsync(int deliveryId, string productKey)
        {
            return await this.DeliveryLineAdjustments
                .FirstOrDefaultAsync(a => a.DeliveryId == deliveryId && a.ProductKey == productKey);
        }
    }
}


