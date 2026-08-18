using System;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Authorization;
using Authorize = Microsoft.AspNetCore.Authorization.AuthorizeAttribute;
using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Models.Security;
using Backup.Web.Api.Server.Services.Accounting;
using Backup.Web.Api.Server.Services.Sales;
using Backup.Web.Api.Server.Services.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RESTFulSense.Controllers;

namespace Backup.Web.Api.Server.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/cabinet")]
    public class CabinetController : RESTFulController
    {
        private readonly IStorageBroker storage;
        private readonly ICompanyContextService companyContext;

        public CabinetController(IStorageBroker storage, ICompanyContextService companyContext)
        {
            this.storage = storage;
            this.companyContext = companyContext;
        }

        [HttpGet("dossiers")]
        [RequirePermission(Permissions.AccountingRead)]
        public async Task<IActionResult> Dossiers() =>
            Ok(await CabinetService.ListDossiersAsync(this.storage, this.companyContext.GetCurrentCompanyId()));

        [HttpGet("companies")]
        [RequirePermission(Permissions.AccountingRead)]
        public IActionResult Companies() =>
            Ok(CabinetService.ListLinkableCompanies(this.storage, this.companyContext.GetCurrentCompanyId()));

        public class LinkRequest
        {
            public string ClientCompanyId { get; set; } = string.Empty;
            public string? MissionLevel { get; set; }
        }

        [HttpPost("dossiers")]
        [RequireAnyPermission(Permissions.AccountingValidate, Permissions.AccountingCabinet)]
        public async Task<IActionResult> Link([FromBody] LinkRequest request)
        {
            var (dto, error) = await CabinetService.LinkClientAsync(
                this.storage, this.companyContext.GetCurrentCompanyId(),
                request.ClientCompanyId, request.MissionLevel, SalesDocumentAudit.ActorFrom(User));
            if (error != null) return BadRequest(error);
            return Ok(dto);
        }

        [HttpGet("dossiers/{companyId}/entries")]
        [RequirePermission(Permissions.AccountingRead)]
        public IActionResult Entries(string companyId, [FromQuery] DateTime? from, [FromQuery] DateTime? to) =>
            Ok(CabinetService.ListEntries(this.storage, this.companyContext.GetCurrentCompanyId(), companyId, from, to));

        [HttpGet("dossiers/{companyId}/annotations")]
        [RequirePermission(Permissions.AccountingRead)]
        public IActionResult Annotations(string companyId, [FromQuery] int? entryId) =>
            Ok(CabinetService.ListAnnotations(this.storage, this.companyContext.GetCurrentCompanyId(), companyId, entryId));

        public class AnnotateRequest
        {
            public string? Type { get; set; }
            public string Message { get; set; } = string.Empty;
            public int? AccountingEntryId { get; set; }
        }

        [HttpPost("dossiers/{companyId}/annotations")]
        [RequireAnyPermission(Permissions.AccountingValidate, Permissions.AccountingCabinet)]
        public async Task<IActionResult> Annotate(string companyId, [FromBody] AnnotateRequest request)
        {
            var (dto, error) = await CabinetService.AddAnnotationAsync(
                this.storage, this.companyContext.GetCurrentCompanyId(), companyId,
                request.Type, request.Message, request.AccountingEntryId, SalesDocumentAudit.ActorFrom(User));
            if (error != null) return BadRequest(error);
            return Ok(dto);
        }

        [HttpPost("annotations/{id:int}/resolve")]
        [RequireAnyPermission(Permissions.AccountingValidate, Permissions.AccountingCabinet)]
        public async Task<IActionResult> Resolve(int id)
        {
            var (dto, error) = await CabinetService.ResolveAnnotationAsync(
                this.storage, this.companyContext.GetCurrentCompanyId(), id, SalesDocumentAudit.ActorFrom(User));
            if (error != null) return BadRequest(error);
            return Ok(dto);
        }

        public class CloseRequest
        {
            public int Year { get; set; }
            public int Month { get; set; }
            public bool Force { get; set; }
        }

        [HttpPost("dossiers/{companyId}/validate-close")]
        [RequireAnyPermission(Permissions.AccountingValidate, Permissions.AccountingCabinet)]
        public async Task<IActionResult> ValidateClose(string companyId, [FromBody] CloseRequest request)
        {
            var (message, error) = await CabinetService.ValidateCloseAsync(
                this.storage, this.companyContext.GetCurrentCompanyId(), companyId,
                request.Year, request.Month, request.Force, SalesDocumentAudit.ActorFrom(User));
            if (error != null) return BadRequest(error);
            return Ok(new { message });
        }
    }
}
