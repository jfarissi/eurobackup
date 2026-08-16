using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Authorization;
using MsAuthorize = Microsoft.AspNetCore.Authorization.AuthorizeAttribute;
using Backup.Web.Api.Server.Models.Security;
using Backup.Web.Api.Server.Models.Users;
using Backup.Web.Api.Server.Services.Garage;
using Backup.Web.Api.Server.Services.Modules;
using Backup.Web.Api.Server.Services.Tenancy;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using RESTFulSense.Controllers;

namespace Backup.Web.Api.Server.Controllers
{
    [MsAuthorize]
    [ApiController]
    [Route("api/garage")]
    [RequireModule(ModuleCodes.AutoParts)]
    public class GarageController : RESTFulController
    {
        private readonly IGaragePortalService portal;
        private readonly ICompanyContextService companyContext;
        private readonly UserManager<User> userManager;
        private readonly ILogger<GarageController> logger;

        public GarageController(
            IGaragePortalService portal,
            ICompanyContextService companyContext,
            UserManager<User> userManager,
            ILogger<GarageController> logger)
        {
            this.portal = portal;
            this.companyContext = companyContext;
            this.userManager = userManager;
            this.logger = logger;
        }

        [HttpGet("me")]
        [RequireAnyPermission(Permissions.GarageOrdersRead, Permissions.GarageVehiclesRead)]
        public async Task<IActionResult> Me(CancellationToken ct)
        {
            try
            {
                var (user, companyId) = await this.RequireGarageUserAsync();
                return Ok(await this.portal.GetMeAsync(user, companyId, ct));
            }
            catch (InvalidOperationException ex)
            {
                return StatusCode(403, new { error = ex.Message });
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "GET /api/garage/me failed");
                return StatusCode(500, new { error = "Échec du profil garage." });
            }
        }

        [HttpGet("orders")]
        [RequirePermission(Permissions.GarageOrdersRead)]
        public async Task<IActionResult> Orders(CancellationToken ct)
        {
            try
            {
                var (user, companyId) = await this.RequireGarageUserAsync();
                return Ok(await this.portal.GetOrdersAsync(user, companyId, ct));
            }
            catch (InvalidOperationException ex)
            {
                return StatusCode(403, new { error = ex.Message });
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "GET /api/garage/orders failed");
                return StatusCode(500, new { error = "Échec des commandes garage." });
            }
        }

        [HttpGet("orders/{id:int}")]
        [RequirePermission(Permissions.GarageOrdersRead)]
        public async Task<IActionResult> Order(int id, CancellationToken ct)
        {
            try
            {
                var (user, companyId) = await this.RequireGarageUserAsync();
                return Ok(await this.portal.GetOrderAsync(user, companyId, id, ct));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return StatusCode(403, new { error = ex.Message });
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "GET /api/garage/orders/{OrderId} failed", id);
                return StatusCode(500, new { error = "Échec du détail commande." });
            }
        }

        [HttpGet("vehicles")]
        [RequirePermission(Permissions.GarageVehiclesRead)]
        public async Task<IActionResult> Vehicles(CancellationToken ct)
        {
            try
            {
                var (user, companyId) = await this.RequireGarageUserAsync();
                return Ok(await this.portal.GetVehiclesAsync(user, companyId, ct));
            }
            catch (InvalidOperationException ex)
            {
                return StatusCode(403, new { error = ex.Message });
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "GET /api/garage/vehicles failed");
                return StatusCode(500, new { error = "Échec des véhicules garage." });
            }
        }

        private async Task<(User user, string companyId)> RequireGarageUserAsync()
        {
            var companyId = this.companyContext.GetCurrentCompanyId()
                ?? throw new InvalidOperationException("Société courante introuvable.");

            var idValue = User.FindFirstValue("id")
                ?? User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User.FindFirstValue("sub");
            if (string.IsNullOrWhiteSpace(idValue))
                throw new InvalidOperationException("Utilisateur introuvable.");

            var user = await this.userManager.FindByIdAsync(idValue)
                ?? throw new InvalidOperationException("Utilisateur introuvable.");
            return (user, companyId);
        }
    }
}
