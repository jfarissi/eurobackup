using System;
using System.Linq;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Models.Entities.SaaS;
using Backup.Web.Api.Server.Services.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RESTFulSense.Controllers;

namespace Backup.Web.Api.Server.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class CompaniesController : RESTFulController
    {
        private readonly IStorageBroker storage;
        private readonly ICompanyContextService companyContext;

        public CompaniesController(IStorageBroker storage, ICompanyContextService companyContext)
        {
            this.storage = storage;
            this.companyContext = companyContext;
        }

        public class CompanyDto
        {
            public string Id { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public string? TenantId { get; set; }
            public bool IsActive { get; set; }
            public string DefaultLanguageCode { get; set; } = "fr-FR";
            public string DefaultCurrencyCode { get; set; } = "EUR";
        }

        public class CreateCompanyRequest
        {
            public string Name { get; set; } = string.Empty;
            public string? TenantId { get; set; }
        }

        [HttpGet("available")]
        public async Task<IActionResult> GetAvailable()
        {
            var userId = this.companyContext.GetCurrentUserId();
            if (!userId.HasValue) return Unauthorized();

            var companies = await this.storage.SelectUserCompaniesByUserId(userId.Value)
                .Select(uc => uc.Company!)
                .Where(c => c.IsActive)
                .OrderBy(c => c.Name)
                .Select(c => new CompanyDto
                {
                    Id = c.Id,
                    Name = c.Name,
                    TenantId = c.TenantId,
                    IsActive = c.IsActive,
                    DefaultLanguageCode = c.DefaultLanguageCode,
                    DefaultCurrencyCode = c.DefaultCurrencyCode
                })
                .ToListAsync();

            return Ok(companies);
        }

        [HttpGet]
        public IActionResult GetAll()
        {
            var companies = this.storage.SelectAllCompanies()
                .Where(c => c.IsActive)
                .OrderBy(c => c.Name)
                .Select(c => new CompanyDto
                {
                    Id = c.Id,
                    Name = c.Name,
                    TenantId = c.TenantId,
                    IsActive = c.IsActive,
                    DefaultLanguageCode = c.DefaultLanguageCode,
                    DefaultCurrencyCode = c.DefaultCurrencyCode
                })
                .ToList();
            return Ok(companies);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateCompanyRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                return BadRequest("Name required");

            var tenantId = request.TenantId ?? TenancySeedService.DefaultTenantId;
            var company = new Company
            {
                Id = Guid.NewGuid().ToString(),
                TenantId = tenantId,
                Name = request.Name.Trim(),
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            var created = await this.storage.InsertCompanyAsync(company);
            return Ok(new CompanyDto
            {
                Id = created.Id,
                Name = created.Name,
                TenantId = created.TenantId,
                IsActive = created.IsActive
            });
        }
    }
}
