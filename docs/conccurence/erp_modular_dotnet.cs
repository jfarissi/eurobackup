// ============================================================
// ERP MODULAIRE .NET CORE 8
// Core générique + Module Pièces Auto (et autres)
// ============================================================
// Structure du projet recommandée :
//
// MyErp.Api/
// ├── Controllers/
// │   ├── ProductsController.cs          (générique)
// │   └── AutoPartsController.cs         (module pièces auto)
// ├── Models/
// │   ├── Entities/                      (EF Core)
// │   └── Dtos/
// ├── Services/
// │   ├── IModuleService.cs
// │   ├── ModuleService.cs
// │   ├── IProductService.cs
// │   └── AutoParts/
// │       ├── IAutoPartsCatalogService.cs
// │       └── AutoPartsCatalogService.cs
// ├── Infrastructure/
// │   ├── Attributes/
// │   │   └── RequireModuleAttribute.cs
// │   └── Middleware/
// │       └── ModuleEnrichmentMiddleware.cs
// └── Program.cs
// ============================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// ═════════════════════════════════════════════════════════════
// 1. ENTITÉS EF CORE
// ═════════════════════════════════════════════════════════════

namespace MyErp.Models.Entities
{
    /// <summary>
    /// Module activé pour une société
    /// </summary>
    public class ErpCompanyModule
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string CompanyId { get; set; } = null!;
        public string ModuleCode { get; set; } = null!;
        public string ModuleName { get; set; } = null!;
        public bool IsActive { get; set; } = true;
        public string? ConfigJson { get; set; }
        public DateTime ActivatedAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        /// <summary>
        /// Parse la configuration JSON en objet typé
        /// </summary>
        public T? GetConfig<T>() where T : class
        {
            if (string.IsNullOrEmpty(ConfigJson)) return null;
            return JsonSerializer.Deserialize<T>(ConfigJson);
        }
    }

    // ── Modèles existants (déjà dans ta BDD) ──

    public class ErpProduct
    {
        public int Id { get; set; }
        public string ErpProductId { get; set; } = null!;
        public string? Name { get; set; }
        public string? Reference { get; set; }
        public string? Ean { get; set; }
        public string? Brand { get; set; }
        public decimal? Weight { get; set; }
        public decimal? Height { get; set; }
        public decimal? Width { get; set; }
        public decimal? Depth { get; set; }
        public decimal? PriceHT { get; set; }
        public decimal? StockQuantity { get; set; }
        public string? DataSource { get; set; }
        public int? BrandId { get; set; }
        public int? CategoryId { get; set; }

        // Navigation
        public ICollection<ErpProductImage>? Images { get; set; }
        public ICollection<ErpProductVehicle>? Vehicles { get; set; }
        public ICollection<ErpOemCrossReference>? OemCrossReferences { get; set; }
    }

    public class ErpProductImage
    {
        public Guid Id { get; set; }
        public int ProductId { get; set; }
        public string Url { get; set; } = null!;
        public bool IsMain { get; set; }
        public int SortOrder { get; set; }
        public ErpProduct Product { get; set; } = null!;
    }

    public class ErpProductVehicle
    {
        public Guid Id { get; set; }
        public int ProductId { get; set; }
        public string Make { get; set; } = null!;
        public string Model { get; set; } = null!;
        public int? YearFrom { get; set; }
        public int? YearTo { get; set; }
        public string? EngineCode { get; set; }
        public string? KType { get; set; }
        public ErpProduct Product { get; set; } = null!;
    }

    public class ErpOemCrossReference
    {
        public Guid Id { get; set; }
        public int ProductId { get; set; }
        public string OemNumber { get; set; } = null!;
        public string? Brand { get; set; }
        public bool IsOriginal { get; set; }
        public ErpProduct Product { get; set; } = null!;
    }

    public class ErpSyncJob
    {
        public Guid Id { get; set; }
        public int? SupplierId { get; set; }
        public string JobType { get; set; } = null!;
        public string Status { get; set; } = "running";
        public DateTime StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public int ProductsCreated { get; set; }
        public int ProductsUpdated { get; set; }
        public int ImagesAdded { get; set; }
        public int VehiclesAdded { get; set; }
        public int ErrorsCount { get; set; }
        public string? ErrorDetails { get; set; }
    }
}


