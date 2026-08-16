using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Authorization;
using Authorize = Microsoft.AspNetCore.Authorization.AuthorizeAttribute;
using Backup.Web.Api.Server.Models.Security;
using Backup.Web.Api.Server.Services.AutoParts;
using Backup.Web.Api.Server.Services.Modules;
using Backup.Web.Api.Server.Services.Sales;
using Backup.Web.Api.Server.Services.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using RESTFulSense.Controllers;

namespace Backup.Web.Api.Server.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/autoparts/plate")]
    [RequireModule(ModuleCodes.AutoParts)]
    public class PlateScanController : RESTFulController
    {
        private readonly IPlateScanService plateService;
        private readonly IKTypeSyncProgressStore syncProgress;
        private readonly IRapidApiKTypeSyncService kTypeSync;
        private readonly IKTypeEnrichmentService kTypeEnrichment;
        private readonly ICompanyContextService companyContext;
        private readonly ILogger<PlateScanController> logger;

        public PlateScanController(
            IPlateScanService plateService,
            IKTypeSyncProgressStore syncProgress,
            IRapidApiKTypeSyncService kTypeSync,
            IKTypeEnrichmentService kTypeEnrichment,
            ICompanyContextService companyContext,
            ILogger<PlateScanController> logger)
        {
            this.plateService = plateService;
            this.syncProgress = syncProgress;
            this.kTypeSync = kTypeSync;
            this.kTypeEnrichment = kTypeEnrichment;
            this.companyContext = companyContext;
            this.logger = logger;
        }

        /// <summary>OCR image de plaque → véhicule + pièces (défaut pays MA).</summary>
        [HttpPost("scan")]
        [RequirePermission(Permissions.ProductRead)]
        [RequestSizeLimit(6 * 1024 * 1024)]
        public async Task<IActionResult> Scan([FromForm] IFormFile? image, CancellationToken ct)
        {
            try
            {
                if (image == null) return BadRequest("Fichier image requis (champ « image »).");
                var companyId = companyContext.GetCurrentCompanyId()
                    ?? throw new InvalidOperationException("Société courante introuvable.");
                var result = await plateService.ScanPlateAsync(
                    companyId, image, SalesDocumentAudit.ActorFrom(User), ct);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "POST /api/autoparts/plate/scan failed");
                return StatusCode(500, new { error = "Échec de l'analyse de plaque." });
            }
        }

        /// <summary>Recherche par numéro de plaque (pays optionnel, défaut MA).</summary>
        [HttpGet("search")]
        [RequirePermission(Permissions.ProductRead)]
        public async Task<IActionResult> Search(
            [FromQuery] string plate,
            [FromQuery] string? country = null,
            CancellationToken ct = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(plate)) return BadRequest("Paramètre plate requis.");
                var companyId = companyContext.GetCurrentCompanyId()
                    ?? throw new InvalidOperationException("Société courante introuvable.");
                var result = await plateService.SearchByPlateAsync(
                    companyId, plate, country, SalesDocumentAudit.ActorFrom(User), ct);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "GET /api/autoparts/plate/search failed");
                return StatusCode(500, new { error = "Échec de la recherche par plaque." });
            }
        }

        [HttpGet("vin/{vin}")]
        [RequirePermission(Permissions.ProductRead)]
        public async Task<IActionResult> ByVin(string vin, CancellationToken ct)
        {
            try
            {
                var companyId = companyContext.GetCurrentCompanyId()
                    ?? throw new InvalidOperationException("Société courante introuvable.");
                var result = await plateService.SearchByVinAsync(
                    companyId, vin, SalesDocumentAudit.ActorFrom(User), ct);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "GET /api/autoparts/plate/vin failed");
                return StatusCode(500, new { error = "Échec du décodage VIN." });
            }
        }

        /// <summary>Scénario B : associer VIN → plaque (registre local permanent).</summary>
        [HttpPost("link")]
        [RequirePermission(Permissions.ProductRead)]
        public async Task<IActionResult> Link([FromBody] LinkPlateVinRequest? body, CancellationToken ct)
        {
            try
            {
                if (body == null) return BadRequest("Corps JSON requis.");
                var companyId = companyContext.GetCurrentCompanyId()
                    ?? throw new InvalidOperationException("Société courante introuvable.");
                var result = await plateService.LinkPlateToVinAsync(
                    companyId, body, SalesDocumentAudit.ActorFrom(User), ct);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "POST /api/autoparts/plate/link failed");
                return StatusCode(500, new { error = "Échec de l'association plaque / VIN." });
            }
        }

        [HttpGet("history")]
        [RequirePermission(Permissions.ProductRead)]
        public async Task<IActionResult> History([FromQuery] int limit = 20, CancellationToken ct = default)
        {
            var companyId = companyContext.GetCurrentCompanyId();
            if (string.IsNullOrWhiteSpace(companyId)) return BadRequest("Société courante introuvable.");
            var rows = await plateService.GetHistoryAsync(companyId, limit, ct);
            return Ok(rows);
        }

        /// <summary>Progression import catalogue RapidAPI pour un K-Type (sync à la demande).</summary>
        [HttpGet("ktype-sync/progress/{kType}")]
        [RequirePermission(Permissions.ProductRead)]
        public IActionResult KTypeSyncProgress(string kType)
        {
            if (string.IsNullOrWhiteSpace(kType)) return BadRequest("K-Type requis.");
            var progress = syncProgress.Get(kType.Trim());
            if (progress == null)
            {
                return Ok(new KTypeSyncProgressDto(
                    kType.Trim(),
                    KTypeSyncStatus.Idle,
                    null,
                    0,
                    0,
                    0,
                    null,
                    null,
                    DateTime.UtcNow));
            }

            return Ok(progress);
        }

        /// <summary>Catégories RapidAPI pour un K-Type (sans import).</summary>
        [HttpGet("ktype-sync/categories/{kType}")]
        [RequirePermission(Permissions.ProductRead)]
        public async Task<IActionResult> KTypeCategories(string kType, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(kType)) return BadRequest("K-Type requis.");
            try
            {
                var list = await kTypeSync.ListCategoriesAsync(kType.Trim(), ct);
                return Ok(list);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "GET /api/autoparts/plate/ktype-sync/categories failed");
                return StatusCode(500, new { error = "Impossible de lister les catégories RapidAPI." });
            }
        }

        /// <summary>Import RapidAPI limité aux catégories cochées.</summary>
        [HttpPost("ktype-sync/import")]
        [RequirePermission(Permissions.ProductRead)]
        public async Task<IActionResult> ImportKTypeCategories(
            [FromBody] KTypeCategoryImportRequest? body, CancellationToken ct)
        {
            if (body == null || string.IsNullOrWhiteSpace(body.KType))
                return BadRequest("K-Type requis.");
            if (body.CategoryIds == null || body.CategoryIds.Count == 0)
                return BadRequest("Sélectionnez au moins une catégorie.");

            try
            {
                var companyId = companyContext.GetCurrentCompanyId()
                    ?? throw new InvalidOperationException("Société courante introuvable.");
                var result = await kTypeEnrichment.StartOnDemandImportAsync(
                    body.KType.Trim(),
                    new KTypeEnrichmentContext(
                        companyId,
                        body.Vin,
                        body.Make,
                        body.Model,
                        body.Year,
                        null,
                        "VinLookup",
                        body.FuelType),
                    body.CategoryIds,
                    ct);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "POST /api/autoparts/plate/ktype-sync/import failed");
                return StatusCode(500, new { error = "Échec de l'import catalogue." });
            }
        }
    }

    public record KTypeCategoryImportRequest(
        string KType,
        string? Make,
        string? Model,
        int? Year,
        string? Vin,
        List<int> CategoryIds,
        string? FuelType = null);
}
