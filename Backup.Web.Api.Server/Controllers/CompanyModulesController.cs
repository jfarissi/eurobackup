using System;
using System.Linq;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Authorization;
using Backup.Web.Api.Server.Models.Security;
using Backup.Web.Api.Server.Services.Modules;
using Backup.Web.Api.Server.Services.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RESTFulSense.Controllers;
using Authorize = Microsoft.AspNetCore.Authorization.AuthorizeAttribute;

namespace Backup.Web.Api.Server.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/company-modules")]
    public class CompanyModulesController : RESTFulController
    {
        private readonly IModuleService modules;
        private readonly ICompanyContextService companyContext;

        public CompanyModulesController(IModuleService modules, ICompanyContextService companyContext)
        {
            this.modules = modules;
            this.companyContext = companyContext;
        }

        public class CompanyModuleDto
        {
            public string Id { get; set; } = string.Empty;
            public string CompanyId { get; set; } = string.Empty;
            public string ModuleCode { get; set; } = string.Empty;
            public string ModuleName { get; set; } = string.Empty;
            public bool IsActive { get; set; }
            public string? ConfigJson { get; set; }
            public DateTime ActivatedAt { get; set; }
            public DateTime? ExpiresAt { get; set; }
        }

        public class ActivateModuleRequest
        {
            public string? ConfigJson { get; set; }
        }

        /// <summary>Modules actifs de la société courante (header / claim CompanyId).</summary>
        [HttpGet]
        [RequirePermission(Permissions.ProductRead)]
        public async Task<IActionResult> GetActive()
        {
            var companyId = this.companyContext.GetCurrentCompanyId();
            if (string.IsNullOrWhiteSpace(companyId))
                return BadRequest(new { error = "Société requise." });

            try
            {
                var list = await this.modules.GetActiveModulesAsync(companyId);
                return Ok(list.Select(Map).ToList());
            }
            catch (Exception)
            {
                // Table absente / migration pas encore appliquée → UI en mode legacy.
                return Ok(Array.Empty<CompanyModuleDto>());
            }
        }

        /// <summary>Active (ou réactive) un module pour la société courante — réservé Admin.</summary>
        [HttpPost("{moduleCode}")]
        [RequirePermission(Permissions.RoleUpdate)]
        public async Task<IActionResult> Activate(string moduleCode, [FromBody] ActivateModuleRequest? request)
        {
            var companyId = this.companyContext.GetCurrentCompanyId();
            if (string.IsNullOrWhiteSpace(companyId))
                return BadRequest(new { error = "Société requise." });

            if (string.IsNullOrWhiteSpace(moduleCode))
                return BadRequest(new { error = "ModuleCode requis." });

            // Ne pas stocker de secrets API dans ConfigJson via cet endpoint public métier.
            var created = await this.modules.EnsureModuleAsync(
                companyId,
                moduleCode.Trim(),
                request?.ConfigJson,
                activate: true);

            return Ok(Map(created));
        }

        private static CompanyModuleDto Map(Models.Entities.SaaS.CompanyModule m) => new()
        {
            Id = m.Id,
            CompanyId = m.CompanyId,
            ModuleCode = m.ModuleCode,
            ModuleName = m.ModuleName,
            IsActive = m.IsActive,
            ConfigJson = m.ConfigJson,
            ActivatedAt = m.ActivatedAt,
            ExpiresAt = m.ExpiresAt
        };
    }
}