// ═════════════════════════════════════════════════════════════
// 2. DBCONTEXT
// ═════════════════════════════════════════════════════════════

namespace MyErp.Data
{
    using MyErp.Models.Entities;

    public class ErpDbContext : DbContext
    {
        public ErpDbContext(DbContextOptions<ErpDbContext> options) : base(options) { }

        public DbSet<ErpCompanyModule> CompanyModules { get; set; } = null!;
        public DbSet<ErpProduct> Products { get; set; } = null!;
        public DbSet<ErpProductImage> ProductImages { get; set; } = null!;
        public DbSet<ErpProductVehicle> ProductVehicles { get; set; } = null!;
        public DbSet<ErpOemCrossReference> OemCrossReferences { get; set; } = null!;
        public DbSet<ErpSyncJob> SyncJobs { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ErpCompanyModule>(entity =>
            {
                entity.ToTable("erpcompanymodules");
                entity.HasIndex(e => new { e.CompanyId, e.ModuleCode }).IsUnique();
                entity.HasIndex(e => e.CompanyId);
                entity.HasIndex(e => e.ModuleCode);
            });

            modelBuilder.Entity<ErpProduct>(entity =>
            {
                entity.ToTable("erpproducts");
                entity.HasIndex(e => e.ErpProductId).IsUnique();
                entity.HasIndex(e => e.Ean);
                entity.HasIndex(e => e.Reference);
                entity.HasIndex(e => e.DataSource);
            });

            modelBuilder.Entity<ErpProductImage>(entity =>
            {
                entity.ToTable("erpproductimages");
                entity.HasIndex(e => new { e.ProductId, e.IsMain });
            });

            modelBuilder.Entity<ErpProductVehicle>(entity =>
            {
                entity.ToTable("erpproductvehicles");
                entity.HasIndex(e => new { e.Make, e.Model });
                entity.HasIndex(e => e.KType);
            });

            modelBuilder.Entity<ErpOemCrossReference>(entity =>
            {
                entity.ToTable("erpoemcrossreferences");
                entity.HasIndex(e => e.OemNumber);
            });

            modelBuilder.Entity<ErpSyncJob>(entity =>
            {
                entity.ToTable("erpsyncjobs");
                entity.HasIndex(e => e.Status);
            });
        }
    }
}


// ═════════════════════════════════════════════════════════════
// 3. SERVICES — MODULES
// ═════════════════════════════════════════════════════════════

namespace MyErp.Services
{
    using MyErp.Data;
    using MyErp.Models.Entities;
    using Microsoft.EntityFrameworkCore;

    public interface IModuleService
    {
        Task<bool> HasModuleAsync(string companyId, string moduleCode);
        Task<bool> HasAnyModuleAsync(string companyId, params string[] moduleCodes);
        Task<ErpCompanyModule?> GetModuleAsync(string companyId, string moduleCode);
        Task<IReadOnlyList<ErpCompanyModule>> GetActiveModulesAsync(string companyId);
        Task<T?> GetModuleConfigAsync<T>(string companyId, string moduleCode) where T : class;
    }

    public class ModuleService : IModuleService
    {
        private readonly ErpDbContext _db;
        private readonly ILogger<ModuleService> _logger;

