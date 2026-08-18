using System.IO;
using System.Text;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Authorization;
using Authorize = Microsoft.AspNetCore.Authorization.AuthorizeAttribute;
using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Models.Security;
using Backup.Web.Api.Server.Services.Accounting;
using Backup.Web.Api.Server.Services.Sales;
using Backup.Web.Api.Server.Services.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RESTFulSense.Controllers;

namespace Backup.Web.Api.Server.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/bank-reconciliations")]
    public class BankReconciliationsController : RESTFulController
    {
        private readonly IStorageBroker storage;
        private readonly ICompanyContextService companyContext;

        public BankReconciliationsController(IStorageBroker storage, ICompanyContextService companyContext)
        {
            this.storage = storage;
            this.companyContext = companyContext;
        }

        [HttpGet]
        [RequirePermission(Permissions.AccountingRead)]
        public IActionResult List() =>
            Ok(BankReconciliationService.List(this.storage, this.companyContext.GetCurrentCompanyId()));

        [HttpGet("{id:int}")]
        [RequirePermission(Permissions.AccountingRead)]
        public IActionResult Get(int id)
        {
            var dto = BankReconciliationService.Get(this.storage, this.companyContext.GetCurrentCompanyId(), id);
            return dto == null ? NotFound() : Ok(dto);
        }

        [HttpPost("import")]
        [RequirePermission(Permissions.AccountingCreate)]
        [RequestSizeLimit(5_000_000)]
        public async Task<IActionResult> Import([FromForm] IFormFile? file, [FromForm] string? accountCode = null)
        {
            if (file == null || file.Length == 0) return BadRequest("Fichier relevé manquant.");

            string content;
            using (var reader = new StreamReader(file.OpenReadStream(), Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
                content = await reader.ReadToEndAsync();

            var (dto, error) = await BankReconciliationService.ImportAsync(
                this.storage,
                this.companyContext.GetCurrentCompanyId(),
                content,
                file.FileName,
                accountCode,
                SalesDocumentAudit.ActorFrom(User));
            if (error != null) return BadRequest(error);
            return Ok(dto);
        }

        [HttpPost("{id:int}/match")]
        [RequirePermission(Permissions.AccountingValidate)]
        public async Task<IActionResult> AutoMatch(int id)
        {
            var (result, error) = await BankReconciliationService.AutoMatchAsync(
                this.storage, this.companyContext.GetCurrentCompanyId(), id);
            if (error != null) return BadRequest(error);
            return Ok(result);
        }

        public class ManualMatchRequest
        {
            public int AccountingEntryLineId { get; set; }
        }

        [HttpPost("{id:int}/lines/{lineId:int}/match")]
        [RequirePermission(Permissions.AccountingValidate)]
        public async Task<IActionResult> ManualMatch(int id, int lineId, [FromBody] ManualMatchRequest request)
        {
            var (dto, error) = await BankReconciliationService.ManualMatchAsync(
                this.storage,
                this.companyContext.GetCurrentCompanyId(),
                id,
                lineId,
                request.AccountingEntryLineId);
            if (error != null) return BadRequest(error);
            return Ok(dto);
        }

        [HttpDelete("{id:int}/lines/{lineId:int}/match")]
        [RequirePermission(Permissions.AccountingValidate)]
        public async Task<IActionResult> Unmatch(int id, int lineId)
        {
            var (dto, error) = await BankReconciliationService.UnmatchAsync(
                this.storage, this.companyContext.GetCurrentCompanyId(), id, lineId);
            if (error != null) return BadRequest(error);
            return Ok(dto);
        }

        [HttpPost("{id:int}/complete")]
        [RequirePermission(Permissions.AccountingValidate)]
        public async Task<IActionResult> Complete(int id)
        {
            var (dto, error) = await BankReconciliationService.CompleteAsync(
                this.storage,
                this.companyContext.GetCurrentCompanyId(),
                id,
                SalesDocumentAudit.ActorFrom(User));
            if (error != null) return BadRequest(error);
            return Ok(dto);
        }
    }
}
