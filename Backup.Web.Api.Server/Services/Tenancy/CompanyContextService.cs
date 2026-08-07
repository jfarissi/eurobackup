using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Brokers.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Backup.Web.Api.Server.Services.Tenancy
{
    public class CompanyContextService : ICompanyContextService
    {
        private readonly IHttpContextAccessor httpContextAccessor;
        private readonly IStorageBroker storage;

        public CompanyContextService(IHttpContextAccessor httpContextAccessor, IStorageBroker storage)
        {
            this.httpContextAccessor = httpContextAccessor;
            this.storage = storage;
        }

        public string? GetCurrentCompanyId()
        {
            var httpContext = this.httpContextAccessor.HttpContext;
            if (httpContext == null) return null;

            var fromClaim = httpContext.User?.FindFirst("CompanyId")?.Value
                ?? httpContext.User?.FindFirst("companyId")?.Value;
            if (!string.IsNullOrWhiteSpace(fromClaim))
                return fromClaim.Trim();

            var header = httpContext.Request.Headers["X-Company-ID"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(header))
                return header.Trim();

            return null;
        }

        public Guid? GetCurrentUserId()
        {
            var httpContext = this.httpContextAccessor.HttpContext;
            if (httpContext?.User?.Identity?.IsAuthenticated != true) return null;

            var idValue = httpContext.User.FindFirst("id")?.Value
                ?? httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? httpContext.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

            return Guid.TryParse(idValue, out var userId) ? userId : null;
        }

        public async Task<bool> CurrentCompanyHasErpCatalogSyncAsync()
        {
            var companyId = GetCurrentCompanyId();
            if (string.IsNullOrWhiteSpace(companyId)) return false;

            var enabled = await this.storage.SelectAllCompanies()
                .AsNoTracking()
                .Where(c => c.Id == companyId)
                .Select(c => c.EnableErpCatalogSync)
                .FirstOrDefaultAsync();
            if (enabled) return true;

            return await this.storage.SelectAllCompanyModules()
                .AsNoTracking()
                .AnyAsync(m =>
                    m.CompanyId == companyId &&
                    m.ModuleCode == "erp_catalog_sync" &&
                    m.IsActive &&
                    (m.ExpiresAt == null || m.ExpiresAt > DateTime.UtcNow));
        }
    }
}