        public ModuleService(ErpDbContext db, ILogger<ModuleService> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<bool> HasModuleAsync(string companyId, string moduleCode)
        {
            return await _db.CompanyModules.AnyAsync(m =>
                m.CompanyId == companyId &&
                m.ModuleCode == moduleCode &&
                m.IsActive &&
                (m.ExpiresAt == null || m.ExpiresAt > DateTime.UtcNow));
        }

        public async Task<bool> HasAnyModuleAsync(string companyId, params string[] moduleCodes)
        {
            return await _db.CompanyModules.AnyAsync(m =>
                m.CompanyId == companyId &&
                moduleCodes.Contains(m.ModuleCode) &&
                m.IsActive &&
                (m.ExpiresAt == null || m.ExpiresAt > DateTime.UtcNow));
        }

        public async Task<ErpCompanyModule?> GetModuleAsync(string companyId, string moduleCode)
        {
            return await _db.CompanyModules.FirstOrDefaultAsync(m =>
                m.CompanyId == companyId && m.ModuleCode == moduleCode);
        }

        public async Task<IReadOnlyList<ErpCompanyModule>> GetActiveModulesAsync(string companyId)
        {
            return await _db.CompanyModules
                .Where(m => m.CompanyId == companyId && m.IsActive)
                .OrderBy(m => m.ModuleCode)
                .ToListAsync();
        }

        public async Task<T?> GetModuleConfigAsync<T>(string companyId, string moduleCode) where T : class
        {
            var module = await GetModuleAsync(companyId, moduleCode);
            return module?.GetConfig<T>();
        }
    }
}


// ═════════════════════════════════════════════════════════════
// 4. ATTRIBUT D'AUTORISATION PAR MODULE
// ═════════════════════════════════════════════════════════════

namespace MyErp.Infrastructure.Attributes
{
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.Filters;
    using MyErp.Services;

    /// <summary>
    /// [RequireModule("auto_parts")]
    /// Retourne 403 si la société n'a pas le module activé
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class RequireModuleAttribute : Attribute, IAsyncAuthorizationFilter
    {
        private readonly string _moduleCode;

        public RequireModuleAttribute(string moduleCode)
        {
            _moduleCode = moduleCode;
        }

        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            // Récupère le CompanyId depuis le JWT / header / claim
            var companyId = context.HttpContext.User.FindFirst("company_id")?.Value
                ?? context.HttpContext.Request.Headers["X-Company-Id"].FirstOrDefault();

            if (string.IsNullOrEmpty(companyId))
            {
                context.Result = new UnauthorizedObjectResult(new { error = "CompanyId manquant" });
                return;
            }

            var moduleService = context.HttpContext.RequestServices.GetRequiredService<IModuleService>();
            var hasModule = await moduleService.HasModuleAsync(companyId, _moduleCode);

            if (!hasModule)
            {
                context.Result = new ObjectResult(new
                {
                    error = $"Module '{_moduleCode}' non activé pour cette société",
                    requiredModule = _moduleCode
                })
                { StatusCode = StatusCodes.Status403Forbidden };
            }
        }
    }
}


// ═════════════════════════════════════════════════════════════
// 5. MIDDLEWARE — ENRICHISSEMENT DU CONTEXT
// ═════════════════════════════════════════════════════════════

namespace MyErp.Infrastructure.Middleware
{
    using MyErp.Services;

    /// <summary>
    /// Ajoute les modules actifs dans HttpContext.Items pour éviter
    /// les requêtes DB répétées dans un même appel API
    /// </summary>
    public class ModuleEnrichmentMiddleware
    {
        private readonly RequestDelegate _next;

        public ModuleEnrichmentMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, IModuleService moduleService)
        {
            var companyId = context.User.FindFirst("company_id")?.Value
                ?? context.Request.Headers["X-Company-Id"].FirstOrDefault();

            if (!string.IsNullOrEmpty(companyId))
            {
                var modules = await moduleService.GetActiveModulesAsync(companyId);
                context.Items["ActiveModules"] = modules.Select(m => m.ModuleCode).ToHashSet();
            }

            await _next(context);
        }
    }
}


// ═════════════════════════════════════════════════════════════
// 6. DTOs
// ═════════════════════════════════════════════════════════════

