using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Services.ErpSync;
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

        public ErpProductController(
            IStorageBroker storage,
            IErpProductSyncService syncService,
            IErpExcelImportService excelImport)
        {
            _storage = storage;
            _syncService = syncService;
            _excelImport = excelImport;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50,
            [FromQuery] string? brand = null,
            [FromQuery] string? q = null,
            [FromQuery] bool? fromExcel = null,
            [FromQuery] string? dataSource = null,
            CancellationToken ct = default)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 200);

            var query = _storage.SelectAllErpProducts().AsNoTracking();
            if (!string.IsNullOrWhiteSpace(brand))
                query = query.Where(p => p.Brand != null && p.Brand.Contains(brand));
            if (fromExcel.HasValue)
                query = query.Where(p => p.FromExcel == fromExcel.Value);
            if (!string.IsNullOrWhiteSpace(dataSource))
                query = query.Where(p => p.DataSource == dataSource);
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
        public async Task<IActionResult> SyncAll(CancellationToken ct = default)
        {
            var log = await _syncService.SyncAllProductsAsync(ct);
            return Ok(log);
        }

        /// <summary>
        /// Charge les produits depuis les Excel fournisseurs (ErpSync:ExcelProductPath),
        /// puis optionnellement lance l'enrichissement ERP.
        /// </summary>
        [HttpPost("import-excel")]
        public async Task<IActionResult> ImportExcel(
            [FromQuery] bool syncAfter = false,
            [FromQuery] string? path = null,
            CancellationToken ct = default)
        {
            var importResult = await _excelImport.ImportFromDirectoryAsync(path, ct);
            object? syncLog = null;
            if (syncAfter)
                syncLog = await _syncService.SyncAllProductsAsync(ct);

            return Ok(new { import = importResult, sync = syncLog });
        }

        [HttpGet("changes")]
        public async Task<IActionResult> GetChanges(
            [FromQuery] bool? unreadOnly = null,
            [FromQuery] string? changeType = null,
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
