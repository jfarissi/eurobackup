using System;
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
        private readonly ICompanyContextService companyContext;
        private readonly ILogger<PlateScanController> logger;

        public PlateScanController(
            IPlateScanService plateService,
            ICompanyContextService companyContext,
            ILogger<PlateScanController> logger)
        {
            this.plateService = plateService;
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

        [HttpGet("history")]
        [RequirePermission(Permissions.ProductRead)]
        public async Task<IActionResult> History([FromQuery] int limit = 20, CancellationToken ct = default)
        {
            var companyId = companyContext.GetCurrentCompanyId();
            if (string.IsNullOrWhiteSpace(companyId)) return BadRequest("Société courante introuvable.");
            var rows = await plateService.GetHistoryAsync(companyId, limit, ct);
            return Ok(rows);
        }
    }
}