namespace MyErp.Models.Dtos
{
    public record ProductDto(
        int Id,
        string ErpProductId,
        string? Name,
        string? Reference,
        string? Ean,
        string? Brand,
        decimal? PriceHT,
        decimal? StockQuantity,
        List<ProductImageDto>? Images
    );

    public record ProductImageDto(string Url, bool IsMain, int SortOrder);

    public record ProductDetailDto : ProductDto
    {
        public decimal? Weight { get; init; }
        public decimal? Height { get; init; }
        public decimal? Width { get; init; }
        public decimal? Depth { get; init; }
        public List<VehicleCompatibilityDto>? Vehicles { get; init; }
        public List<OemCrossRefDto>? OemNumbers { get; init; }
    }

    public record VehicleCompatibilityDto(
        string Make, string Model,
        int? YearFrom, int? YearTo,
        string? EngineCode
    );

    public record OemCrossRefDto(string OemNumber, string? Brand, bool IsOriginal);

    public record SyncRequestDto(
        string SyncType,  // "oem", "vehicle", "full"
        string? OemNumber,
        int? VehicleId,
        int? MaxPages
    );

    public record SyncResultDto(
        Guid JobId,
        string Status,
        int ProductsCreated,
        int ProductsUpdated,
        int ImagesAdded,
        int VehiclesAdded,
        int ErrorsCount
    );
}


// ═════════════════════════════════════════════════════════════
// 7. SERVICES — PIÈCES AUTO
// ═════════════════════════════════════════════════════════════

namespace MyErp.Services.AutoParts
{
    using MyErp.Data;
    using MyErp.Models.Dtos;
    using MyErp.Models.Entities;
    using Microsoft.EntityFrameworkCore;

    public interface IAutoPartsCatalogService
    {
        Task<ProductDetailDto?> GetByOemAsync(string oemNumber);
        Task<ProductDetailDto?> GetByReferenceAsync(string reference);
        Task<IReadOnlyList<ProductDto>> SearchByVehicleAsync(string make, string model, int? year);
        Task<SyncResultDto> SyncFromCatalogAsync(string companyId, SyncRequestDto request);
    }

    public class AutoPartsCatalogService : IAutoPartsCatalogService
    {
        private readonly ErpDbContext _db;
        private readonly IModuleService _moduleService;
        private readonly ILogger<AutoPartsCatalogService> _logger;

        public AutoPartsCatalogService(
            ErpDbContext db,
            IModuleService moduleService,
            ILogger<AutoPartsCatalogService> logger)
        {
            _db = db;
            _moduleService = moduleService;
            _logger = logger;
        }

        public async Task<ProductDetailDto?> GetByOemAsync(string oemNumber)
        {
            // Recherche par cross-référence OEM
            var crossRef = await _db.OemCrossReferences
                .Include(o => o.Product)
                .ThenInclude(p => p!.Images)
                .Include(o => o.Product)
                .ThenInclude(p => p!.Vehicles)
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.OemNumber == oemNumber);

            if (crossRef?.Product == null) return null;

            return MapToDetail(crossRef.Product);
        }

        public async Task<ProductDetailDto?> GetByReferenceAsync(string reference)
        {
            var product = await _db.Products
                .Include(p => p.Images)
                .Include(p => p.Vehicles)
                .Include(p => p.OemCrossReferences)
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Reference == reference);

