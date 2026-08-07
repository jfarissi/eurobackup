using System;
using System.Threading;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Authorization;
using Authorize = Microsoft.AspNetCore.Authorization.AuthorizeAttribute;
using Backup.Web.Api.Server.Models.Security;
using Backup.Web.Api.Server.Services.ErpSync;
using Backup.Web.Api.Server.Services.Modules;
using Backup.Web.Api.Server.Services.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RESTFulSense.Controllers;

namespace Backup.Web.Api.Server.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/car-api")]
    [RequireModule(ModuleCodes.AutoParts)]
    public class CarApiCatalogController : RESTFulController
    {
        private readonly ICarApiCatalogService _catalog;
        private readonly ICompanyContextService _companyContext;

        public CarApiCatalogController(ICarApiCatalogService catalog, ICompanyContextService companyContext)
        {
            _catalog = catalog;
            _companyContext = companyContext;
        }

        [HttpGet("brands")]
        [RequirePermission(Permissions.ProductRead)]
        public async Task<IActionResult> GetBrands(CancellationToken ct)
        {
            var brands = await _catalog.GetBrandsAsync(ct);
            return Ok(brands);
        }

        [HttpGet("models")]
        [RequirePermission(Permissions.ProductRead)]
        public async Task<IActionResult> GetModels([FromQuery] string brand, CancellationToken ct)
        {
            var models = await _catalog.GetModelsAsync(brand, ct);
            return Ok(models);
        }

        [HttpGet("generations")]
        [RequirePermission(Permissions.ProductRead)]
        public async Task<IActionResult> GetGenerations(
            [FromQuery] string brand,
            [FromQuery] string model,
            CancellationToken ct)
        {
            var generations = await _catalog.GetGenerationsAsync(brand, model, ct);
            return Ok(generations);
        }

        /// <summary>Prépare l'attribut JSON <c>vehicle_compat</c> pour la société.</summary>
        [HttpPost("ensure-vehicle-attribute")]
        [RequirePermission(Permissions.ProductUpdate)]
        public async Task<IActionResult> EnsureVehicleAttribute(CancellationToken ct)
        {
            var companyId = _companyContext.GetCurrentCompanyId();
            if (string.IsNullOrWhiteSpace(companyId))
                return BadRequest(new { error = "Société requise." });

            var def = await _catalog.EnsureVehicleCompatAttributeAsync(companyId, User.Identity?.Name, ct);
            return Ok(def);
        }
    }
}
