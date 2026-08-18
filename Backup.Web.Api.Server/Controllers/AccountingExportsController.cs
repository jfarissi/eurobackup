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
    /// <summary>Exports comptables : FEC (18 colonnes |) et CSV générique par exercice.</summary>
    [Authorize]
    [ApiController]
    [Route("api/accounting-exports")]
    public class AccountingExportsController : RESTFulController
    {
        private readonly IStorageBroker storage;
        private readonly ICompanyContextService companyContext;

        public AccountingExportsController(IStorageBroker storage, ICompanyContextService companyContext)
        {
            this.storage = storage;
            this.companyContext = companyContext;
        }

        [HttpGet("preview")]
        [RequirePermission(Permissions.AccountingRead)]
        public async Task<IActionResult> Preview([FromQuery] int yearId)
        {
            var (dto, error) = await AccountingExportsService.PreviewAsync(
                this.storage, this.companyContext.GetCurrentCompanyId(), yearId);
            if (error != null) return BadRequest(error);
            return Ok(dto);
        }

        [HttpGet("fec")]
        [RequirePermission(Permissions.AccountingRead)]
        public async Task<IActionResult> ExportFec([FromQuery] int yearId)
        {
            var (file, error) = await AccountingExportsService.ExportFecAsync(
                this.storage, this.companyContext.GetCurrentCompanyId(), yearId);
            if (error != null) return BadRequest(error);
            return File(file!.Content, file.ContentType, file.FileName);
        }

        [HttpGet("csv")]
        [RequirePermission(Permissions.AccountingRead)]
        public async Task<IActionResult> ExportCsv([FromQuery] int yearId)
        {
            var (file, error) = await AccountingExportsService.ExportCsvAsync(
                this.storage, this.companyContext.GetCurrentCompanyId(), yearId);
            if (error != null) return BadRequest(error);
            return File(file!.Content, file.ContentType, file.FileName);
        }
    }
}
