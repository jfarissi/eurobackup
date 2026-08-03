using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Authorization;
using Authorize = Microsoft.AspNetCore.Authorization.AuthorizeAttribute;
using AllowAnonymous = Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute;
using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Models;
using Backup.Web.Api.Server.Models.Security;
using Backup.Web.Api.Server.Services.ErpSync;
using Backup.Web.Api.Server.Services.Purchases;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RESTFulSense.Controllers;

namespace Backup.Web.Api.Server.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/erp-products")]
    public class ErpProductController : RESTFulController
    {
        private readonly IStorageBroker _storage;
        private readonly IErpProductSyncService _syncService;
        private readonly IErpExcelImportService _excelImport;
        private readonly IErpCatalogSyncService _catalogSync;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ErpSyncOptions _erpSyncOptions;

        public ErpProductController(
            IStorageBroker storage,
            IErpProductSyncService syncService,
            IErpExcelImportService excelImport,
            IErpCatalogSyncService catalogSync,
            IHttpClientFactory httpClientFactory,
            IOptions<ErpSyncOptions> erpSyncOptions)
        {
            _storage = storage;
            _syncService = syncService;
            _excelImport = excelImport;
            _catalogSync = catalogSync;
            _httpClientFactory = httpClientFactory;
            _erpSyncOptions = erpSyncOptions.Value;
        }

        /// <summary>
        /// Proxies ERP product images (port 15022 is HTTP-only; browsers with HSTS on the host break direct https).
        /// Anonymous so &lt;img src&gt; works without Bearer token.
        /// </summary>
        [AllowAnonymous]
        [HttpGet("image")]
        public async Task<IActionResult> ProxyImage([FromQuery] string? f, CancellationToken ct = default)
        {
            var upstream = ErpProductImageUrls.ToUpstreamUrl(_erpSyncOptions.ImageBaseUrl, f);
            if (upstream == null)
                return NotFound();

            try
            {
                var client = _httpClientFactory.CreateClient("ErpProductImages");
                using var response = await client.GetAsync(upstream, HttpCompletionOption.ResponseHeadersRead, ct);
                if (!response.IsSuccessStatusCode)
                    return StatusCode((int)response.StatusCode);

                var contentType = response.Content.Headers.ContentType?.ToString() ?? "image/jpeg";
                var bytes = await response.Content.ReadAsByteArrayAsync(ct);
                Response.Headers.CacheControl = "public,max-age=3600";
                return File(bytes, contentType);
            }
            catch (Exception)
            {
                return NotFound();
            }
        }

        [HttpGet]
        [RequirePermission(Permissions.ProductRead)]
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
            [FromQuery] int? supplierId = null,
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

            query = await ApplySupplierBrandFilterAsync(query, supplierId, ct);

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
        [RequirePermission(Permissions.ProductRead)]
        public async Task<IActionResult> GetById([FromRoute] int id, CancellationToken ct = default)
        {
            var item = await _storage.SelectAllErpProducts()
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == id, ct);
            if (item == null)
                return NotFound();
            return Ok(item);
        }

        public class CreateErpProductRequest
        {
            public string? Name { get; set; }
            public string? Reference { get; set; }
            public string? Ean { get; set; }
            public decimal? PurchasePrice { get; set; }
            public decimal? VatPercent { get; set; }
            public int? BrandId { get; set; }
            public string? BrandName { get; set; }
            public int? CategoryId { get; set; }
            public string? SupplierName { get; set; }
        }

        /// <summary>
        /// Crée un produit catalogue manquant (ex. ligne facture non trouvée).
        /// Marque : BrandId / BrandName / suggestion via SupplierName.
        /// Catégorie : CategoryId (optionnel) → remplit MainType/Type/SubType.
        /// </summary>
        [HttpPost]
        [RequirePermission(Permissions.ProductCreate)]
        public async Task<IActionResult> Create([FromBody] CreateErpProductRequest? request, CancellationToken ct = default)
        {
            if (request == null)
                return BadRequest(new { message = "Body required" });

            var name = (request.Name ?? string.Empty).Trim();
            var reference = (request.Reference ?? string.Empty).Trim();
            var ean = string.IsNullOrWhiteSpace(request.Ean) ? null : request.Ean.Trim();

            if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(reference) && string.IsNullOrWhiteSpace(ean))
                return BadRequest(new { message = "Name, Reference or Ean required" });

            if (string.IsNullOrWhiteSpace(reference))
                reference = !string.IsNullOrWhiteSpace(ean) ? ean! : $"MAN-{Guid.NewGuid():N}"[..12].ToUpperInvariant();

            var existing = await FindLocalCatalogProductAsync(reference, ean, ct);
            if (existing != null)
                return Ok(new { product = existing, created = false, message = "Produit déjà présent dans le catalogue" });

            var (brandId, brandName) = await ResolveBrandForCreateAsync(
                request.BrandId, request.BrandName, request.SupplierName, ct);

            var product = new ErpProduct
            {
                ErpProductId = $"MAN-{reference}",
                Name = string.IsNullOrWhiteSpace(name) ? reference : name,
                Reference = reference,
                Ean = ean,
                BrandId = brandId,
                Brand = brandName,
                CPrice = request.PurchasePrice,
                UnitPrice = request.PurchasePrice,
                PriceHT = request.PurchasePrice,
                TypeVatPerc = request.VatPercent ?? 21m,
                DataSource = "Manual",
                FromExcel = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            if (request.CategoryId is > 0)
                await ApplyCategoryToProductAsync(product, request.CategoryId.Value, ct);

            // Éviter collision ErpProductId unique
            var erpIdTaken = await _storage.SelectAllErpProducts()
                .AsNoTracking()
                .AnyAsync(p => p.ErpProductId == product.ErpProductId, ct);
            if (erpIdTaken)
                product.ErpProductId = $"MAN-{reference}-{DateTime.UtcNow:HHmmss}";

            var inserted = await _storage.InsertErpProductAsync(product);
            return Ok(new { product = inserted, created = true });
        }

        [HttpGet("suggest-brand")]
        [RequirePermission(Permissions.ProductRead)]
        public async Task<IActionResult> SuggestBrand(
            [FromQuery] string? supplierName = null,
            [FromQuery] int? supplierId = null,
            CancellationToken ct = default)
        {
            if (supplierId is > 0)
            {
                var supplier = await _storage.SelectSupplierByIdAsync(supplierId.Value);
                supplierName = supplier?.Name ?? supplierName;
            }

            var token = SupplierBrandMatcher.DeriveBrandToken(supplierName);
            if (string.IsNullOrWhiteSpace(token))
                return Ok(new { token = (string?)null, brands = Array.Empty<object>(), suggestedBrandId = (int?)null });

            var tokenLower = token.ToLowerInvariant();
            var brands = await _storage.SelectAllErpBrands()
                .AsNoTracking()
                .Where(b => b.Name != null && b.Name.ToLower().Contains(tokenLower))
                .OrderBy(b => b.Name)
                .Select(b => new { b.Id, b.Name, b.Slug, b.IsActive })
                .ToListAsync(ct);

            return Ok(new
            {
                token,
                brands,
                suggestedBrandId = brands.Count == 1 ? brands[0].Id : (int?)null
            });
        }

        [HttpGet("search")]
        [RequirePermission(Permissions.ProductRead)]
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

        private async Task<ErpProduct?> FindLocalCatalogProductAsync(string? reference, string? ean, CancellationToken ct)
        {
            var query = _storage.SelectAllErpProducts().AsNoTracking();
            if (!string.IsNullOrWhiteSpace(ean))
            {
                var eanTerm = ean.Trim().ToLowerInvariant();
                var byEan = await query.FirstOrDefaultAsync(p => p.Ean != null && p.Ean.ToLower() == eanTerm, ct);
                if (byEan != null) return byEan;
            }

            if (!string.IsNullOrWhiteSpace(reference))
            {
                var refTerm = reference.Trim().ToLowerInvariant();
                return await query.FirstOrDefaultAsync(p =>
                    (p.Reference != null && p.Reference.ToLower() == refTerm)
                    || (p.ErpProductId != null && p.ErpProductId.ToLower() == refTerm), ct);
            }

            return null;
        }

        private async Task<(int? brandId, string? brandName)> ResolveBrandForCreateAsync(
            int? brandId,
            string? brandName,
            string? supplierName,
            CancellationToken ct)
        {
            if (brandId is > 0)
            {
                var brand = await _storage.SelectAllErpBrands()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(b => b.Id == brandId.Value, ct);
                if (brand != null)
                    return (brand.Id, brand.Name);
            }

            if (!string.IsNullOrWhiteSpace(brandName))
            {
                var name = brandName.Trim();
                var existing = await _storage.SelectAllErpBrands()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(b => b.Name.ToLower() == name.ToLower(), ct);
                if (existing != null)
                    return (existing.Id, existing.Name);
                return (null, name);
            }

            var token = SupplierBrandMatcher.DeriveBrandToken(supplierName);
            if (string.IsNullOrWhiteSpace(token))
                return (null, null);

            var tokenLower = token.ToLowerInvariant();
            var matches = await _storage.SelectAllErpBrands()
                .AsNoTracking()
                .Where(b => b.Name != null && b.Name.ToLower().Contains(tokenLower))
                .OrderBy(b => b.Name.Length)
                .ToListAsync(ct);

            if (matches.Count == 1)
                return (matches[0].Id, matches[0].Name);
            if (matches.Count > 1)
                return (matches[0].Id, matches[0].Name);

            return (null, token);
        }

        private async Task ApplyCategoryToProductAsync(ErpProduct product, int categoryId, CancellationToken ct)
        {
            var leaf = await _storage.SelectAllErpCategories()
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == categoryId, ct);
            if (leaf == null) return;

            product.CategoryId = leaf.Id;
            ApplyCategoryLevel(product, leaf);

            // Remonter parents pour MainType / Type
            var parentId = leaf.ParentId;
            var guard = 0;
            while (parentId.HasValue && guard++ < 5)
            {
                var parent = await _storage.SelectAllErpCategories()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Id == parentId.Value, ct);
                if (parent == null) break;
                ApplyCategoryLevel(product, parent);
                parentId = parent.ParentId;
            }
        }

        private static void ApplyCategoryLevel(ErpProduct product, ErpCategory category)
        {
            var display = FirstNonEmpty(category.NameFr, category.NameNl, category.NameEn, category.ErpExternalId);
            switch (category.Level)
            {
                case "SubType":
                    product.SubTypeID = category.ErpExternalId;
                    product.SubTypeName = display;
                    break;
                case "Type":
                    product.TypeID = category.ErpExternalId;
                    product.TypeName = display;
                    break;
                case "MainType":
                    product.MainTypeID = category.ErpExternalId;
                    product.MainTypeName = display;
                    break;
                case "MainSubType":
                    product.MainSubTypeID = category.ErpExternalId;
                    product.MainSubTypeName = display;
                    break;
            }
        }

        private static string FirstNonEmpty(params string?[] values) =>
            values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim() ?? string.Empty;

        [HttpPost("sync/{erpId}")]
        [RequirePermission(Permissions.ProductUpdate)]
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
        [RequirePermission(Permissions.ProductUpdate)]
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
        [RequireAnyPermission(Permissions.ProductUpdate, Permissions.ErpChangeUpdate)]
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
        [RequirePermission(Permissions.ProductUpdate)]
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
        [RequirePermission(Permissions.ProductUpdate)]
        public async Task<IActionResult> CancelRunningSync(CancellationToken ct = default)
        {
            var cancelled = await _syncService.CancelRunningSyncAsync(ct);
            if (cancelled == null)
                return NotFound(new { message = "Aucune sync en cours" });
            return Ok(cancelled);
        }

        [HttpGet("sync-logs/{jobId}")]
        [RequireAnyPermission(Permissions.ProductRead, Permissions.ErpChangeRead)]
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
        [RequireAnyPermission(Permissions.ProductUpdate, Permissions.ErpChangeUpdate)]
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
        /// Importe GetProductMainTypes (+ Types) depuis l'ERP dans ErpCategories.
        /// Les MainTypes absents des produits locaux apparaissent ainsi dans la table.
        /// </summary>
        [HttpPost("sync-main-types")]
        [RequirePermission(Permissions.ProductUpdate)]
        [RequestTimeout(600_000)]
        public async Task<IActionResult> SyncMainTypes(
            [FromQuery] bool includeTypes = true,
            CancellationToken ct = default)
        {
            try
            {
                var result = await _syncService.SyncMainTypesFromErpAsync(includeTypes, ct);
                return Ok(result);
            }
            catch (Exception ex)
            {
                var msg = ex.InnerException?.Message ?? ex.Message;
                return StatusCode(500, new { message = "Sync MainTypes ERP échoué", detail = msg });
            }
        }

        /// <summary>
        /// Reconstruit ErpBrands + ErpCategories depuis les produits existants
        /// et rattache BrandId / CategoryId.
        /// Préfixe : sync MainTypes depuis l'API ERP pour ne manquer aucune catégorie racine.
        /// </summary>
        [HttpPost("rebuild-catalog")]
        [RequirePermission(Permissions.ProductUpdate)]
        [RequestTimeout(3_600_000)]
        public async Task<IActionResult> RebuildCatalog(CancellationToken ct = default)
        {
            try
            {
                var fromErp = await _syncService.SyncMainTypesFromErpAsync(includeTypes: true, ct);
                var result = await _catalogSync.RebuildFromProductsAsync(ct);
                return Ok(new
                {
                    fromErp,
                    fromProducts = result
                });
            }
            catch (Exception ex)
            {
                var msg = ex.InnerException?.Message ?? ex.Message;
                return StatusCode(500, new { message = "Rebuild catalog échoué", detail = msg });
            }
        }

        [HttpGet("brands")]
        [RequirePermission(Permissions.ProductRead)]
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
        [RequirePermission(Permissions.ProductRead)]
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

        /// <summary>
        /// Filtre catalogue par marques du fournisseur (Brand.Name LIKE '%token%'),
        /// token dérivé du nom fournisseur (ex. FF GROUP TOOL INDUSTRIES SA → FF GROUP).
        /// </summary>
        private async Task<IQueryable<ErpProduct>> ApplySupplierBrandFilterAsync(
            IQueryable<ErpProduct> query,
            int? supplierId,
            CancellationToken ct)
        {
            if (!supplierId.HasValue || supplierId.Value <= 0)
                return query;

            var supplier = await _storage.SelectSupplierByIdAsync(supplierId.Value);
            if (supplier == null)
                return query.Where(_ => false);

            var token = SupplierBrandMatcher.DeriveBrandToken(supplier.Name);
            if (string.IsNullOrWhiteSpace(token))
                return query.Where(_ => false);

            var tokenLower = token.ToLowerInvariant();
            var brandIds = await _storage.SelectAllErpBrands()
                .AsNoTracking()
                .Where(b => b.Name != null && b.Name.ToLower().Contains(tokenLower))
                .Select(b => b.Id)
                .ToListAsync(ct);

            if (brandIds.Count > 0)
            {
                return query.Where(p =>
                    (p.BrandId != null && brandIds.Contains(p.BrandId.Value))
                    || (p.Brand != null && p.Brand.ToLower().Contains(tokenLower)));
            }

            return query.Where(p => p.Brand != null && p.Brand.ToLower().Contains(tokenLower));
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
        [RequirePermission(Permissions.ErpChangeRead)]
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
        [RequirePermission(Permissions.ErpChangeUpdate)]
        public async Task<IActionResult> MarkChangesRead([FromBody] MarkChangesReadRequest request, CancellationToken ct = default)
        {
            if (request?.Ids == null || request.Ids.Count == 0)
                return BadRequest("ids required");

            await _syncService.MarkChangesAsReadAsync(request.Ids, ct);
            return Ok(new { marked = request.Ids.Count });
        }

        [HttpPost("changes/delete")]
        [RequirePermission(Permissions.ErpChangeDelete)]
        public async Task<IActionResult> DeleteChanges([FromBody] MarkChangesReadRequest request, CancellationToken ct = default)
        {
            if (request?.Ids == null || request.Ids.Count == 0)
                return BadRequest("ids required");

            var deleted = await _syncService.DeleteChangesAsync(request.Ids, ct);
            return Ok(new { deleted });
        }

        [HttpPost("changes/cleanup-formatting")]
        [RequirePermission(Permissions.ErpChangeUpdate)]
        public async Task<IActionResult> CleanupFormattingFalsePositives(CancellationToken ct = default)
        {
            var deleted = await _syncService.CleanupFormattingFalsePositivesAsync(ct);
            return Ok(new { deleted });
        }

        [HttpGet("sync-logs")]
        [RequireAnyPermission(Permissions.ProductRead, Permissions.ErpChangeRead)]
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
