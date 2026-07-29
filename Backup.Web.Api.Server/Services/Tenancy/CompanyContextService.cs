using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Backup.Web.Api.Server.Services.Tenancy
{
    public class CompanyContextService : ICompanyContextService
    {
        private readonly IHttpContextAccessor httpContextAccessor;

        public CompanyContextService(IHttpContextAccessor httpContextAccessor)
        {
            this.httpContextAccessor = httpContextAccessor;
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
    }
}