            return product == null ? null : MapToDetail(product);
        }

        public async Task<IReadOnlyList<ProductDto>> SearchByVehicleAsync(string make, string model, int? year)
        {
            var query = _db.ProductVehicles
                .Include(v => v.Product)
                .ThenInclude(p => p!.Images)
                .AsNoTracking()
                .Where(v => v.Make == make && v.Model == model);

            if (year.HasValue)
            {
                query = query.Where(v => v.YearFrom <= year && v.YearTo >= year);
            }

            var vehicles = await query.ToListAsync();

            return vehicles.Select(v => MapToDto(v.Product!)).ToList();
        }

        public async Task<SyncResultDto> SyncFromCatalogAsync(string companyId, SyncRequestDto request)
        {
            // Vérifie que le module est actif et récupère la config
            var config = await _moduleService.GetModuleConfigAsync<AutoPartsModuleConfig>(companyId, "auto_parts");
            if (config == null)
                throw new InvalidOperationException("Module pièces auto non configuré");

            var job = new ErpSyncJob
            {
                JobType = request.SyncType,
                StartedAt = DateTime.UtcNow
            };

            _db.SyncJobs.Add(job);
            await _db.SaveChangesAsync();

            try
            {
                // TODO: Appeler l'API catalogue (RapidAPI / TecDoc)
                // selon config.ApiSource et injecter dans la BDD

                // Simulation pour l'exemple :
                job.ProductsCreated = 150;
                job.ProductsUpdated = 45;
                job.ImagesAdded = 380;
                job.VehiclesAdded = 1200;
                job.Status = "completed";
                job.CompletedAt = DateTime.UtcNow;

                await _db.SaveChangesAsync();

                return new SyncResultDto(
                    job.Id, job.Status,
                    job.ProductsCreated, job.ProductsUpdated,
                    job.ImagesAdded, job.VehiclesAdded, job.ErrorsCount);
            }
            catch (Exception ex)
            {
                job.Status = "failed";
                job.ErrorDetails = ex.Message;
                job.CompletedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();
                throw;
            }
        }

        // ── Mapping ──
        private static ProductDto MapToDto(ErpProduct p) => new(
            p.Id, p.ErpProductId, p.Name, p.Reference, p.Ean, p.Brand,
            p.PriceHT, p.StockQuantity,
            p.Images?.Select(i => new ProductImageDto(i.Url, i.IsMain, i.SortOrder)).ToList()
        );

        private static ProductDetailDto MapToDetail(ErpProduct p) => new()
        {
            Id = p.Id,
            ErpProductId = p.ErpProductId,
            Name = p.Name,
            Reference = p.Reference,
            Ean = p.Ean,
            Brand = p.Brand,
            PriceHT = p.PriceHT,
            StockQuantity = p.StockQuantity,
            Weight = p.Weight,
            Height = p.Height,
            Width = p.Width,
            Depth = p.Depth,
            Images = p.Images?.Select(i => new ProductImageDto(i.Url, i.IsMain, i.SortOrder)).ToList(),
            Vehicles = p.Vehicles?.Select(v => new VehicleCompatibilityDto(
                v.Make, v.Model, v.YearFrom, v.YearTo, v.EngineCode)).ToList(),
            OemNumbers = p.OemCrossReferences?.Select(o => new OemCrossRefDto(
                o.OemNumber, o.Brand, o.IsOriginal)).ToList()
        };
    }

    /// <summary>
    /// Configuration du module pièces auto (stockée en JSON dans erpcompanymodules.ConfigJson)
    /// </summary>
    public class AutoPartsModuleConfig
    {
        public string ApiSource { get; set; } = "rapidapi";  // rapidapi, tecdoc, epicor
        public string? ApiKey { get; set; }
        public string SyncFrequency { get; set; } = "daily";  // hourly, daily, weekly
        public decimal DefaultVat { get; set; } = 20.0m;
        public string DefaultLanguage { get; set; } = "fr";
        public bool IncludeOemCrossRefs { get; set; } = true;
        public bool IncludeVehicleCompatibility { get; set; } = true;
    }
}


// ═════════════════════════════════════════════════════════════
// 8. CONTROLLERS
// ═════════════════════════════════════════════════════════════

namespace MyErp.Controllers
{
    using Microsoft.AspNetCore.Mvc;
    using MyErp.Infrastructure.Attributes;
    using MyErp.Models.Dtos;
    using MyErp.Services;
    using MyErp.Services.AutoParts;

