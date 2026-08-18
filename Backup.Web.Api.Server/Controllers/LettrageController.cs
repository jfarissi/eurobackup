using System.Collections.Generic;
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
    /// <summary>
    /// Phase 3 — lettrage comptable au niveau des lignes d'écritures (tout compte lettrable :
    /// clients, fournisseurs, …). Distinct du lettrage métier existant (LetteringsController,
    /// rapprochement de documents), qui est conservé.
    /// </summary>
    [Authorize]
    [ApiController]
    [Route("api/lettrage")]
    public class LettrageController : RESTFulController
    {
        private readonly IStorageBroker storage;
        private readonly INumberingSequenceService numberingService;
        private readonly ICompanyContextService companyContext;

        public LettrageController(
            IStorageBroker storage,
            INumberingSequenceService numberingService,
            ICompanyContextService companyContext)
        {
            this.storage = storage;
            this.numberingService = numberingService;
            this.companyContext = companyContext;
        }

        /// <summary>Lignes non lettrées d'un compte (écritures Posted/Validated), triées par date.</summary>
        [HttpGet("unlettered")]
        [RequirePermission(Permissions.AccountingRead)]
        public async Task<IActionResult> GetUnlettered([FromQuery] string? accountCode = null)
        {
            if (string.IsNullOrWhiteSpace(accountCode))
                return BadRequest("Le paramètre accountCode est requis.");

            var lines = await LettrageService.GetUnletteredLinesAsync(
                this.storage, this.companyContext.GetCurrentCompanyId(), accountCode.Trim());
            return Ok(lines);
        }

        /// <summary>Groupes de lettrage existants (tous comptes ou filtré par accountCode).</summary>
        [HttpGet("groups")]
        [RequirePermission(Permissions.AccountingRead)]
        public async Task<IActionResult> GetGroups([FromQuery] string? accountCode = null)
        {
            var groups = await LettrageService.GetLetteringGroupsAsync(
                this.storage, this.companyContext.GetCurrentCompanyId(), accountCode);
            return Ok(groups);
        }

        public class AutomaticLettrageRequest
        {
            /// <summary>Null / vide = comptes clients ET fournisseurs des paramètres de la société.</summary>
            public string? AccountCode { get; set; }
        }

        /// <summary>Lettrage automatique : référence exacte (facture ↔ règlements) puis montant FIFO.</summary>
        [HttpPost("automatic")]
        [RequirePermission(Permissions.AccountingValidate)]
        public async Task<IActionResult> Automatic([FromBody] AutomaticLettrageRequest? request)
        {
            var summaries = await LettrageService.AutomaticAsync(
                this.storage,
                this.numberingService,
                this.companyContext.GetCurrentCompanyId(),
                request?.AccountCode,
                SalesDocumentAudit.ActorFrom(User));
            return Ok(summaries);
        }

        public class ManualLettrageRequest
        {
            public List<int> LineIds { get; set; } = new();
        }

        /// <summary>Lettrage manuel d'une sélection de lignes (même compte, équilibrée).</summary>
        [HttpPost("manual")]
        [RequirePermission(Permissions.AccountingValidate)]
        public async Task<IActionResult> Manual([FromBody] ManualLettrageRequest request)
        {
            var (code, error) = await LettrageService.ManualAsync(
                this.storage,
                this.numberingService,
                this.companyContext.GetCurrentCompanyId(),
                request?.LineIds,
                SalesDocumentAudit.ActorFrom(User));
            if (error != null) return BadRequest(error);
            return Ok(new { code });
        }

        /// <summary>Délettrage : efface le code des lignes concernées (période du jour ouverte requise).</summary>
        [HttpDelete("{code}")]
        [RequirePermission(Permissions.AccountingValidate)]
        public async Task<IActionResult> Delete(string code)
        {
            var (count, error) = await LettrageService.DeletterAsync(
                this.storage,
                this.companyContext.GetCurrentCompanyId(),
                code,
                SalesDocumentAudit.ActorFrom(User));
            if (error != null) return BadRequest(error);
            return Ok(new { delettered = count });
        }
    }
}
