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
    /// <summary>Déclaration TVA mensuelle : calcul live, figeage, annulation.</summary>
    [Authorize]
    [ApiController]
    [Route("api/vat-declarations")]
    public class VatDeclarationsController : RESTFulController
    {
        private readonly IStorageBroker storage;
        private readonly ICompanyContextService companyContext;

        public VatDeclarationsController(IStorageBroker storage, ICompanyContextService companyContext)
        {
            this.storage = storage;
            this.companyContext = companyContext;
        }

        /// <summary>Calcul live, ou snapshot si la période est déjà déclarée.</summary>
        [HttpGet]
        [RequirePermission(Permissions.AccountingRead)]
        public async Task<IActionResult> Get([FromQuery] int year, [FromQuery] int month)
        {
            var error = VatDeclarationService.ValidatePeriod(year, month);
            if (error != null) return BadRequest(error);

            var dto = await VatDeclarationService.GetAsync(
                this.storage, this.companyContext.GetCurrentCompanyId(), year, month);
            return Ok(dto);
        }

        public class DeclareVatRequest
        {
            public int Year { get; set; }
            public int Month { get; set; }
        }

        /// <summary>Fige le calcul et marque la période fiscale « TVA déclarée ».</summary>
        [HttpPost("declare")]
        [RequirePermission(Permissions.AccountingValidate)]
        public async Task<IActionResult> Declare([FromBody] DeclareVatRequest request)
        {
            var error = VatDeclarationService.ValidatePeriod(request.Year, request.Month);
            if (error != null) return BadRequest(error);

            var (dto, declareError) = await VatDeclarationService.DeclareAsync(
                this.storage,
                this.companyContext.GetCurrentCompanyId(),
                request.Year,
                request.Month,
                SalesDocumentAudit.ActorFrom(User));
            if (declareError != null) return BadRequest(declareError);
            return Ok(dto);
        }

        /// <summary>Annule la déclaration (période non verrouillée).</summary>
        [HttpDelete]
        [RequirePermission(Permissions.AccountingValidate)]
        public async Task<IActionResult> Undeclare([FromQuery] int year, [FromQuery] int month)
        {
            var error = VatDeclarationService.ValidatePeriod(year, month);
            if (error != null) return BadRequest(error);

            var (ok, undeclareError) = await VatDeclarationService.UndeclareAsync(
                this.storage,
                this.companyContext.GetCurrentCompanyId(),
                year,
                month,
                SalesDocumentAudit.ActorFrom(User));
            if (!ok) return BadRequest(undeclareError);
            return Ok(new { undeclared = true });
        }

        /// <summary>Fichier XML DGI / simpl-TVA (télétransmission).</summary>
        [HttpGet("edi")]
        [RequirePermission(Permissions.AccountingRead)]
        public async Task<IActionResult> ExportEdi([FromQuery] int year, [FromQuery] int month)
        {
            var error = VatDeclarationService.ValidatePeriod(year, month);
            if (error != null) return BadRequest(error);

            var (file, ediError) = await VatDeclarationService.ExportEdiAsync(
                this.storage, this.companyContext.GetCurrentCompanyId(), year, month);
            if (ediError != null) return BadRequest(ediError);
            return File(file!.Content, "application/xml; charset=utf-8", file.FileName);
        }
    }
}
