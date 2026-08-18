using System;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Authorization;
using Authorize = Microsoft.AspNetCore.Authorization.AuthorizeAttribute;
using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Models.Security;
using Backup.Web.Api.Server.Services.Accounting;
using Backup.Web.Api.Server.Services.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RESTFulSense.Controllers;

namespace Backup.Web.Api.Server.Controllers
{
    /// <summary>Rapports comptables en lecture : balance des comptes et grand livre.</summary>
    [Authorize]
    [ApiController]
    [Route("api/accounting-reports")]
    public class AccountingReportsController : RESTFulController
    {
        private readonly IStorageBroker storage;
        private readonly ICompanyContextService companyContext;

        public AccountingReportsController(IStorageBroker storage, ICompanyContextService companyContext)
        {
            this.storage = storage;
            this.companyContext = companyContext;
        }

        /// <summary>
        /// Balance des comptes. Ouverture = mouvements avant <paramref name="from"/>,
        /// période = [from, to], clôture = ouverture + débit − crédit.
        /// Écritures Posted et Validated uniquement.
        /// </summary>
        [HttpGet("balance")]
        [RequirePermission(Permissions.AccountingRead)]
        public async Task<IActionResult> GetBalance(
            [FromQuery] DateTime? from = null,
            [FromQuery] DateTime? to = null)
        {
            if (from != null && to != null && from.Value.Date > to.Value.Date)
                return BadRequest("La date de début doit être antérieure ou égale à la date de fin.");

            var report = await AccountingReportsService.GetBalanceAsync(
                this.storage, this.companyContext.GetCurrentCompanyId(), from, to);
            return Ok(report);
        }

        /// <summary>
        /// Grand livre d'un compte : solde d'ouverture, mouvements de la période, solde de clôture.
        /// Écritures Posted et Validated uniquement.
        /// </summary>
        [HttpGet("general-ledger")]
        [RequirePermission(Permissions.AccountingRead)]
        public async Task<IActionResult> GetGeneralLedger(
            [FromQuery] string? accountCode = null,
            [FromQuery] DateTime? from = null,
            [FromQuery] DateTime? to = null)
        {
            if (string.IsNullOrWhiteSpace(accountCode))
                return BadRequest("Le paramètre accountCode est requis.");
            if (from != null && to != null && from.Value.Date > to.Value.Date)
                return BadRequest("La date de début doit être antérieure ou égale à la date de fin.");

            var report = await AccountingReportsService.GetGeneralLedgerAsync(
                this.storage, this.companyContext.GetCurrentCompanyId(), accountCode, from, to);
            return Ok(report);
        }
    }
}
