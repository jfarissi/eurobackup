using System;
using System.Linq;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Authorization;
using Authorize = Microsoft.AspNetCore.Authorization.AuthorizeAttribute;
using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Models.Entities;
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
    /// Facture d'acompte (AAC) — RG-AA1–4. Toujours liée à une commande.
    /// Cycle : Draft → Validated (GL 411/419) → Applied (déduite d'une facture finale) / Cancelled.
    /// </summary>
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class DepositInvoicesController : RESTFulController
    {
        private readonly IStorageBroker storage;
        private readonly INumberingSequenceService numberingService;
        private readonly ICompanyContextService companyContext;

        public DepositInvoicesController(IStorageBroker storage, INumberingSequenceService numberingService, ICompanyContextService companyContext)
        {
            this.storage = storage;
            this.numberingService = numberingService;
            this.companyContext = companyContext;
        }

        public class ApplyToInvoiceRequest
        {
            public int SalesInvoiceId { get; set; }
        }

        public class CancelRequest
        {
            public string? Reason { get; set; }
        }

        [HttpGet]
        [RequirePermission(Permissions.InvoiceRead)]
        public IActionResult GetAll([FromQuery] string? search = null, [FromQuery] int? customerId = null, [FromQuery] int? salesOrderId = null)
        {
            var query = this.storage.SelectAllDepositInvoices().ForCompany(this.companyContext.GetCurrentCompanyId());

            if (customerId.HasValue)
                query = query.Where(d => d.CustomerId == customerId.Value);
            if (salesOrderId.HasValue)
                query = query.Where(d => d.SalesOrderId == salesOrderId.Value);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.ToLowerInvariant();
                query = query.Where(d => d.DepositNumber.ToLower().Contains(s) || (d.Customer != null && d.Customer.Name.ToLower().Contains(s)));
            }

            return Ok(query.OrderByDescending(d => d.Date).ToList());
        }

        [HttpGet("{id:int}")]
        [RequirePermission(Permissions.InvoiceRead)]
        public async Task<IActionResult> GetById(int id)
        {
            var deposit = await this.storage.SelectDepositInvoiceByIdAsync(id);
            if (deposit == null || !deposit.BelongsToCompany(this.companyContext.GetCurrentCompanyId())) return NotFound();
            return Ok(deposit);
        }

        /// <summary>RG-AA1 : acompte toujours lié à une commande.</summary>
        [HttpPost]
        [RequirePermission(Permissions.InvoiceCreate)]
        public async Task<IActionResult> Post([FromBody] DepositInvoice deposit)
        {
            deposit.EnsureCompanyId(this.companyContext.GetCurrentCompanyId());

            if (deposit.SalesOrderId <= 0) return BadRequest("Une facture d'acompte doit être liée à une commande.");
            var order = await this.storage.SelectSalesOrderByIdAsync(deposit.SalesOrderId);
            if (order == null || !order.BelongsToCompany(deposit.CompanyId)) return BadRequest("Commande introuvable.");
            var orderErr = DocumentLifecycleRules.RejectIfDepositOrderUnusable(order.Status);
            if (orderErr != null) return BadRequest(orderErr);

            if (string.Equals(order.Status, "Closed", StringComparison.OrdinalIgnoreCase))
            {
                var closedErr = DocumentLifecycleRules.RejectIfClosedOrderFullySettled(this.storage, order.Id);
                if (closedErr != null) return BadRequest(closedErr);
            }

            deposit.CustomerId = deposit.CustomerId > 0 ? deposit.CustomerId : order.CustomerId;
            if (deposit.CustomerId != order.CustomerId)
                return BadRequest("Le client de l'acompte doit correspondre au client de la commande.");

            var customer = await this.storage.SelectCustomerByIdAsync(deposit.CustomerId);
            if (customer == null) return BadRequest("Client introuvable.");
            var partyErr = SalesBusinessRules.RejectIfPartyNotActive(customer.Status, customer.Name);
            if (partyErr != null) return BadRequest(partyErr);

            var amountErr = DocumentLifecycleRules.RejectIfDepositAmountInvalid(deposit.AmountHT);
            if (amountErr != null) return BadRequest(amountErr);
            if (deposit.VatRate <= 0) deposit.VatRate = 21.0m;
            deposit.AmountTTC = Math.Round(deposit.AmountHT * (1 + deposit.VatRate / 100m), 4);

            var capErr = DocumentLifecycleRules.RejectIfDepositExceedsOrder(deposit.AmountTTC, order.TotalTTC, order.OrderNumber);
            if (capErr != null) return BadRequest(capErr);

            deposit.DepositNumber = await this.numberingService.GetNextNumberAsync("DepositInvoice", deposit.CompanyId);
            deposit.Date = deposit.Date == default ? DateTime.UtcNow : deposit.Date;
            deposit.CreatedAt = DateTime.UtcNow;
            deposit.Status = "Draft";
            deposit.AppliedSalesInvoiceId = null;
            deposit.AppliedAt = null;
            // RG-CP1 : devise figée à la création depuis Company.DefaultCurrencyCode.
            deposit.CurrencyCode = await SalesBusinessRules.ResolveCompanyCurrencyAsync(this.storage, deposit.CompanyId);

            var created = await this.storage.InsertDepositInvoiceAsync(deposit);
            await AuditDepositAsync(created.Id, "Created", $"Création acompte {created.DepositNumber} ({created.AmountTTC:0.##} €)");
            return Created(created);
        }

        [HttpPut("{id:int}")]
        [RequirePermission(Permissions.InvoiceUpdate)]
        public async Task<IActionResult> Put(int id, [FromBody] DepositInvoice deposit)
        {
            var existing = await this.storage.SelectDepositInvoiceByIdAsync(id);
            if (existing == null || !existing.BelongsToCompany(this.companyContext.GetCurrentCompanyId())) return NotFound();

            if (!string.Equals(existing.Status, "Draft", StringComparison.OrdinalIgnoreCase))
                return BadRequest($"Un acompte au statut {existing.Status} ne peut plus être modifié (Draft uniquement).");

            existing.Date = deposit.Date == default ? existing.Date : deposit.Date;
            existing.Notes = deposit.Notes;
            if (deposit.AmountHT > 0) existing.AmountHT = deposit.AmountHT;
            if (deposit.VatRate > 0) existing.VatRate = deposit.VatRate;
            existing.AmountTTC = Math.Round(existing.AmountHT * (1 + existing.VatRate / 100m), 4);

            var updated = await this.storage.UpdateDepositInvoiceAsync(existing);
            await AuditDepositAsync(updated.Id, "Updated", $"Modification acompte {updated.DepositNumber}");
            return Ok(updated);
        }

        /// <summary>RG-AA2 : Draft → Validated, écriture GL 411/419.</summary>
        [HttpPost("{id:int}/validate")]
        [RequirePermission(Permissions.InvoiceUpdate)]
        public async Task<IActionResult> Validate(int id)
        {
            var deposit = await this.storage.SelectDepositInvoiceByIdAsync(id);
            if (deposit == null || !deposit.BelongsToCompany(this.companyContext.GetCurrentCompanyId())) return NotFound();

            var validateErr = DocumentLifecycleRules.RejectIfDepositCannotValidate(deposit.Status);
            if (validateErr != null) return Conflict(new { error = validateErr });

            var (_, glError) = await AccountingLedger.PostDepositInvoiceAsync(this.storage, this.numberingService, deposit, User.Identity?.Name);
            if (glError != null) return BadRequest(glError);

            deposit.Status = "Validated";
            var updated = await this.storage.UpdateDepositInvoiceAsync(deposit);
            await AuditDepositAsync(updated.Id, "Validated", $"Validation acompte {updated.DepositNumber}");
            return Ok(updated);
        }

        /// <summary>RG-AA3 : déduction de l'acompte sur la facture finale (reverse GL 419/411 + PaidAmount facture).</summary>
        [HttpPost("{id:int}/apply-to-invoice")]
        [RequirePermission(Permissions.InvoiceUpdate)]
        public async Task<IActionResult> ApplyToInvoice(int id, [FromBody] ApplyToInvoiceRequest request)
        {
            if (request == null || request.SalesInvoiceId <= 0) return BadRequest("SalesInvoiceId requis.");

            var deposit = await this.storage.SelectDepositInvoiceByIdAsync(id);
            if (deposit == null || !deposit.BelongsToCompany(this.companyContext.GetCurrentCompanyId())) return NotFound();

            var applyErr = DocumentLifecycleRules.RejectIfDepositCannotApply(deposit.Status);
            if (applyErr != null)
            {
                if (string.Equals(deposit.Status, "Applied", StringComparison.OrdinalIgnoreCase))
                    return Conflict(new { error = applyErr });
                return BadRequest(applyErr);
            }

            var invoice = await this.storage.SelectSalesInvoiceByIdAsync(request.SalesInvoiceId);
            if (invoice == null || !invoice.BelongsToCompany(this.companyContext.GetCurrentCompanyId())) return BadRequest("Facture introuvable.");
            if (invoice.CustomerId != deposit.CustomerId)
                return BadRequest("La facture doit appartenir au même client que l'acompte.");
            if (invoice.SalesOrderId.HasValue && invoice.SalesOrderId.Value != deposit.SalesOrderId)
                return BadRequest("La facture doit provenir de la même commande que l'acompte.");
            if (string.Equals(invoice.Status, "Draft", StringComparison.OrdinalIgnoreCase)
                || string.Equals(invoice.Status, "Cancelled", StringComparison.OrdinalIgnoreCase))
                return BadRequest($"La facture doit être validée avant application de l'acompte (statut actuel : {invoice.Status}).");

            var (_, glError) = await AccountingLedger.PostDepositApplicationAsync(this.storage, this.numberingService, deposit, invoice, User.Identity?.Name);
            if (glError != null) return BadRequest(glError);

            var appliedAmount = Math.Min(deposit.AmountTTC, Math.Max(0m, invoice.TotalTTC - invoice.PaidAmount));
            invoice.PaidAmount += appliedAmount;
            var credited = SalesInvoiceSettlement.GetAppliedCreditTotal(this.storage, invoice.Id);
            SalesInvoiceSettlement.RefreshPaymentStatus(invoice, credited);
            await this.storage.UpdateSalesInvoiceAsync(invoice);

            deposit.Status = "Applied";
            deposit.AppliedSalesInvoiceId = invoice.Id;
            deposit.AppliedAt = DateTime.UtcNow;
            var updated = await this.storage.UpdateDepositInvoiceAsync(deposit);
            await AuditDepositAsync(updated.Id, "Applied", $"Acompte {updated.DepositNumber} appliqué sur facture {invoice.InvoiceNumber}");
            return Ok(updated);
        }

        /// <summary>RG-AA4 : annulation — Draft (aucun effet) ou Validated (reverse GL) ; Applied non annulable directement.</summary>
        [HttpPost("{id:int}/cancel")]
        [RequirePermission(Permissions.InvoiceUpdate)]
        public async Task<IActionResult> Cancel(int id, [FromBody] CancelRequest? request)
        {
            var deposit = await this.storage.SelectDepositInvoiceByIdAsync(id);
            if (deposit == null || !deposit.BelongsToCompany(this.companyContext.GetCurrentCompanyId())) return NotFound();

            var cancelErr = DocumentLifecycleRules.RejectIfDepositCannotCancel(deposit.Status);
            if (cancelErr != null)
            {
                if (string.Equals(deposit.Status, "Cancelled", StringComparison.OrdinalIgnoreCase))
                    return Conflict(new { error = cancelErr });
                return BadRequest(cancelErr);
            }

            if (string.Equals(deposit.Status, "Validated", StringComparison.OrdinalIgnoreCase))
            {
                var (_, glError) = await AccountingLedger.PostDepositCancellationAsync(this.storage, this.numberingService, deposit, User.Identity?.Name);
                if (glError != null) return BadRequest(glError);
            }

            deposit.Status = "Cancelled";
            var motif = string.IsNullOrWhiteSpace(request?.Reason) ? "Annulation" : request!.Reason!.Trim();
            deposit.Notes = string.IsNullOrWhiteSpace(deposit.Notes) ? motif : $"{deposit.Notes}\n{motif}";

            var updated = await this.storage.UpdateDepositInvoiceAsync(deposit);
            await AuditDepositAsync(updated.Id, "Cancelled", $"Annulation acompte {updated.DepositNumber}", motif);
            return Ok(updated);
        }

        private async Task AuditDepositAsync(int depositId, string action, string summary, string? details = null)
        {
            await SalesDocumentAudit.LogAsync(
                this.storage,
                this.companyContext.GetCurrentCompanyId(),
                "DepositInvoice",
                depositId,
                action,
                SalesDocumentAudit.ActorFrom(User),
                summary,
                details);
        }
    }
}