    [ApiController]
    [Route("api/[controller]")]
    public class ProductsController : ControllerBase
    {
        private readonly ErpDbContext _db;
        private readonly IModuleService _moduleService;

        public ProductsController(ErpDbContext db, IModuleService moduleService)
        {
            _db = db;
            _moduleService = moduleService;
        }

        // ── Endpoints GÉNÉRIQUES (disponibles pour TOUTES les sociétés) ──

        [HttpGet]
        public async Task<IActionResult> GetAll([FromHeader(Name = "X-Company-Id")] string companyId)
        {
            // Core : liste des produits, fonctionne pour tout le monde
            var products = await _db.Products
                .AsNoTracking()
                .Take(100)
                .ToListAsync();

            return Ok(products);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var product = await _db.Products
                .Include(p => p.Images)
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product == null) return NotFound();

            // Si le module pièces auto est actif, on enrichit avec les données véhicule
            var companyId = Request.Headers["X-Company-Id"].FirstOrDefault();
            if (!string.IsNullOrEmpty(companyId))
            {
                var hasAutoParts = await _moduleService.HasModuleAsync(companyId, "auto_parts");
                if (hasAutoParts)
                {
                    // Charge aussi les véhicules et OEM
                    product = await _db.Products
                        .Include(p => p.Images)
                        .Include(p => p.Vehicles)
                        .Include(p => p.OemCrossReferences)
                        .AsNoTracking()
                        .FirstOrDefaultAsync(p => p.Id == id);
                }
            }

            return Ok(product);
        }
    }

    /// <summary>
    /// Controller SPÉCIFIQUE pièces auto.
    /// Tous les endpoints retournent 403 si la société n'a pas le module.
    /// </summary>
    [ApiController]
    [Route("api/autoparts")]
    [RequireModule("auto_parts")]  // ← Tout le controller est protégé
    public class AutoPartsController : ControllerBase
    {
        private readonly IAutoPartsCatalogService _catalogService;
        private readonly ILogger<AutoPartsController> _logger;

        public AutoPartsController(IAutoPartsCatalogService catalogService, ILogger<AutoPartsController> logger)
        {
            _catalogService = catalogService;
            _logger = logger;
        }

        /// <summary>
        /// Recherche une pièce par numéro OEM
        /// </summary>
        [HttpGet("search/oem/{oemNumber}")]
        public async Task<IActionResult> SearchByOem(string oemNumber)
        {
            var result = await _catalogService.GetByOemAsync(oemNumber);
            return result == null ? NotFound() : Ok(result);
        }

        /// <summary>
        /// Recherche une pièce par référence fabricant
        /// </summary>
        [HttpGet("search/reference/{reference}")]
        public async Task<IActionResult> SearchByReference(string reference)
        {
            var result = await _catalogService.GetByReferenceAsync(reference);
            return result == null ? NotFound() : Ok(result);
        }

        /// <summary>
        /// Recherche les pièces compatibles avec un véhicule
        /// </summary>
        [HttpGet("search/vehicle")]
        public async Task<IActionResult> SearchByVehicle(
            [FromQuery] string make,
            [FromQuery] string model,
            [FromQuery] int? year)
        {
            var results = await _catalogService.SearchByVehicleAsync(make, model, year);
            return Ok(results);
        }

        /// <summary>
        /// Lancer une synchronisation depuis le catalogue externe
        /// </summary>
        [HttpPost("sync")]
        public async Task<IActionResult> Sync(
            [FromHeader(Name = "X-Company-Id")] string companyId,
            [FromBody] SyncRequestDto request)
        {
            var result = await _catalogService.SyncFromCatalogAsync(companyId, request);
            return Ok(result);
        }
    }

    /// <summary>
    /// Controller pour la gestion des modules (admin)
    /// </summary>
    [ApiController]
    [Route("api/admin/modules")]
    public class ModuleAdminController : ControllerBase
    {
        private readonly ErpDbContext _db;

        public ModuleAdminController(ErpDbContext db)
        {
            _db = db;
        }

        [HttpGet("{companyId}")]
        public async Task<IActionResult> GetCompanyModules(string companyId)
        {
            var modules = await _db.CompanyModules
                .Where(m => m.CompanyId == companyId)
                .OrderBy(m => m.ModuleCode)
                .ToListAsync();

            return Ok(modules);
        }

        [HttpPost("{companyId}/{moduleCode}")]
        public async Task<IActionResult> ActivateModule(
            string companyId,
            string moduleCode,
            [FromBody] string? configJson)
        {
            var module = new ErpCompanyModule
            {
                CompanyId = companyId,
                ModuleCode = moduleCode,
                ModuleName = moduleCode switch
                {
                    "auto_parts" => "Module Pièces Auto",
                    "hardware" => "Module Quincaillerie",
                    "appliances" => "Module Électroménager",
                    _ => moduleCode
                },
                ConfigJson = configJson,
                ActivatedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };

            _db.CompanyModules.Add(module);
            await _db.SaveChangesAsync();

            return Ok(module);
        }
    }
}


