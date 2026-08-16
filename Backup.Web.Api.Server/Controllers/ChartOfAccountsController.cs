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
    [Route("api/chart-of-accounts")]
    public class ChartOfAccountsController : RESTFulController
    {
        private readonly IStorageBroker storage;
        private readonly ICompanyContextService companyContext;

        public ChartOfAccountsController(IStorageBroker storage, ICompanyContextService companyContext)
        {
            this.storage = storage;
            this.companyContext = companyContext;
        }

        [HttpGet]
        [RequirePermission(Permissions.AccountingRead)]
        public IActionResult GetAll(
            [FromQuery] int? accountClass = null,
            [FromQuery] string? search = null)
        {
            var query = this.storage.SelectAllChartOfAccounts().ForCompany(this.companyContext.GetCurrentCompanyId());
            if (accountClass.HasValue)
                query = query.Where(a => a.AccountClass == accountClass.Value);
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLowerInvariant();
                query = query.Where(a =>
                    a.AccountNumber.ToLower().Contains(s) ||
                    a.Label.ToLower().Contains(s));
            }
            return Ok(query.OrderBy(a => a.AccountNumber).ToList());
        }

        /// <summary>Plan groupé par classe comptable (arbre classe → comptes).</summary>
        [HttpGet("tree")]
        [RequirePermission(Permissions.AccountingRead)]
        public IActionResult GetTree()
        {
            var accounts = this.storage.SelectAllChartOfAccounts()
                .ForCompany(this.companyContext.GetCurrentCompanyId())
                .OrderBy(a => a.AccountNumber)
                .ToList();

            var tree = accounts
                .GroupBy(a => a.AccountClass)
                .OrderBy(g => g.Key)
                .Select(g => new { AccountClass = g.Key, Accounts = g.ToList() })
                .ToList();
            return Ok(tree);
        }

        [HttpPost]
        [RequirePermission(Permissions.AccountingManagePlan)]
        public async Task<IActionResult> Post([FromBody] ChartOfAccount account)
        {
            if (string.IsNullOrWhiteSpace(account.AccountNumber)) return BadRequest("Numéro de compte requis.");
            if (string.IsNullOrWhiteSpace(account.Label)) return BadRequest("Intitulé requis.");
            if (account.AccountClass < 1 || account.AccountClass > 8) return BadRequest("Classe comptable invalide (1 à 8).");

            var companyId = this.companyContext.GetCurrentCompanyId();
            account.CompanyId = companyId;
            account.AccountNumber = account.AccountNumber.Trim();
            account.Label = account.Label.Trim();
            if (string.IsNullOrWhiteSpace(account.AccountType)) account.AccountType = "Actif";

            var exists = this.storage.SelectAllChartOfAccounts()
                .ForCompany(companyId)
                .Any(a => a.AccountNumber == account.AccountNumber);
            if (exists) return BadRequest($"Le compte {account.AccountNumber} existe déjà pour cette société.");

            account.Id = 0;
            account.CreatedAt = DateTime.UtcNow;
            account.UpdatedAt = DateTime.UtcNow;
            var created = await this.storage.InsertChartOfAccountAsync(account);
            return Created(created);
        }

        [HttpPut("{id:int}")]
        [RequirePermission(Permissions.AccountingManagePlan)]
        public async Task<IActionResult> Put(int id, [FromBody] ChartOfAccount account)
        {
            var existing = await this.storage.SelectChartOfAccountByIdAsync(id);
            if (existing == null || !existing.BelongsToCompany(this.companyContext.GetCurrentCompanyId())) return NotFound();

            if (string.IsNullOrWhiteSpace(account.AccountNumber)) return BadRequest("Numéro de compte requis.");
            if (string.IsNullOrWhiteSpace(account.Label)) return BadRequest("Intitulé requis.");

            var companyId = this.companyContext.GetCurrentCompanyId();
            var newNumber = account.AccountNumber.Trim();
            if (!string.Equals(existing.AccountNumber, newNumber, StringComparison.Ordinal))
            {
                var exists = this.storage.SelectAllChartOfAccounts()
                    .ForCompany(companyId)
                    .Any(a => a.AccountNumber == newNumber && a.Id != id);
                if (exists) return BadRequest($"Le compte {newNumber} existe déjà pour cette société.");
            }

            existing.AccountNumber = newNumber;
            existing.Label = account.Label.Trim();
            existing.LabelArabic = string.IsNullOrWhiteSpace(account.LabelArabic) ? null : account.LabelArabic.Trim();
            existing.AccountClass = account.AccountClass;
            existing.AccountType = string.IsNullOrWhiteSpace(account.AccountType) ? existing.AccountType : account.AccountType.Trim();
            existing.IsLettrable = account.IsLettrable;
            existing.IsBilan = account.IsBilan;
            existing.IsResultat = account.IsResultat;
            existing.ParentId = account.ParentId;
            existing.UpdatedAt = DateTime.UtcNow;

            var updated = await this.storage.UpdateChartOfAccountAsync(existing);
            return Ok(updated);
        }

        /// <summary>Suppression refusée si le compte est utilisé dans une ligne d'écriture.</summary>
        [HttpDelete("{id:int}")]
        [RequirePermission(Permissions.AccountingManagePlan)]
        public async Task<IActionResult> Delete(int id)
        {
            var existing = await this.storage.SelectChartOfAccountByIdAsync(id);
            if (existing == null || !existing.BelongsToCompany(this.companyContext.GetCurrentCompanyId())) return NotFound();

            var companyId = this.companyContext.GetCurrentCompanyId();
            var isUsed = this.storage.SelectAllAccountingEntryLines()
                .Any(l => l.ChartOfAccountId == id || l.AccountCode == existing.AccountNumber);
            if (isUsed) return BadRequest($"Le compte {existing.AccountNumber} est utilisé dans des écritures comptables : suppression impossible.");

            var hasChildren = this.storage.SelectAllChartOfAccounts()
                .ForCompany(companyId)
                .Any(a => a.ParentId == id);
            if (hasChildren) return BadRequest($"Le compte {existing.AccountNumber} a des comptes rattachés : suppression impossible.");

            await this.storage.DeleteChartOfAccountAsync(existing);
            return NoContent();
        }
    }
}
