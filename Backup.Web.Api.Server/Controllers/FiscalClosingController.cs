using System.Threading.Tasks;
using Backup.Web.Api.Server.Authorization;
using Authorize = Microsoft.AspNetCore.Authorization.AuthorizeAttribute;
using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Models.Security;
using Backup.Web.Api.Server.Services.Accounting;
using Backup.Web.Api.Server.Services.Numbering;
using Backup.Web.Api.Server.Services.Sales;
using Backup.Web.Api.Server.Services.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RESTFulSense.Controllers;

namespace Backup.Web.Api.Server.Controllers
{
    /// <summary>Clôture mensuelle et annuelle (préconditions, pièce OD, à-nouveaux, exercice suivant).</summary>
    [Authorize]
    [ApiController]
    [Route("api/fiscal-closing")]
    public class FiscalClosingController : RESTFulController
    {
        private readonly IStorageBroker storage;
        private readonly INumberingSequenceService numbering;
        private readonly ICompanyContextService companyContext;

        public FiscalClosingController(
            IStorageBroker storage,
            INumberingSequenceService numbering,
            ICompanyContextService companyContext)
        {
            this.storage = storage;
            this.numbering = numbering;
            this.companyContext = companyContext;
        }

        [HttpGet("years/{id:int}/preview")]
        [RequirePermission(Permissions.AccountingRead)]
        public IActionResult Preview(int id)
        {
            var preview = FiscalClosingService.PreviewYear(
                this.storage, this.companyContext.GetCurrentCompanyId(), id);
            if (preview.Checks.Exists(c => c.Code == "E000" && c.Message.Contains("introuvable")))
                return NotFound(preview.Checks[0].Message);
            return Ok(preview);
        }

        [HttpPost("periods/{id:int}/close")]
        [RequirePermission(Permissions.AccountingManageFiscalYears)]
        public async Task<IActionResult> ClosePeriod(int id)
        {
            var (period, error) = await FiscalClosingService.ClosePeriodAsync(
                this.storage,
                this.companyContext.GetCurrentCompanyId(),
                id,
                SalesDocumentAudit.ActorFrom(User));
            if (error != null) return BadRequest(error);
            return Ok(period);
        }

        [HttpPost("years/{id:int}/close")]
        [RequirePermission(Permissions.AccountingManageFiscalYears)]
        public async Task<IActionResult> CloseYear(int id)
        {
            var result = await FiscalClosingService.CloseYearAsync(
                this.storage,
                this.numbering,
                this.companyContext.GetCurrentCompanyId(),
                id,
                SalesDocumentAudit.ActorFrom(User));
            if (!result.Success) return BadRequest(result);
            return Ok(result);
        }

        [HttpPost("years/{id:int}/open-next")]
        [RequirePermission(Permissions.AccountingManageFiscalYears)]
        public async Task<IActionResult> OpenNext(int id)
        {
            var (year, error) = await FiscalClosingService.OpenNextYearAsync(
                this.storage,
                this.companyContext.GetCurrentCompanyId(),
                id,
                SalesDocumentAudit.ActorFrom(User));
            if (error != null) return BadRequest(error);
            return Ok(year);
        }
    }
}
