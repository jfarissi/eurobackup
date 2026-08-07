using System;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Services.Modules;
using Backup.Web.Api.Server.Services.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace Backup.Web.Api.Server.Authorization
{
    /// <summary>[RequireModule("auto_parts")] → 403 si le module n'est pas actif pour la société courante.</summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
    public sealed class RequireModuleAttribute : Attribute, IAsyncAuthorizationFilter
    {
        private readonly string moduleCode;

        public RequireModuleAttribute(string moduleCode)
        {
            this.moduleCode = moduleCode;
        }

        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            var companyContext = context.HttpContext.RequestServices.GetRequiredService<ICompanyContextService>();
            var companyId = companyContext.GetCurrentCompanyId();
            if (string.IsNullOrWhiteSpace(companyId))
            {
                context.Result = new UnauthorizedObjectResult(new { error = "Société requise." });
                return;
            }

            var modules = context.HttpContext.RequestServices.GetRequiredService<IModuleService>();
            if (!await modules.HasModuleAsync(companyId, this.moduleCode))
            {
                context.Result = new ObjectResult(new
                {
                    error = $"Module « {this.moduleCode} » non activé pour cette société.",
                    requiredModule = this.moduleCode
                })
                {
                    StatusCode = StatusCodes.Status403Forbidden
                };
            }
        }
    }
}