// ═════════════════════════════════════════════════════════════
// 9. PROGRAM.CS — CONFIGURATION
// ═════════════════════════════════════════════════════════════

/*
// Dans Program.cs :

using MyErp.Data;
using MyErp.Infrastructure.Middleware;
using MyErp.Services;
using MyErp.Services.AutoParts;

var builder = WebApplication.CreateBuilder(args);

// ── Services ──
builder.Services.AddDbContext<ErpDbContext>(options =>
    options.UseMySql(
        builder.Configuration.GetConnectionString("Default"),
        ServerVersion.AutoDetect(builder.Configuration.GetConnectionString("Default"))
    ));

builder.Services.AddScoped<IModuleService, ModuleService>();
builder.Services.AddScoped<IAutoPartsCatalogService, AutoPartsCatalogService>();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ── Auth (JWT avec claim company_id) ──
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            // ... config JWT
        };
    });

var app = builder.Build();

// ── Middleware pipeline ──
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

// Enrichit le contexte avec les modules actifs (évite les requêtes DB répétées)
app.UseMiddleware<ModuleEnrichmentMiddleware>();

app.MapControllers();
app.Run();
*/


// ═════════════════════════════════════════════════════════════
// 10. EXEMPLE DE REQUÊTES HTTP (test avec curl / Postman)
// ═════════════════════════════════════════════════════════════

/*

// ── Recherche par OEM (nécessite module auto_parts) ──
GET https://ton-api.com/api/autoparts/search/oem/0281002937
Headers:
  Authorization: Bearer <jwt_token>
  X-Company-Id: COMP-001

// ── Recherche par véhicule ──
GET https://ton-api.com/api/autoparts/search/vehicle?make=Renault&model=Clio&year=2018
Headers:
  Authorization: Bearer <jwt_token>
  X-Company-Id: COMP-001

// ── Lancer une synchro catalogue ──
POST https://ton-api.com/api/autoparts/sync
Headers:
  Authorization: Bearer <jwt_token>
  X-Company-Id: COMP-001
  Content-Type: application/json
Body:
{
  "syncType": "oem",
  "oemNumber": "0281002937",
  "maxPages": 5
}

// ── Liste des produits (générique, tout le monde) ──
GET https://ton-api.com/api/products
Headers:
  Authorization: Bearer <jwt_token>
  X-Company-Id: COMP-001

// ── Activer un module pour une société (admin) ──
POST https://ton-api.com/api/admin/modules/COMP-001/auto_parts
Headers:
  Authorization: Bearer <admin_jwt>
  Content-Type: application/json
Body:
{
  "api_source": "rapidapi",
  "sync_frequency": "daily",
  "default_vat": 20.0
}

*/
