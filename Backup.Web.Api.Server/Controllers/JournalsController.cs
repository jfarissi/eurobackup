using System;
using System.Linq;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Authorization;
using Authorize = Microsoft.AspNetCore.Authorization.AuthorizeAttribute;
using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Models.Entities.Accounting;
using Backup.Web.Api.Server.Models.Security;
using Backup.Web.Api.Server.Services.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RESTFulSense.Controllers;

namespace Backup.Web.Api.Server.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/journals")]
    public class JournalsController : RESTFulController
    {
        private readonly IStorageBroker storage;
        private readonly ICompanyContextService companyContext;

        public JournalsController(IStorageBroker storage, ICompanyContextService companyContext)
        {
            this.storage = storage;
            this.companyContext = companyContext;
        }

        [HttpGet]
        [RequirePermission(Permissions.AccountingRead)]
        public IActionResult GetAll()
        {
            var journals = this.storage.SelectAllJournals()
                .ForCompany(this.companyContext.GetCurrentCompanyId())
                .OrderBy(j => j.Code)
                .ToList();
            return Ok(journals);
        }

        [HttpPost]
        [RequirePermission(Permissions.AccountingManagePlan)]
        public async Task<IActionResult> Post([FromBody] Journal journal)
        {
            if (string.IsNullOrWhiteSpace(journal.Code)) return BadRequest("Code journal requis.");
            if (string.IsNullOrWhiteSpace(journal.Label)) return BadRequest("Intitulé requis.");

            var companyId = this.companyContext.GetCurrentCompanyId();
            journal.CompanyId = companyId;
            journal.Code = journal.Code.Trim().ToUpperInvariant();
            journal.Label = journal.Label.Trim();

            var exists = this.storage.SelectAllJournals()
                .ForCompany(companyId)
                .Any(j => j.Code == journal.Code);
            if (exists) return BadRequest($"Le journal {journal.Code} existe déjà pour cette société.");

            journal.Id = 0;
            journal.CreatedAt = DateTime.UtcNow;
            journal.UpdatedAt = DateTime.UtcNow;
            var created = await this.storage.InsertJournalAsync(journal);
            return Created(created);
        }
    }
}
