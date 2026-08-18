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
    [Authorize]
    [ApiController]
    [Route("api/fixed-assets")]
    public class FixedAssetsController : RESTFulController
    {
        private readonly IStorageBroker storage;
        private readonly INumberingSequenceService numbering;
        private readonly ICompanyContextService companyContext;

        public FixedAssetsController(
            IStorageBroker storage,
            INumberingSequenceService numbering,
            ICompanyContextService companyContext)
        {
            this.storage = storage;
            this.numbering = numbering;
            this.companyContext = companyContext;
        }

        [HttpGet]
        [RequirePermission(Permissions.AccountingRead)]
        public IActionResult List() =>
            Ok(FixedAssetService.List(this.storage, this.companyContext.GetCurrentCompanyId()));

        [HttpGet("{id:int}")]
        [RequirePermission(Permissions.AccountingRead)]
        public IActionResult Get(int id)
        {
            var dto = FixedAssetService.Get(this.storage, this.companyContext.GetCurrentCompanyId(), id);
            return dto == null ? NotFound() : Ok(dto);
        }

        [HttpPost]
        [RequirePermission(Permissions.AccountingCreate)]
        public async Task<IActionResult> Create([FromBody] FixedAssetService.AssetForm form)
        {
            var (dto, error) = await FixedAssetService.CreateAsync(
                this.storage, this.companyContext.GetCurrentCompanyId(), form, SalesDocumentAudit.ActorFrom(User));
            if (error != null) return BadRequest(error);
            return Ok(dto);
        }

        [HttpPut("{id:int}")]
        [RequirePermission(Permissions.AccountingCreate)]
        public async Task<IActionResult> Update(int id, [FromBody] FixedAssetService.AssetForm form)
        {
            var (dto, error) = await FixedAssetService.UpdateAsync(
                this.storage, this.companyContext.GetCurrentCompanyId(), id, form, SalesDocumentAudit.ActorFrom(User));
            if (error != null) return BadRequest(error);
            return Ok(dto);
        }

        [HttpPost("{id:int}/recalculate")]
        [RequirePermission(Permissions.AccountingCreate)]
        public async Task<IActionResult> Recalculate(int id)
        {
            var (dto, error) = await FixedAssetService.RecalculateAsync(
                this.storage, this.companyContext.GetCurrentCompanyId(), id, SalesDocumentAudit.ActorFrom(User));
            if (error != null) return BadRequest(error);
            return Ok(dto);
        }

        [HttpPost("{id:int}/deactivate")]
        [RequirePermission(Permissions.AccountingValidate)]
        public async Task<IActionResult> Deactivate(int id)
        {
            var (dto, error) = await FixedAssetService.DeactivateAsync(
                this.storage, this.companyContext.GetCurrentCompanyId(), id, null, SalesDocumentAudit.ActorFrom(User));
            if (error != null) return BadRequest(error);
            return Ok(dto);
        }

        [HttpPost("post-month")]
        [RequirePermission(Permissions.AccountingValidate)]
        public async Task<IActionResult> PostMonth([FromQuery] int year, [FromQuery] int month)
        {
            var (result, error) = await FixedAssetService.PostMonthAsync(
                this.storage,
                this.numbering,
                this.companyContext.GetCurrentCompanyId(),
                year,
                month,
                SalesDocumentAudit.ActorFrom(User));
            if (error != null) return BadRequest(error);
            return Ok(result);
        }
    }
}
