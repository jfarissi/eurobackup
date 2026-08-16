using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Authorization;
using Authorize = Microsoft.AspNetCore.Authorization.AuthorizeAttribute;
using AllowAnonymous = Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute;
using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Models;
using Backup.Web.Api.Server.Models.Catalog;
using Backup.Web.Api.Server.Models.Security;
using Backup.Web.Api.Server.Services.ErpSync;
using Backup.Web.Api.Server.Services.Purchases;
using Backup.Web.Api.Server.Services.Tenancy;
using Backup.Web.Api.Server.Services.AutoParts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
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
        private readonly ICarApiImportService _carApiImport;
        private readonly IErpCatalogSyncService _catalogSync;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ErpSyncOptions _erpSyncOptions;
        private readonly ICompanyContextService _companyContext;

        public ErpProductController(
            IStorageBroker storage,
            IErpProductSyncService syncService,
            IErpExcelImportService excelImport,
            ICarApiImportService carApiImport,
            IErpCatalogSyncService catalogSync,
            IHttpClientFactory httpClientFactory,
            IOptions<ErpSyncOptions> erpSyncOptions,
            ICompanyContextService companyContext)
        {
            _storage = storage;
            _syncService = syncService;
            _excelImport = excelImport;
            _carApiImport = carApiImport;
            _catalogSync = catalogSync;
            _httpClientFactory = httpClientFactory;
            _erpSyncOptions = erpSyncOptions.Value;
            _companyContext = companyContext;
        }

        private async Task<IActionResult?> ForbidUnlessErpCatalogSyncAsync()
        {
            if (await _companyContext.CurrentCompanyHasErpCatalogSyncAsync())
                return null;
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                error = "Sync catalogue ERP non activée pour cette société."
            });
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
                return NotFound(new { message = "Image introuvable ou nom de fichier invalide", file = f });

            try
            {
                var client = _httpClientFactory.CreateClient("ErpProductImages");
                using var response = await client.GetAsync(upstream, HttpCompletionOption.ResponseHeadersRead, ct);
                if (!response.IsSuccessStatusCode)
                    return StatusCode((int)response.StatusCode, new { message = "Image absente sur le serveur ERP", upstream });

                var contentType = response.Content.Headers.ContentType?.ToString() ?? "image/jpeg";
                var bytes = await response.Content.ReadAsByteArrayAsync(ct);
                Response.Headers.CacheControl = "public,max-age=3600";
                return File(bytes, contentType);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status502BadGateway, new
                {
                    message = "Impossible de joindre le serveur images ERP",
                    upstream,
                    detail = ex.Message
                });
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
            [FromQuery] string? vehicleBrand = null,
            [FromQuery] string? vehicleModel = null,
            [FromQuery] string? vehicleYear = null,
            [FromQuery] string? vehicleFuel = null,
            [FromQuery] string? vehicleBody = null,
            [FromQuery] string? vehicleDrive = null,
            [FromQuery] string? vehicleTransmission = null,
            [FromQuery] string? vehicleEngine = null,
            [FromQuery] string? vehicleKType = null,
            CancellationToken ct = default)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 200);

            var vehicleYearParsed = TryParseVehicleYear(vehicleYear);
            if (!string.IsNullOrWhiteSpace(vehicleYear) && !vehicleYearParsed.HasValue)
            {
                return Ok(new { total = 0, page, pageSize, items = Array.Empty<ErpProduct>() });
            }

            var hasVehicleFilter =
                !string.IsNullOrWhiteSpace(vehicleBrand)
                || !string.IsNullOrWhiteSpace(vehicleModel)
                || vehicleYearParsed.HasValue
                || !string.IsNullOrWhiteSpace(vehicleFuel)
                || !string.IsNullOrWhiteSpace(vehicleBody)
                || !string.IsNullOrWhiteSpace(vehicleDrive)
                || !string.IsNullOrWhiteSpace(vehicleTransmission)
                || !string.IsNullOrWhiteSpace(vehicleEngine)
                || !string.IsNullOrWhiteSpace(vehicleKType);

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
                query = ApplyCategoryExternalIdFilter(query, typeId, "Type");
            else if (!string.IsNullOrWhiteSpace(mainTypeId))
                query = ApplyCategoryExternalIdFilter(query, mainTypeId, "MainType");

            if (hasVehicleFilter)
            {
                var matchingIds = await FindProductIdsByVehicleCompatAsync(
                    vehicleBrand, vehicleModel, vehicleYearParsed, vehicleFuel,
                    vehicleBody, vehicleDrive, vehicleTransmission, vehicleEngine, vehicleKType, ct);
                query = query.Where(p => matchingIds.Contains(p.Id));
            }

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

            // Autocomplete ligne doc : si filtre marque fournisseur exclut tout, retomber sur la recherche globale par q.
            if (total == 0
                && supplierId is > 0
                && !string.IsNullOrWhiteSpace(q))
            {
                query = _storage.SelectAllErpProducts().AsNoTracking();
                if (fromExcel.HasValue)
                    query = query.Where(p => p.FromExcel == fromExcel.Value);
                if (!string.IsNullOrWhiteSpace(dataSource))
                    query = query.Where(p => p.DataSource == dataSource);

                var term = q.Trim().ToLowerInvariant();
                query = query.Where(p =>
                    (p.Name != null && p.Name.ToLower().Contains(term))
                    || (p.Name2 != null && p.Name2.ToLower().Contains(term))
                    || (p.Reference != null && p.Reference.ToLower().Contains(term))
                    || (p.Ean != null && p.Ean.ToLower().Contains(term))
                    || (p.ErpProductId != null && p.ErpProductId.ToLower().Contains(term)));

                total = await query.CountAsync(ct);
            }

            // Prioriser la référence exacte / se terminant par le terme (saisie code produit).
            IOrderedQueryable<ErpProduct> ordered;
            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim().ToLowerInvariant();
                ordered = query
                    .OrderByDescending(p => p.Reference != null && p.Reference.ToLower() == term)
                    .ThenByDescending(p => p.ErpProductId != null && p.ErpProductId.ToLower() == term)
                    .ThenByDescending(p => p.Ean != null && p.Ean.ToLower() == term)
                    .ThenByDescending(p => p.Reference != null && p.Reference.ToLower().EndsWith(term))
                    .ThenBy(p => p.Name);
            }
            else
            {
                ordered = query.OrderBy(p => p.Name);
            }

            var items = await ordered
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            if (hasVehicleFilter && items.Count > 0)
            {
                await AttachVehicleFitmentsAsync(
                    items,
                    vehicleBrand,
                    vehicleModel,
                    vehicleYearParsed,
                    vehicleKType,
                    ct);
            }

            return Ok(new { total, page, pageSize, items });
        }

        private static int? TryParseVehicleYear(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            if (!int.TryParse(raw.Trim(), out var year)) return null;
            if (year < 1950 || year > 2035) return null;
            return year;
        }

        private async Task AttachVehicleFitmentsAsync(
            List<ErpProduct> items,
            string? vehicleBrand,
            string? vehicleModel,
            int? vehicleYear,
            string? vehicleKType,
            CancellationToken ct)
        {
            var productIds = items.Select(p => p.Id).ToList();
            var fitments = await _storage.SelectAllErpProductVehicles()
                .AsNoTracking()
                .Where(v => productIds.Contains(v.ProductId))
                .ToListAsync(ct);

            foreach (var product in items)
            {
                var fit = PickBestVehicleFitment(
                    fitments.Where(v => v.ProductId == product.Id).ToList(),
                    vehicleBrand,
                    vehicleModel,
                    vehicleYear,
                    vehicleKType);
                if (fit == null) continue;

                product.VehicleMake = fit.Make;
                product.VehicleModel = fit.Model;
                product.VehicleTypeName = fit.TypeName;
                product.VehicleYearFrom = fit.YearFrom;
                product.VehicleYearTo = fit.YearTo;
                product.VehicleEngineCode = fit.EngineCode;
                product.VehicleKType = fit.KType;
                product.VehicleFuelType = fit.FuelType;
            }
        }

        private static ErpProductVehicle? PickBestVehicleFitment(
            List<ErpProductVehicle> candidates,
            string? vehicleBrand,
            string? vehicleModel,
            int? vehicleYear,
            string? vehicleKType)
        {
            if (candidates.Count == 0) return null;

            ErpProductVehicle? Pick(Func<ErpProductVehicle, bool> pred) =>
                candidates.FirstOrDefault(pred);

            if (!string.IsNullOrWhiteSpace(vehicleKType))
            {
                var k = vehicleKType.Trim();
                var byK = Pick(v => string.Equals(v.KType, k, StringComparison.OrdinalIgnoreCase));
                if (byK != null) return byK;
            }

            if (!string.IsNullOrWhiteSpace(vehicleBrand) && !string.IsNullOrWhiteSpace(vehicleModel))
            {
                var byMakeModel = Pick(v =>
                    VehicleMakeAliases.Matches(v.Make, vehicleBrand)
                    && (string.Equals(v.Model, vehicleModel, StringComparison.OrdinalIgnoreCase)
                        || v.Model.StartsWith(vehicleModel, StringComparison.OrdinalIgnoreCase)));
                if (byMakeModel != null) return byMakeModel;
            }

            if (!string.IsNullOrWhiteSpace(vehicleBrand))
            {
                var byMake = Pick(v => VehicleMakeAliases.Matches(v.Make, vehicleBrand));
                if (byMake != null) return byMake;
            }

            if (vehicleYear.HasValue)
            {
                var year = vehicleYear.Value;
                var maxOpenYear = DateTime.UtcNow.Year + 1;
                var byYear = Pick(v =>
                    (v.YearFrom.HasValue || v.YearTo.HasValue)
                    && (!v.YearFrom.HasValue || v.YearFrom <= year)
                    && (v.YearTo.HasValue ? v.YearTo >= year : year <= maxOpenYear));
                if (byYear != null) return byYear;
            }

            return candidates[0];
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
            public decimal? UnitPrice { get; set; }
            public decimal? VatPercent { get; set; }
            public int? BrandId { get; set; }
            public string? BrandName { get; set; }
            public int? CategoryId { get; set; }
            public string? SupplierName { get; set; }
            public bool IsDropship { get; set; }
            public int? DropshipSupplierId { get; set; }
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
                UnitPrice = request.UnitPrice ?? request.PurchasePrice,
                PriceHT = request.PurchasePrice ?? request.UnitPrice,
                TypeVatPerc = request.VatPercent ?? 21m,
                DataSource = "Manual",
                FromExcel = false,
                IsDropship = request.IsDropship,
                DropshipSupplierId = request.IsDropship ? request.DropshipSupplierId : null,
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

        public class UpdateErpProductRequest
        {
            public string? Name { get; set; }
            public string? Name2 { get; set; }
            public string? Reference { get; set; }
            public string? Ean { get; set; }
            public decimal? PurchasePrice { get; set; }
            public decimal? UnitPrice { get; set; }
            public decimal? VatPercent { get; set; }
            public int? BrandId { get; set; }
            public string? BrandName { get; set; }
            public int? CategoryId { get; set; }
            public string? Comment { get; set; }
            public bool? Archived { get; set; }
            public decimal? Weight { get; set; }
            public decimal? Height { get; set; }
            public decimal? Width { get; set; }
            public decimal? Depth { get; set; }
            public bool? IsDropship { get; set; }
            public int? DropshipSupplierId { get; set; }
        }

        [HttpPut("{id:int}")]
        [RequirePermission(Permissions.ProductUpdate)]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateErpProductRequest? request, CancellationToken ct = default)
        {
            if (request == null) return BadRequest(new { message = "Body required" });

            var existing = await _storage.SelectErpProductByIdAsync(id);
            if (existing == null) return NotFound();

            if (request.Name != null) existing.Name = request.Name.Trim();
            if (request.Name2 != null) existing.Name2 = string.IsNullOrWhiteSpace(request.Name2) ? null : request.Name2.Trim();
            if (request.Reference != null) existing.Reference = request.Reference.Trim();
            if (request.Ean != null) existing.Ean = string.IsNullOrWhiteSpace(request.Ean) ? null : request.Ean.Trim();
            if (request.PurchasePrice.HasValue)
            {
                existing.CPrice = request.PurchasePrice;
                existing.PriceHT = request.PurchasePrice;
            }
            if (request.UnitPrice.HasValue) existing.UnitPrice = request.UnitPrice;
            if (request.VatPercent.HasValue) existing.TypeVatPerc = request.VatPercent;
            if (request.Comment != null) existing.Comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim();
            if (request.Archived.HasValue) existing.Archived = request.Archived;
            if (request.Weight.HasValue) existing.Weight = request.Weight;
            if (request.Height.HasValue) existing.Height = request.Height;
            if (request.Width.HasValue) existing.Width = request.Width;
            if (request.Depth.HasValue) existing.Depth = request.Depth;
            if (request.IsDropship.HasValue)
            {
                existing.IsDropship = request.IsDropship.Value;
                existing.DropshipSupplierId = request.IsDropship.Value ? request.DropshipSupplierId : null;
            }
            else if (request.DropshipSupplierId.HasValue && existing.IsDropship)
            {
                existing.DropshipSupplierId = request.DropshipSupplierId;
            }

            if (request.BrandId.HasValue || !string.IsNullOrWhiteSpace(request.BrandName))
            {
                var (brandId, brandName) = await ResolveBrandForCreateAsync(
                    request.BrandId, request.BrandName, null, ct);
                existing.BrandId = brandId;
                existing.Brand = brandName;
            }

            if (request.CategoryId is > 0)
                await ApplyCategoryToProductAsync(existing, request.CategoryId.Value, ct);

            existing.UpdatedAt = DateTime.UtcNow;
            existing.UpdatedBy = User.Identity?.Name;
            var updated = await _storage.UpdateErpProductAsync(existing);
            return Ok(updated);
        }

        [HttpDelete("{id:int}")]
        [RequirePermission(Permissions.ProductDelete)]
        public async Task<IActionResult> Archive(int id, CancellationToken ct = default)
        {
            var existing = await _storage.SelectErpProductByIdAsync(id);
            if (existing == null) return NotFound();

            existing.Archived = true;
            existing.UpdatedAt = DateTime.UtcNow;
            existing.UpdatedBy = User.Identity?.Name;
            await _storage.UpdateErpProductAsync(existing);
            return NoContent();
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

                var created = await _storage.InsertErpBrandAsync(new ErpBrand
                {
                    Name = name,
                    Slug = SlugifyBrand(name),
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    CreatedBy = User.Identity?.Name
                });
                return (created.Id, created.Name);
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

        private static string SlugifyBrand(string input)
        {
            var normalized = input.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (var c in normalized)
            {
                var cat = CharUnicodeInfo.GetUnicodeCategory(c);
                if (cat == UnicodeCategory.NonSpacingMark) continue;
                if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
                else if (char.IsWhiteSpace(c) || c == '-' || c == '_') sb.Append('-');
            }
            var slug = sb.ToString().Trim('-');
            while (slug.Contains("--", StringComparison.Ordinal))
                slug = slug.Replace("--", "-", StringComparison.Ordinal);
            return string.IsNullOrEmpty(slug) ? "brand" : slug;
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
            if (await ForbidUnlessErpCatalogSyncAsync() is { } forbidden) return forbidden;
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
            if (await ForbidUnlessErpCatalogSyncAsync() is { } forbidden) return forbidden;
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
            if (await ForbidUnlessErpCatalogSyncAsync() is { } forbidden) return forbidden;
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
            if (await ForbidUnlessErpCatalogSyncAsync() is { } forbidden) return forbidden;
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
            if (await ForbidUnlessErpCatalogSyncAsync() is { } forbidden) return forbidden;
            var cancelled = await _syncService.CancelRunningSyncAsync(ct);
            if (cancelled == null)
                return NotFound(new { message = "Aucune sync en cours" });
            return Ok(cancelled);
        }

        [HttpGet("sync-logs/{jobId}")]
        [RequireAnyPermission(Permissions.ProductRead, Permissions.ErpChangeRead)]
        public async Task<IActionResult> GetSyncLogByJobId([FromRoute] string jobId, CancellationToken ct = default)
        {
            if (await ForbidUnlessErpCatalogSyncAsync() is { } forbidden) return forbidden;
            var log = await _syncService.GetSyncLogByJobIdAsync(jobId, ct);
            if (log == null)
                return NotFound(new { message = $"Job {jobId} introuvable" });
            return Ok(log);
        }

        /// <summary>
        /// Importe le catalogue pièces auto depuis lifeofcapo/car-api (car-parts.json).
        /// Les fichiers sont lus depuis ErpSync:CarApiDataPath ou Data/CarApi par défaut.
        /// </summary>
        [HttpPost("import-car-api")]
        [RequirePermission(Permissions.ProductUpdate)]
        [RequestTimeout(600_000)]
        public async Task<IActionResult> ImportCarApi(
            [FromQuery] string? path = null,
            [FromQuery] bool importParts = true,
            [FromQuery] bool importVehicleBrands = false,
            [FromQuery] bool applyFrenchNames = true,
            [FromQuery] bool ensureVehicleAttribute = true,
            [FromQuery] bool rebuildCatalog = false,
            CancellationToken ct = default)
        {
            try
            {
                var companyId = _companyContext.GetCurrentCompanyId();
                var importResult = await _carApiImport.ImportAsync(
                    path,
                    importParts,
                    importVehicleBrands,
                    applyFrenchNames,
                    ensureVehicleAttribute,
                    companyId,
                    User.Identity?.Name,
                    ct);
                object? catalog = null;
                if (rebuildCatalog)
                    catalog = await _catalogSync.RebuildFromProductsAsync(ct);

                return Ok(new { import = importResult, catalog });
            }
            catch (Exception ex)
            {
                var msg = ex.InnerException?.Message ?? ex.Message;
                return StatusCode(500, new { message = "Import car-api échoué", detail = msg });
            }
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
            if (await ForbidUnlessErpCatalogSyncAsync() is { } forbidden) return forbidden;
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
            if (await ForbidUnlessErpCatalogSyncAsync() is { } forbidden) return forbidden;
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
            if (await ForbidUnlessErpCatalogSyncAsync() is { } forbidden) return forbidden;
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

        /// <summary>Fitments véhicule d'un produit (ErpProductVehicles) — détail pièce auto.</summary>
        [HttpGet("{id:int}/vehicles")]
        [RequirePermission(Permissions.ProductRead)]
        public async Task<IActionResult> GetProductVehicles(int id, CancellationToken ct = default)
        {
            var exists = await _storage.SelectAllErpProducts()
                .AsNoTracking()
                .AnyAsync(p => p.Id == id, ct);
            if (!exists)
                return NotFound(new { message = $"Produit {id} introuvable." });

            var vehicles = await _storage.SelectAllErpProductVehicles()
                .AsNoTracking()
                .Where(v => v.ProductId == id)
                .OrderBy(v => v.Make)
                .ThenBy(v => v.Model)
                .ThenBy(v => v.YearFrom)
                .ThenBy(v => v.TypeName)
                .Select(v => new
                {
                    v.Id,
                    v.Make,
                    v.Model,
                    v.TypeName,
                    v.YearFrom,
                    v.YearTo,
                    v.EngineCode,
                    v.KType,
                    v.BodyType,
                    v.FuelType,
                    v.DriveType,
                    v.Transmission,
                    v.PowerKW,
                    v.PowerHP,
                    v.Ccm,
                    v.Cylinders,
                    v.Valves
                })
                .ToListAsync(ct);

            return Ok(vehicles);
        }

        /// <summary>Cross-références OEM d'un produit.</summary>
        [HttpGet("{id:int}/oems")]
        [RequirePermission(Permissions.ProductRead)]
        public async Task<IActionResult> GetProductOems(int id, CancellationToken ct = default)
        {
            var exists = await _storage.SelectAllErpProducts()
                .AsNoTracking()
                .AnyAsync(p => p.Id == id, ct);
            if (!exists)
                return NotFound(new { message = $"Produit {id} introuvable." });

            var oems = await _storage.SelectAllErpOemCrossReferences()
                .AsNoTracking()
                .Where(o => o.ProductId == id)
                .OrderByDescending(o => o.IsOriginal)
                .ThenBy(o => o.OemNumber)
                .Select(o => new
                {
                    o.Id,
                    o.OemNumber,
                    o.Brand,
                    o.IsOriginal
                })
                .ToListAsync(ct);

            return Ok(oems);
        }

        /// <summary>Recherche produits par numéro OEM (exact puis préfixe / contient).</summary>
        [HttpGet("by-oem")]
        [RequirePermission(Permissions.ProductRead)]
        public async Task<IActionResult> SearchByOem(
            [FromQuery] string? q = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50,
            CancellationToken ct = default)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var term = (q ?? string.Empty).Trim();
            if (term.Length < 3)
                return Ok(new { total = 0, page, pageSize, query = term, items = Array.Empty<object>() });

            var termLower = term.ToLowerInvariant();
            var termCompact = NormalizeOem(term);

            var oemRows = await _storage.SelectAllErpOemCrossReferences()
                .AsNoTracking()
                .Where(o => o.OemNumber != null && o.OemNumber != "")
                .Select(o => new { o.ProductId, o.OemNumber, o.Brand, o.IsOriginal })
                .ToListAsync(ct);

            var matched = oemRows
                .Select(o =>
                {
                    var oemLower = o.OemNumber.ToLowerInvariant();
                    var oemCompact = NormalizeOem(o.OemNumber);
                    var score =
                        oemLower == termLower || oemCompact == termCompact ? 3
                        : oemLower.StartsWith(termLower) || oemCompact.StartsWith(termCompact) ? 2
                        : oemLower.Contains(termLower) || oemCompact.Contains(termCompact) ? 1
                        : 0;
                    return new { o.ProductId, o.OemNumber, o.Brand, o.IsOriginal, score };
                })
                .Where(x => x.score > 0)
                .ToList();

            if (matched.Count == 0)
                return Ok(new { total = 0, page, pageSize, query = term, items = Array.Empty<object>() });

            var bestByProduct = matched
                .GroupBy(x => x.ProductId)
                .Select(g => g.OrderByDescending(x => x.score).ThenByDescending(x => x.IsOriginal).First())
                .OrderByDescending(x => x.score)
                .ThenBy(x => x.OemNumber)
                .ToList();

            var total = bestByProduct.Count;
            var pageHits = bestByProduct.Skip((page - 1) * pageSize).Take(pageSize).ToList();
            var productIds = pageHits.Select(x => x.ProductId).ToList();

            var products = await _storage.SelectAllErpProducts()
                .AsNoTracking()
                .Where(p => productIds.Contains(p.Id))
                .ToListAsync(ct);
            var productMap = products.ToDictionary(p => p.Id);

            var items = pageHits
                .Where(h => productMap.ContainsKey(h.ProductId))
                .Select(h =>
                {
                    var p = productMap[h.ProductId];
                    return new
                    {
                        productId = p.Id,
                        erpProductId = p.ErpProductId,
                        name = p.Name,
                        reference = p.Reference,
                        brand = p.Brand,
                        unitPrice = p.UnitPrice ?? p.RPrice,
                        stockQuantity = p.StockQuantity,
                        matchedOem = h.OemNumber,
                        oemBrand = h.Brand,
                        isOriginal = h.IsOriginal
                    };
                })
                .ToList();

            return Ok(new { total, page, pageSize, query = term, items });
        }

        private static string NormalizeOem(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            var sb = new StringBuilder(value.Length);
            foreach (var c in value)
            {
                if (char.IsLetterOrDigit(c))
                    sb.Append(char.ToLowerInvariant(c));
            }
            return sb.ToString();
        }

        [HttpGet("vehicle-makes")]
        [RequirePermission(Permissions.ProductRead)]
        public async Task<IActionResult> GetVehicleMakes(CancellationToken ct = default)
        {
            var makes = await _storage.SelectAllErpProductVehicles()
                .AsNoTracking()
                .Where(v => v.Make != null && v.Make != "")
                .Select(v => v.Make)
                .Distinct()
                .OrderBy(m => m)
                .ToListAsync(ct);
            return Ok(makes);
        }

        [HttpGet("vehicle-models")]
        [RequirePermission(Permissions.ProductRead)]
        public async Task<IActionResult> GetVehicleModels(
            [FromQuery] string? make = null,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(make))
                return Ok(Array.Empty<string>());

            var makeAliases = VehicleMakeAliases.Expand(make);
            var models = await _storage.SelectAllErpProductVehicles()
                .AsNoTracking()
                .Where(v => v.Make != null && makeAliases.Contains(v.Make.ToLower())
                            && v.Model != null && v.Model != "")
                .Select(v => v.Model)
                .Distinct()
                .OrderBy(m => m)
                .ToListAsync(ct);
            return Ok(models);
        }

        /// <summary>Types de carburant distincts (optionnellement filtrés par marque/modèle).</summary>
        [HttpGet("vehicle-fuel-types")]
        [RequirePermission(Permissions.ProductRead)]
        public async Task<IActionResult> GetVehicleFuelTypes(
            [FromQuery] string? make = null,
            [FromQuery] string? model = null,
            CancellationToken ct = default)
        {
            var facets = await LoadVehicleFacetsAsync(make, model, ct);
            return Ok(facets.Fuels);
        }

        /// <summary>Facettes véhicule pour la section filtres (carburant, carrosserie, transmission…).</summary>
        [HttpGet("vehicle-facets")]
        [RequirePermission(Permissions.ProductRead)]
        public async Task<IActionResult> GetVehicleFacets(
            [FromQuery] string? make = null,
            [FromQuery] string? model = null,
            CancellationToken ct = default)
        {
            var facets = await LoadVehicleFacetsAsync(make, model, ct);
            return Ok(new
            {
                fuels = facets.Fuels,
                bodyTypes = facets.BodyTypes,
                driveTypes = facets.DriveTypes,
                transmissions = facets.Transmissions
            });
        }

        private async Task<(List<string> Fuels, List<string> BodyTypes, List<string> DriveTypes, List<string> Transmissions)>
            LoadVehicleFacetsAsync(string? make, string? model, CancellationToken ct)
        {
            var query = _storage.SelectAllErpProductVehicles().AsNoTracking();

            if (!string.IsNullOrWhiteSpace(make))
            {
                var makeAliases = VehicleMakeAliases.Expand(make);
                query = query.Where(v => v.Make != null && makeAliases.Contains(v.Make.ToLower()));
            }

            if (!string.IsNullOrWhiteSpace(model))
            {
                var modelLower = model.Trim().ToLowerInvariant();
                query = query.Where(v =>
                    v.Model != null
                    && (v.Model.ToLower() == modelLower || v.Model.ToLower().StartsWith(modelLower)));
            }

            var fuels = await query
                .Where(v => v.FuelType != null && v.FuelType != "")
                .Select(v => v.FuelType!)
                .Distinct()
                .OrderBy(f => f)
                .ToListAsync(ct);
            var bodies = await query
                .Where(v => v.BodyType != null && v.BodyType != "")
                .Select(v => v.BodyType!)
                .Distinct()
                .OrderBy(f => f)
                .ToListAsync(ct);
            var drives = await query
                .Where(v => v.DriveType != null && v.DriveType != "")
                .Select(v => v.DriveType!)
                .Distinct()
                .OrderBy(f => f)
                .ToListAsync(ct);
            var transmissions = await query
                .Where(v => v.Transmission != null && v.Transmission != "")
                .Select(v => v.Transmission!)
                .Distinct()
                .OrderBy(f => f)
                .ToListAsync(ct);

            return (fuels, bodies, drives, transmissions);
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
                    .Select(b => new
                    {
                        b.Id,
                        b.Name,
                        b.Slug,
                        b.LogoUrl,
                        b.WebsiteUrl,
                        b.Description,
                        b.IsActive,
                        b.CreatedBy,
                        b.UpdatedBy
                    })
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
                .Select(b => new
                {
                    b.Id,
                    b.Name,
                    b.Slug,
                    b.LogoUrl,
                    b.WebsiteUrl,
                    b.Description,
                    b.IsActive,
                    b.CreatedBy,
                    b.UpdatedBy
                })
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
            /// <summary>Catalogue pièces auto plat (RapidAPI / CarAPI) : exclut les Type EuroBrico sous MainType ERP.</summary>
            [FromQuery] bool flatCatalog = false,
            CancellationToken ct = default)
        {
            try
            {
                var query = _storage.SelectAllErpCategories().AsNoTracking();
                if (!string.IsNullOrWhiteSpace(level))
                    query = query.Where(c => c.Level == level);
                if (parentId.HasValue)
                    query = query.Where(c => c.ParentId == parentId.Value);

                if (flatCatalog
                    && !string.IsNullOrWhiteSpace(level)
                    && level.Equals("Type", StringComparison.OrdinalIgnoreCase))
                {
                    var carApiMainIds = await _storage.SelectAllErpCategories()
                        .AsNoTracking()
                        .Where(c => c.Level == "MainType" && c.ErpExternalId == "CARAPI-MAIN")
                        .Select(c => c.Id)
                        .ToListAsync(ct);
                    if (carApiMainIds.Count == 0)
                        query = query.Where(c => c.ParentId == null);
                    else
                        query = query.Where(c => c.ParentId == null || carApiMainIds.Contains(c.ParentId.Value));
                }

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
            catch (Exception ex)
            {
                var msg = ex.InnerException?.Message ?? ex.Message;
                return StatusCode(500, new { message = "Lecture des catégories impossible", detail = msg });
            }
        }

        private async Task<List<string>> GetDistinctCategoryExternalIdsAsync(
            IQueryable<ErpProduct> products,
            string? level,
            CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(level))
                return new List<string>();

            if (level.Equals("SubType", StringComparison.OrdinalIgnoreCase))
            {
                return await products
                    .Where(p => p.SubTypeID != null && p.SubTypeID != "")
                    .Select(p => p.SubTypeID!)
                    .Distinct()
                    .ToListAsync(ct);
            }

            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (level.Equals("MainType", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var id in await products
                    .Where(p => p.MainTypeID != null && p.MainTypeID != "")
                    .Select(p => p.MainTypeID!)
                    .Distinct()
                    .ToListAsync(ct))
                    ids.Add(id);
            }
            else if (level.Equals("Type", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var id in await products
                    .Where(p => p.TypeID != null && p.TypeID != "")
                    .Select(p => p.TypeID!)
                    .Distinct()
                    .ToListAsync(ct))
                    ids.Add(id);
            }

            var categoryIds = await products
                .Where(p => p.CategoryId != null)
                .Select(p => p.CategoryId!.Value)
                .Distinct()
                .ToListAsync(ct);

            if (categoryIds.Count > 0)
            {
                var fromCategory = await _storage.SelectAllErpCategories()
                    .AsNoTracking()
                    .Where(c => categoryIds.Contains(c.Id)
                        && c.Level == level
                        && c.ErpExternalId != null
                        && c.ErpExternalId != "")
                    .Select(c => c.ErpExternalId)
                    .Distinct()
                    .ToListAsync(ct);

                foreach (var id in fromCategory)
                    ids.Add(id);
            }

            return ids.ToList();
        }

        /// <summary>
        /// Produits compatibles véhicule via ErpProductVehicles (+ fallback attribut vehicle_compat).
        /// </summary>
        private async Task<HashSet<int>> FindProductIdsByVehicleCompatAsync(
            string? vehicleBrand,
            string? vehicleModel,
            int? vehicleYear,
            string? vehicleFuel,
            string? vehicleBody,
            string? vehicleDrive,
            string? vehicleTransmission,
            string? vehicleEngine,
            string? vehicleKType,
            CancellationToken ct)
        {
            var ids = new HashSet<int>();
            var brand = vehicleBrand?.Trim();
            var model = vehicleModel?.Trim();
            var fuel = vehicleFuel?.Trim();
            var body = vehicleBody?.Trim();
            var drive = vehicleDrive?.Trim();
            var transmission = vehicleTransmission?.Trim();
            var engine = vehicleEngine?.Trim();
            var kType = vehicleKType?.Trim();
            var fuelAliases = ExpandFuelAliases(fuel);
            var hasTechFilter = fuelAliases.Count > 0
                || !string.IsNullOrWhiteSpace(body)
                || !string.IsNullOrWhiteSpace(drive)
                || !string.IsNullOrWhiteSpace(transmission)
                || !string.IsNullOrWhiteSpace(engine)
                || !string.IsNullOrWhiteSpace(kType);

            // Raccourci K-Type : filtre TecDoc prioritaire (identité véhicule exacte — l'année n'affine pas).
            if (!string.IsNullOrWhiteSpace(kType))
            {
                var k = kType.ToLowerInvariant();
                var kQuery = _storage.SelectAllErpProductVehicles().AsNoTracking()
                    .Where(v => v.KType != null && v.KType.ToLower() == k);
                foreach (var id in await kQuery.Select(v => v.ProductId).Distinct().ToListAsync(ct))
                    ids.Add(id);
                if (ids.Count > 0)
                    return ids;
            }

            var vehicleQuery = _storage.SelectAllErpProductVehicles().AsNoTracking();
            if (!string.IsNullOrWhiteSpace(brand))
            {
                var makeAliases = VehicleMakeAliases.Expand(brand);
                vehicleQuery = vehicleQuery.Where(v => makeAliases.Contains(v.Make.ToLower()));
            }

            if (!string.IsNullOrWhiteSpace(model))
            {
                var modelLower = model.ToLowerInvariant();
                vehicleQuery = vehicleQuery.Where(v =>
                    v.Model != null && v.Model != ""
                    && (v.Model.ToLower() == modelLower
                        || v.Model.ToLower().StartsWith(modelLower)));
            }

            if (vehicleYear.HasValue)
            {
                var year = vehicleYear.Value;
                // Année hors plage réaliste → aucun résultat (évite 90000000 matchant YearTo NULL)
                if (year < 1950 || year > 2035)
                {
                    return new HashSet<int>();
                }

                var maxOpenYear = DateTime.UtcNow.Year + 1;
                vehicleQuery = vehicleQuery.Where(v =>
                    (v.YearFrom != null || v.YearTo != null)
                    && (v.YearFrom == null || v.YearFrom <= year)
                    && (v.YearTo != null
                        ? v.YearTo >= year
                        // YearTo ouvert : limité à l'année courante + 1 (pas l'infini)
                        : year <= maxOpenYear));
            }

            if (!string.IsNullOrWhiteSpace(body))
            {
                var bodyLower = body.ToLowerInvariant();
                vehicleQuery = vehicleQuery.Where(v =>
                    v.BodyType != null && v.BodyType.ToLower() == bodyLower);
            }

            if (!string.IsNullOrWhiteSpace(drive))
            {
                var driveLower = drive.ToLowerInvariant();
                vehicleQuery = vehicleQuery.Where(v =>
                    v.DriveType != null && v.DriveType.ToLower() == driveLower);
            }

            if (!string.IsNullOrWhiteSpace(transmission))
            {
                var transmissionLower = transmission.ToLowerInvariant();
                vehicleQuery = vehicleQuery.Where(v =>
                    v.Transmission != null && v.Transmission.ToLower() == transmissionLower);
            }

            if (!string.IsNullOrWhiteSpace(engine))
            {
                var engineLower = engine.ToLowerInvariant();
                vehicleQuery = vehicleQuery.Where(v =>
                    v.EngineCode != null && v.EngineCode.ToLower().Contains(engineLower));
            }

            if (fuelAliases.Count > 0)
            {
                var rows = await vehicleQuery
                    .Where(v => v.FuelType != null && v.FuelType != "")
                    .Select(v => new { v.ProductId, Fuel = v.FuelType! })
                    .ToListAsync(ct);
                foreach (var row in rows)
                {
                    var ft = row.Fuel.ToLowerInvariant();
                    if (fuelAliases.Any(a => ft.Contains(a)))
                        ids.Add(row.ProductId);
                }
            }
            else
            {
                foreach (var id in await vehicleQuery.Select(v => v.ProductId).Distinct().ToListAsync(ct))
                    ids.Add(id);
            }

            // Fallback JSON attribute (compat manuelle) si pas de critères techniques ErpProductVehicles.
            if (!hasTechFilter)
            {
                var defIds = await _storage.SelectAllErpProductAttributeDefinitions()
                    .AsNoTracking()
                    .Where(d => d.Code == CarApiCatalogService.VehicleCompatAttributeCode && d.IsActive)
                    .Select(d => d.Id)
                    .ToListAsync(ct);

                if (defIds.Count > 0)
                {
                    var values = await _storage.SelectAllErpProductAttributeValues()
                        .AsNoTracking()
                        .Where(v => defIds.Contains(v.AttributeId) && v.Value != null && v.Value != "")
                        .Select(v => new { v.ProductId, v.Value })
                        .ToListAsync(ct);

                    foreach (var row in values)
                    {
                        if (!VehicleCompatMatches(row.Value, brand, model, vehicleYear))
                            continue;
                        ids.Add(row.ProductId);
                    }
                }
            }

            return ids;
        }

        /// <summary>
        /// Étend un filtre carburant UI (Diesel / Essence…) vers alias TecDoc (Petrol, Gasoline…).
        /// </summary>
        private static List<string> ExpandFuelAliases(string? fuel)
        {
            if (string.IsNullOrWhiteSpace(fuel))
                return new List<string>();

            var f = fuel.Trim().ToLowerInvariant();
            var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { f };

            if (f.Contains("diesel") || f.Contains("gazole"))
            {
                aliases.Add("diesel");
                aliases.Add("gazole");
            }
            else if (f.Contains("essence") || f.Contains("petrol") || f.Contains("gasoline") || f.Contains("benzine"))
            {
                aliases.Add("essence");
                aliases.Add("petrol");
                aliases.Add("gasoline");
                aliases.Add("benzine");
            }
            else if (f.Contains("elect") || f.Contains("élect"))
            {
                aliases.Add("electric");
                aliases.Add("électrique");
                aliases.Add("electrique");
            }
            else if (f.Contains("hybrid") || f.Contains("hybride"))
            {
                aliases.Add("hybrid");
                aliases.Add("hybride");
            }
            else if (f.Contains("lpg") || f.Contains("gpl") || f == "gas")
            {
                aliases.Add("lpg");
                aliases.Add("gpl");
                aliases.Add("gas");
            }

            return aliases.Select(a => a.ToLowerInvariant()).Distinct().ToList();
        }

        private static bool VehicleCompatMatches(
            string json,
            string? brand,
            string? model,
            int? year)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind != JsonValueKind.Array)
                    return false;

                foreach (var entry in doc.RootElement.EnumerateArray())
                {
                    var entryBrand = entry.TryGetProperty("brand", out var b) ? b.GetString() : null;
                    var entryModel = entry.TryGetProperty("model", out var m) ? m.GetString() : null;
                    int? yearFrom = entry.TryGetProperty("yearFrom", out var yf) && yf.ValueKind == JsonValueKind.Number
                        ? yf.GetInt32()
                        : null;
                    int? yearTo = entry.TryGetProperty("yearTo", out var yt) && yt.ValueKind == JsonValueKind.Number
                        ? yt.GetInt32()
                        : null;

                    if (!string.IsNullOrWhiteSpace(brand)
                        && !VehicleMakeAliases.Matches(entryBrand, brand))
                        continue;

                    if (!string.IsNullOrWhiteSpace(model))
                    {
                        var em = (entryModel ?? "").Trim();
                        if (!string.Equals(em, model, StringComparison.OrdinalIgnoreCase)
                            && !em.StartsWith(model, StringComparison.OrdinalIgnoreCase))
                            continue;
                    }

                    if (year.HasValue)
                    {
                        if (year.Value < 1950 || year.Value > 2035)
                            continue;
                        // Sans plage d'années → ne matche pas un filtre année explicite
                        if (!yearFrom.HasValue && !yearTo.HasValue)
                            continue;
                        if (yearFrom.HasValue && year.Value < yearFrom.Value)
                            continue;
                        if (yearTo.HasValue)
                        {
                            if (year.Value > yearTo.Value)
                                continue;
                        }
                        else if (year.Value > DateTime.UtcNow.Year + 1)
                        {
                            continue;
                        }
                    }

                    return true;
                }
            }
            catch (JsonException)
            {
                return false;
            }

            return false;
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
                return query; // Pas de token fiable → ne pas vider le catalogue.

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
                query = ApplyCategoryExternalIdFilter(query, typeId, "Type");
            else if (!string.IsNullOrWhiteSpace(mainTypeId))
                query = ApplyCategoryExternalIdFilter(query, mainTypeId, "MainType");

            return query;
        }

        /// <summary>
        /// Filtre catégorie par ErpExternalId : champs MainTypeID/TypeID OU CategoryId (catalogue RapidAPI plat).
        /// </summary>
        private IQueryable<ErpProduct> ApplyCategoryExternalIdFilter(
            IQueryable<ErpProduct> query,
            string externalId,
            string level)
        {
            var ext = externalId.Trim();
            var catIds = _storage.SelectAllErpCategories()
                .AsNoTracking()
                .Where(c => c.ErpExternalId == ext)
                .Select(c => c.Id);

            if (level.Equals("Type", StringComparison.OrdinalIgnoreCase))
            {
                return query.Where(p =>
                    p.TypeID == ext
                    || (p.CategoryId != null && catIds.Contains(p.CategoryId.Value)));
            }

            return query.Where(p =>
                p.MainTypeID == ext
                || (p.CategoryId != null && catIds.Contains(p.CategoryId.Value)));
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
            if (await ForbidUnlessErpCatalogSyncAsync() is { } forbidden) return forbidden;
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
            if (await ForbidUnlessErpCatalogSyncAsync() is { } forbidden) return forbidden;
            if (request?.Ids == null || request.Ids.Count == 0)
                return BadRequest("ids required");

            await _syncService.MarkChangesAsReadAsync(request.Ids, ct);
            return Ok(new { marked = request.Ids.Count });
        }

        [HttpPost("changes/delete")]
        [RequirePermission(Permissions.ErpChangeDelete)]
        public async Task<IActionResult> DeleteChanges([FromBody] MarkChangesReadRequest request, CancellationToken ct = default)
        {
            if (await ForbidUnlessErpCatalogSyncAsync() is { } forbidden) return forbidden;
            if (request?.Ids == null || request.Ids.Count == 0)
                return BadRequest("ids required");

            var deleted = await _syncService.DeleteChangesAsync(request.Ids, ct);
            return Ok(new { deleted });
        }

        [HttpPost("changes/cleanup-formatting")]
        [RequirePermission(Permissions.ErpChangeUpdate)]
        public async Task<IActionResult> CleanupFormattingFalsePositives(CancellationToken ct = default)
        {
            if (await ForbidUnlessErpCatalogSyncAsync() is { } forbidden) return forbidden;
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
            if (await ForbidUnlessErpCatalogSyncAsync() is { } forbidden) return forbidden;            page = Math.Max(1, page);
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
