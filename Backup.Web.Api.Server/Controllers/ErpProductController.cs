using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Models;
using Backup.Web.Api.Server.Services.ErpSync;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RESTFulSense.Controllers;

namespace Backup.Web.Api.Server.Controllers
{
    [ApiController]
    [Route("api/erp-products")]
    public class ErpProductController : RESTFulController
    {
        private readonly IStorageBroker _storage;
        private readonly IErpProductSyncService _syncService;
        private readonly IErpExcelImportService _excelImport;
        private readonly IErpCatalogSyncService _catalogSync;

        public ErpProductController(
            IStorageBroker storage,
            IErpProductSyncService syncService,
            IErpExcelImportService excelImport,
            IErpCatalogSyncService catalogSync)
        {
            _storage = storage;
            _syncService = syncService;
            _excelImport = excelImport;
            _catalogSync = catalogSync;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50,
            [FromQuery] string? brand = null,
            [FromQuery] string? q = null,
            [FromQuery] bool? fromExcel = null,
            [FromQuery] string? dataSource = null,
            [FromQuery] string? mainTypeId = null,
            [FromQuery] string? typeId = null,
            [FromQuery] string? subTypeId = null,
            CancellationToken ct = default)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 200);

            var query = _storage.SelectAllErpProducts().AsNoTracking();
            if (!string.IsNullOrWhiteSpace(brand))
            {
                var brandTerm = brand.Trim().ToLowerInvariant();
                query = query.Where(p => p.Brand != null && p.Brand.ToLower() == brandTerm);
            }
            if (fromExcel.HasValue)
                query = query.Where(p => p.FromExcel == fromExcel.Value);
            if (!string.IsNullOrWhiteSpace(dataSource))
                query = query.Where(p => p.DataSource == dataSource);
            if (!string.IsNullOrWhiteSpace(subTypeId))
                query = query.Where(p => p.SubTypeID == subTypeId);
            else if (!string.IsNullOrWhiteSpace(typeId))
                query = query.Where(p => p.TypeID == typeId);
            else if (!string.IsNullOrWhiteSpace(mainTypeId))
                query = query.Where(p => p.MainTypeID == mainTypeId);
            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim().ToLowerInvariant();
                query = query.Where(p =>
                    (p.Name != null && p.Name.ToLower().Contains(term))
                    || (p.Name2 != null && p.Name2.ToLower().Contains(term))
                    || (p.Reference != null && p.Reference.ToLower().Contains(term))
                    || (p.Ean != null && p.Ean.ToLower().Contains(term))
                    || (p.ErpProductId != null && p.ErpProductId.ToLower().Contains(term))
                    || (p.Brand != null && p.Brand.ToLower().Contains(term))
                    || (p.SourceFile != null && p.SourceFile.ToLower().Contains(term)));
            }

            var total = await query.CountAsync(ct);
            var items = await query
                .OrderBy(p => p.Name)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            return Ok(new { total, page, pageSize, items });
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById([FromRoute] int id, CancellationToken ct = default)
        {
            var item = await _storage.SelectAllErpProducts()
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == id, ct);
            if (item == null)
                return NotFound();
            return Ok(item);
        }

        [HttpGet("search")]
        public async Task<IActionResult> Search([FromQuery] string q, [FromQuery] int limit = 50, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(q))
                return BadRequest("q required");

            limit = Math.Clamp(limit, 1, 200);
            var term = q.Trim().ToLowerInvariant();
            var items = await _storage.SelectAllErpProducts()
                .AsNoTracking()
                .Where(p =>
                    (p.Name != null && p.Name.ToLower().Contains(term))
                    || (p.Reference != null && p.Reference.ToLower().Contains(term))
                    || (p.Ean != null && p.Ean.ToLower().Contains(term))
                    || (p.ErpProductId != null && p.ErpProductId.ToLower().Contains(term))
                    || (p.Brand != null && p.Brand.ToLower().Contains(term)))
                .OrderBy(p => p.Name)
                .Take(limit)
                .ToListAsync(ct);

            return Ok(items);
        }

        [HttpPost("sync/{erpId}")]
        public async Task<IActionResult> SyncOne([FromRoute] string erpId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(erpId))
                return BadRequest("erpId required");

            try
            {
                var product = await _syncService.SyncProductByIdAsync(erpId, ct);
                if (product == null)
                    return NotFound(new { message = $"Produit {erpId} introuvable après sync" });
                return Ok(product);
            }
            catch (Exception ex)
            {
                var msg = ex.InnerException?.Message ?? ex.Message;
                return StatusCode(502, new { message = $"Sync ERP échouée pour {erpId}", detail = msg });
            }
        }

        /// <summary>Sync par Id local (PK MySQL) — utilisé par l'UI Produits.</summary>
        [HttpPost("{id:int}/sync")]
        public async Task<IActionResult> SyncByLocalId([FromRoute] int id, CancellationToken ct = default)
        {
            try
            {
                var product = await _syncService.SyncLocalProductByIdAsync(id, ct);
                if (product == null)
                    return NotFound(new { message = $"Produit local #{id} introuvable" });
                return Ok(product);
            }
            catch (Exception ex)
            {
                var msg = ex.InnerException?.Message ?? ex.Message;
                return StatusCode(502, new { message = $"Sync ERP échouée pour produit #{id}", detail = msg });
            }
        }

        [HttpPost("sync-all")]
        public async Task<IActionResult> SyncAll(
            [FromQuery] bool wait = false,
            CancellationToken ct = default)
        {
            if (wait)
            {
                var log = await _syncService.SyncAllProductsAsync(ct);
                return Ok(log);
            }

            var started = await _syncService.StartSyncAllAsync(ct);
            return Accepted(started);
        }

        /// <summary>
        /// Enrichit depuis l'ERP les produits locaux correspondant aux filtres (marque / catégories).
        /// Même périmètre que le tableau filtré — pas un import complet de la branche ERP.
        /// </summary>
        [HttpPost("sync-catalog")]
        [RequestTimeout(3_600_000)]
        public async Task<IActionResult> SyncCatalog(
            [FromQuery] string? mainTypeId = null,
            [FromQuery] string? typeId = null,
            [FromQuery] string? subTypeId = null,
            [FromQuery] string? brand = null,
            [FromQuery] bool wait = false,
            [FromQuery] bool cancelPrevious = true,
            CancellationToken ct = default)
        {
            var filter = new ErpCatalogSyncFilter
            {
                MainTypeId = mainTypeId,
                TypeId = typeId,
                SubTypeId = subTypeId,
                Brand = brand
            };

            if (!filter.HasAnyFilter)
            {
                return BadRequest(new
                {
                    message = "Au moins un filtre requis : brand, mainTypeId, typeId ou subTypeId"
                });
            }

            try
            {
                if (wait)
                {
                    var log = await _syncService.SyncCatalogAsync(filter, ct);
                    return Ok(log);
                }

                var started = await _syncService.StartSyncCatalogAsync(filter, cancelPrevious, ct);
                return Accepted(started);
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                var msg = ex.InnerException?.Message ?? ex.Message;
                return StatusCode(500, new { message = "Sync catalogue échouée", detail = msg });
            }
        }

        [HttpPost("sync-cancel")]
        public async Task<IActionResult> CancelRunningSync(CancellationToken ct = default)
        {
            var cancelled = await _syncService.CancelRunningSyncAsync(ct);
            if (cancelled == null)
                return NotFound(new { message = "Aucune sync en cours" });
            return Ok(cancelled);
        }

        [HttpGet("sync-logs/{jobId}")]
        public async Task<IActionResult> GetSyncLogByJobId([FromRoute] string jobId, CancellationToken ct = default)
        {
            var log = await _syncService.GetSyncLogByJobIdAsync(jobId, ct);
            if (log == null)
                return NotFound(new { message = $"Job {jobId} introuvable" });
            return Ok(log);
        }

        /// <summary>
        /// Charge les produits depuis les Excel fournisseurs (ErpSync:ExcelProductPath),
        /// puis optionnellement lance l'enrichissement ERP.
        /// </summary>
        [HttpPost("import-excel")]
        [RequestTimeout(3_600_000)]
        public async Task<IActionResult> ImportExcel(
            [FromQuery] bool syncAfter = false,
            [FromQuery] string? path = null,
            CancellationToken ct = default)
        {
            try
            {
                var importResult = await _excelImport.ImportFromDirectoryAsync(path, ct);
                object? syncLog = null;
                if (syncAfter)
                    syncLog = await _syncService.SyncAllProductsAsync(ct);

                var catalog = await _catalogSync.RebuildFromProductsAsync(ct);

                return Ok(new { import = importResult, sync = syncLog, catalog });
            }
            catch (Exception ex)
            {
                var msg = ex.InnerException?.Message ?? ex.Message;
                return StatusCode(500, new { message = "Import Excel échoué", detail = msg });
            }
        }

        /// <summary>
        /// Reconstruit ErpBrands + ErpCategories depuis les produits existants
        /// et rattache BrandId / CategoryId.
        /// </summary>
        [HttpPost("rebuild-catalog")]
        [RequestTimeout(3_600_000)]
        public async Task<IActionResult> RebuildCatalog(CancellationToken ct = default)
        {
            try
            {
                var result = await _catalogSync.RebuildFromProductsAsync(ct);
                return Ok(result);
            }
            catch (Exception ex)
            {
                var msg = ex.InnerException?.Message ?? ex.Message;
                return StatusCode(500, new { message = "Rebuild catalog échoué", detail = msg });
            }
        }

        [HttpGet("brands")]
        public async Task<IActionResult> GetBrands(
            [FromQuery] string? mainTypeId = null,
            [FromQuery] string? typeId = null,
            [FromQuery] string? subTypeId = null,
            CancellationToken ct = default)
        {
            var hasCategoryFilter = !string.IsNullOrWhiteSpace(mainTypeId)
                || !string.IsNullOrWhiteSpace(typeId)
                || !string.IsNullOrWhiteSpace(subTypeId);

            if (!hasCategoryFilter)
            {
                var all = await _storage.SelectAllErpBrands()
                    .AsNoTracking()
                    .OrderBy(b => b.Name)
                    .ToListAsync(ct);
                return Ok(all);
            }

            var brandNames = await BuildFilteredProductsQuery(
                    brand: null,
                    mainTypeId,
                    typeId,
                    subTypeId)
                .Where(p => p.Brand != null && p.Brand != "")
                .Select(p => p.Brand!)
                .Distinct()
                .ToListAsync(ct);

            var items = await _storage.SelectAllErpBrands()
                .AsNoTracking()
                .Where(b => brandNames.Contains(b.Name))
                .OrderBy(b => b.Name)
                .ToListAsync(ct);

            return Ok(items);
        }

        [HttpGet("categories")]
        public async Task<IActionResult> GetCategories(
            [FromQuery] string? level = null,
            [FromQuery] int? parentId = null,
            [FromQuery] string? brand = null,
            [FromQuery] string? mainTypeId = null,
            [FromQuery] string? typeId = null,
            CancellationToken ct = default)
        {
            var query = _storage.SelectAllErpCategories().AsNoTracking();
            if (!string.IsNullOrWhiteSpace(level))
                query = query.Where(c => c.Level == level);
            if (parentId.HasValue)
                query = query.Where(c => c.ParentId == parentId.Value);

            var hasProductFilter = !string.IsNullOrWhiteSpace(brand)
                || !string.IsNullOrWhiteSpace(mainTypeId)
                || !string.IsNullOrWhiteSpace(typeId);

            if (hasProductFilter)
            {
                var products = BuildFilteredProductsQuery(brand, mainTypeId, typeId, subTypeId: null);

                if (parentId.HasValue && !string.IsNullOrWhiteSpace(level))
                {
                    var parent = await _storage.SelectAllErpCategories()
                        .AsNoTracking()
                        .FirstOrDefaultAsync(c => c.Id == parentId.Value, ct);
                    if (parent != null)
                    {
                        if (level.Equals("Type", StringComparison.OrdinalIgnoreCase))
                            products = products.Where(p => p.MainTypeID == parent.ErpExternalId);
                        else if (level.Equals("SubType", StringComparison.OrdinalIgnoreCase))
                            products = products.Where(p => p.TypeID == parent.ErpExternalId);
                    }
                }

                var validIds = await GetDistinctCategoryExternalIdsAsync(products, level, ct);
                query = validIds.Count > 0
                    ? query.Where(c => validIds.Contains(c.ErpExternalId))
                    : query.Where(c => false);
            }

            var items = await query
                .OrderBy(c => c.SortOrder)
                .ThenBy(c => c.NameNl)
                .ToListAsync(ct);
            return Ok(items);
        }

        private static async Task<List<string>> GetDistinctCategoryExternalIdsAsync(
            IQueryable<ErpProduct> products,
            string? level,
            CancellationToken ct)
        {
            if (level.Equals("MainType", StringComparison.OrdinalIgnoreCase))
            {
                return await products
                    .Where(p => p.MainTypeID != null && p.MainTypeID != "")
                    .Select(p => p.MainTypeID!)
                    .Distinct()
                    .ToListAsync(ct);
            }

            if (level.Equals("Type", StringComparison.OrdinalIgnoreCase))
            {
                return await products
                    .Where(p => p.TypeID != null && p.TypeID != "")
                    .Select(p => p.TypeID!)
                    .Distinct()
                    .ToListAsync(ct);
            }

            if (level.Equals("SubType", StringComparison.OrdinalIgnoreCase))
            {
                return await products
                    .Where(p => p.SubTypeID != null && p.SubTypeID != "")
                    .Select(p => p.SubTypeID!)
                    .Distinct()
                    .ToListAsync(ct);
            }

            return new List<string>();
        }

        private IQueryable<ErpProduct> BuildFilteredProductsQuery(
            string? brand,
            string? mainTypeId,
            string? typeId,
            string? subTypeId)
        {
            var query = _storage.SelectAllErpProducts().AsNoTracking();
            if (!string.IsNullOrWhiteSpace(brand))
            {
                var brandTerm = brand.Trim().ToLowerInvariant();
                query = query.Where(p => p.Brand != null && p.Brand.ToLower() == brandTerm);
            }

            if (!string.IsNullOrWhiteSpace(subTypeId))
                query = query.Where(p => p.SubTypeID == subTypeId);
            else if (!string.IsNullOrWhiteSpace(typeId))
                query = query.Where(p => p.TypeID == typeId);
            else if (!string.IsNullOrWhiteSpace(mainTypeId))
                query = query.Where(p => p.MainTypeID == mainTypeId);

            return query;
        }

        [HttpGet("changes")]
        public async Task<IActionResult> GetChanges(
            [FromQuery] bool? unreadOnly = null,
            [FromQuery] string? changeType = null,
            /// <summary>
            /// both = Avant et Après renseignés ;
            /// cleared = Avant renseigné, Après vide ;
            /// added = Avant vide, Après renseigné.
            /// </summary>
            [FromQuery] string? valueMode = null,
            [FromQuery] string? q = null,
            [FromQuery] DateTime? from = null,
            [FromQuery] DateTime? to = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50,
            CancellationToken ct = default)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 200);

            var query = _storage.SelectAllErpProductChangeLogs().AsNoTracking();
            if (unreadOnly == true)
                query = query.Where(c => !c.IsRead);
            if (!string.IsNullOrWhiteSpace(changeType))
                query = query.Where(c => c.ChangeType == changeType);

            var mode = (valueMode ?? string.Empty).Trim().ToLowerInvariant();
            if (mode == "both")
            {
                query = query.Where(c =>
                    c.OldValue != null && c.OldValue != ""
                    && c.NewValue != null && c.NewValue != "");
            }
            else if (mode == "cleared")
            {
                query = query.Where(c =>
                    c.OldValue != null && c.OldValue != ""
                    && (c.NewValue == null || c.NewValue == ""));
            }
            else if (mode == "added")
            {
                query = query.Where(c =>
                    (c.OldValue == null || c.OldValue == "")
                    && c.NewValue != null && c.NewValue != "");
            }

            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim().ToLowerInvariant();
                query = query.Where(c =>
                    c.ErpProduct != null && (
                        (c.ErpProduct.Name != null && c.ErpProduct.Name.ToLower().Contains(term))
                        || (c.ErpProduct.Reference != null && c.ErpProduct.Reference.ToLower().Contains(term))
                        || (c.ErpProduct.Ean != null && c.ErpProduct.Ean.ToLower().Contains(term))
                        || (c.ErpProduct.Brand != null && c.ErpProduct.Brand.ToLower().Contains(term))
                        || (c.ErpProduct.ErpProductId != null && c.ErpProduct.ErpProductId.ToLower().Contains(term))
                        || (c.FieldName != null && c.FieldName.ToLower().Contains(term))
                        || (c.OldValue != null && c.OldValue.ToLower().Contains(term))
                        || (c.NewValue != null && c.NewValue.ToLower().Contains(term))));
            }

            if (from.HasValue)
                query = query.Where(c => c.DetectedAt >= from.Value);
            if (to.HasValue)
                query = query.Where(c => c.DetectedAt <= to.Value);

            var total = await query.CountAsync(ct);
            var items = await query
                .Include(c => c.ErpProduct)
                .OrderByDescending(c => c.DetectedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(c => new
                {
                    c.Id,
                    c.ErpProductId,
                    c.ChangeType,
                    c.FieldName,
                    c.OldValue,
                    c.NewValue,
                    c.DetectedAt,
                    c.SyncJobId,
                    c.IsRead,
                    product = c.ErpProduct == null ? null : new
                    {
                        id = c.ErpProduct.Id,
                        erpProductId = c.ErpProduct.ErpProductId,
                        name = c.ErpProduct.Name,
                        reference = c.ErpProduct.Reference,
                        ean = c.ErpProduct.Ean,
                        brand = c.ErpProduct.Brand,
                        unitPrice = c.ErpProduct.UnitPrice,
                        stockQuantity = c.ErpProduct.StockQuantity
                    }
                })
                .ToListAsync(ct);

            return Ok(new { total, page, pageSize, items });
        }

        [HttpPost("changes/mark-read")]
        public async Task<IActionResult> MarkChangesRead([FromBody] MarkChangesReadRequest request, CancellationToken ct = default)
        {
            if (request?.Ids == null || request.Ids.Count == 0)
                return BadRequest("ids required");

            await _syncService.MarkChangesAsReadAsync(request.Ids, ct);
            return Ok(new { marked = request.Ids.Count });
        }

        [HttpPost("changes/delete")]
        public async Task<IActionResult> DeleteChanges([FromBody] MarkChangesReadRequest request, CancellationToken ct = default)
        {
            if (request?.Ids == null || request.Ids.Count == 0)
                return BadRequest("ids required");

            var deleted = await _syncService.DeleteChangesAsync(request.Ids, ct);
            return Ok(new { deleted });
        }

        [HttpPost("changes/cleanup-formatting")]
        public async Task<IActionResult> CleanupFormattingFalsePositives(CancellationToken ct = default)
        {
            var deleted = await _syncService.CleanupFormattingFalsePositivesAsync(ct);
            return Ok(new { deleted });
        }

        [HttpGet("sync-logs")]
        public async Task<IActionResult> GetSyncLogs(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            CancellationToken ct = default)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var query = _storage.SelectAllErpSyncLogs().AsNoTracking();
            var total = await query.CountAsync(ct);
            var items = await query
                .OrderByDescending(s => s.StartedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            return Ok(new { total, page, pageSize, items });
        }

        public class MarkChangesReadRequest
        {
            public List<int> Ids { get; set; } = new();
        }
    }
}
