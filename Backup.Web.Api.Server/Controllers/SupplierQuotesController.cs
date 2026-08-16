using System;
using System.Threading;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Authorization;
using Authorize = Microsoft.AspNetCore.Authorization.AuthorizeAttribute;
using Backup.Web.Api.Server.Models.Security;
using Backup.Web.Api.Server.Services.SupplierQuotes;
using Backup.Web.Api.Server.Services.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using RESTFulSense.Controllers;

namespace Backup.Web.Api.Server.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/supplier-quotes")]
    public class SupplierQuotesController : RESTFulController
    {
        private readonly ISupplierQuoteService quotes;
        private readonly ISupplierQuoteNotifier notifier;
        private readonly ICompanyContextService companyContext;
        private readonly ILogger<SupplierQuotesController> logger;

        public SupplierQuotesController(
            ISupplierQuoteService quotes,
            ISupplierQuoteNotifier notifier,
            ICompanyContextService companyContext,
            ILogger<SupplierQuotesController> logger)
        {
            this.quotes = quotes;
            this.notifier = notifier;
            this.companyContext = companyContext;
            this.logger = logger;
        }

        [HttpGet("{productId:int}")]
        [RequirePermission(Permissions.ProductRead)]
        public async Task<IActionResult> Get(int productId, CancellationToken ct)
        {
            try
            {
                var companyId = this.companyContext.GetCurrentCompanyId()
                    ?? throw new InvalidOperationException("Société courante introuvable.");
                var result = await this.quotes.GetQuotesAsync(productId, companyId, forceRefresh: false, ct);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "GET supplier-quotes {ProductId} failed", productId);
                return StatusCode(500, new { error = "Échec de la cotation fournisseurs." });
            }
        }

        [HttpPost("{productId:int}/refresh")]
        [RequirePermission(Permissions.ProductRead)]
        public async Task<IActionResult> Refresh(int productId, CancellationToken ct)
        {
            try
            {
                var companyId = this.companyContext.GetCurrentCompanyId()
                    ?? throw new InvalidOperationException("Société courante introuvable.");
                var result = await this.quotes.GetQuotesAsync(productId, companyId, forceRefresh: true, ct);
                await this.notifier.NotifyQuotesUpdatedAsync(companyId, productId, result, ct);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "POST supplier-quotes refresh {ProductId} failed", productId);
                return StatusCode(500, new { error = "Échec du rafraîchissement des offres." });
            }
        }
    }
}
