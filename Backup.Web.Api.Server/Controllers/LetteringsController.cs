using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Authorization;
using Authorize = Microsoft.AspNetCore.Authorization.AuthorizeAttribute;
using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Models.Entities;
using Backup.Web.Api.Server.Models.Security;
using Backup.Web.Api.Server.Services.Numbering;
using Backup.Web.Api.Server.Services.Sales;
using Backup.Web.Api.Server.Services.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RESTFulSense.Controllers;

namespace Backup.Web.Api.Server.Controllers
{
    /// <summary>
    /// RG-LT1–4 lite : lettrage client simplifié — regroupement manuel de factures/paiements/avoirs,
    /// sans moteur de proposition automatique de rapprochement.
    /// </summary>
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class LetteringsController : RESTFulController
    {
        private readonly IStorageBroker storage;
        private readonly INumberingSequenceService numberingService;
        private readonly ICompanyContextService companyContext;

        public LetteringsController(
            IStorageBroker storage,
            INumberingSequenceService numberingService,
            ICompanyContextService companyContext)
        {
            this.storage = storage;
            this.numberingService = numberingService;
            this.companyContext = companyContext;
        }

        [HttpGet]
        [RequirePermission(Permissions.InvoiceRead)]
        public IActionResult GetAll([FromQuery] int? customerId = null)
        {
            var query = this.storage.SelectAllLetteringGroups().ForCompany(this.companyContext.GetCurrentCompanyId());
            if (customerId.HasValue) query = query.Where(g => g.CustomerId == customerId.Value);
            return Ok(query.OrderByDescending(g => g.CreatedAt).ToList());
        }

        [HttpGet("{id:int}")]
        [RequirePermission(Permissions.InvoiceRead)]
        public async Task<IActionResult> GetById(int id)
        {
            var group = await this.storage.SelectLetteringGroupByIdAsync(id);
            if (group == null || !group.BelongsToCompany(this.companyContext.GetCurrentCompanyId())) return NotFound();
            return Ok(group);
        }

        public class LetteringItemRequest
        {
            public int? SalesInvoiceId { get; set; }
            public int? PaymentId { get; set; }
            public int? CreditNoteId { get; set; }
            public decimal Amount { get; set; }
        }

        public class CreateLetteringRequest
        {
            public int CustomerId { get; set; }
            public List<LetteringItemRequest> Items { get; set; } = new();
        }

        /// <summary>RG-LT1–4 lite : crée un groupe de lettrage Closed depuis une sélection manuelle de documents.</summary>
        [HttpPost]
        [RequirePermission(Permissions.InvoiceUpdate)]
        public async Task<IActionResult> Post([FromBody] CreateLetteringRequest request)
        {
            if (request.Items == null || request.Items.Count == 0)
                return BadRequest("Au moins une ligne (facture, paiement ou avoir) est requise.");

            var companyId = this.companyContext.GetCurrentCompanyId();
            var customer = await this.storage.SelectCustomerByIdAsync(request.CustomerId);
            if (customer == null) return BadRequest("Client introuvable.");

            var lines = new List<LetteringLine>();
            foreach (var item in request.Items)
            {
                var refCount = (item.SalesInvoiceId.HasValue ? 1 : 0)
                    + (item.PaymentId.HasValue ? 1 : 0)
                    + (item.CreditNoteId.HasValue ? 1 : 0);
                if (refCount != 1)
                    return BadRequest("Chaque ligne doit référencer exactement un document (salesInvoiceId, paymentId ou creditNoteId).");
                if (item.Amount <= 0)
                    return BadRequest("Le montant de chaque ligne doit être positif.");

                decimal documentTotal;
                string documentLabel;

                if (item.SalesInvoiceId.HasValue)
                {
                    var invoice = await this.storage.SelectSalesInvoiceByIdAsync(item.SalesInvoiceId.Value);
                    if (invoice == null || !invoice.BelongsToCompany(companyId) || invoice.CustomerId != request.CustomerId)
                        return BadRequest($"Facture #{item.SalesInvoiceId} introuvable pour ce client.");
                    documentTotal = invoice.TotalTTC;
                    documentLabel = $"facture {invoice.InvoiceNumber}";
                }
                else if (item.PaymentId.HasValue)
                {
                    var payment = await this.storage.SelectPaymentByIdAsync(item.PaymentId.Value);
                    if (payment == null || !payment.BelongsToCompany(companyId))
                        return BadRequest($"Paiement #{item.PaymentId} introuvable.");
                    documentTotal = payment.Amount;
                    documentLabel = $"paiement #{payment.Id}";
                }
                else
                {
                    var creditNote = await this.storage.SelectCreditNoteByIdAsync(item.CreditNoteId!.Value);
                    if (creditNote == null || !creditNote.BelongsToCompany(companyId) || creditNote.CustomerId != request.CustomerId)
                        return BadRequest($"Avoir #{item.CreditNoteId} introuvable pour ce client.");
                    documentTotal = creditNote.TotalTTC;
                    documentLabel = $"avoir {creditNote.CreditNoteNumber}";
                }

                var alreadyLettered = this.GetAlreadyLetteredAmount(item.SalesInvoiceId, item.PaymentId, item.CreditNoteId);
                if (alreadyLettered + item.Amount > documentTotal + 0.01m)
                {
                    return BadRequest(
                        $"Le montant lettré pour {documentLabel} ({alreadyLettered + item.Amount:0.##} €) dépasserait son montant total ({documentTotal:0.##} €).");
                }

                lines.Add(new LetteringLine
                {
                    SalesInvoiceId = item.SalesInvoiceId,
                    PaymentId = item.PaymentId,
                    CreditNoteId = item.CreditNoteId,
                    Amount = item.Amount
                });
            }

            var code = await this.numberingService.GetNextNumberAsync("Lettering", companyId);
            var group = new LetteringGroup
            {
                LetteringCode = code,
                CompanyId = companyId,
                CustomerId = request.CustomerId,
                Status = "Closed",
                CreatedAt = DateTime.UtcNow,
                CreatedBy = SalesDocumentAudit.ActorFrom(User),
                Lines = lines
            };

            var created = await this.storage.InsertLetteringGroupAsync(group);
            return Created(created);
        }

        /// <summary>RG-LT1–4 lite : montant déjà lettré (groupes non délettrés) pour un document donné.</summary>
        private decimal GetAlreadyLetteredAmount(int? salesInvoiceId, int? paymentId, int? creditNoteId)
        {
            return this.storage.SelectAllLetteringGroups()
                .Where(g => !string.Equals(g.Status, "Unlettered", StringComparison.OrdinalIgnoreCase))
                .SelectMany(g => g.Lines ?? new List<LetteringLine>())
                .Where(l => (salesInvoiceId.HasValue && l.SalesInvoiceId == salesInvoiceId.Value)
                    || (paymentId.HasValue && l.PaymentId == paymentId.Value)
                    || (creditNoteId.HasValue && l.CreditNoteId == creditNoteId.Value))
                .Sum(l => l.Amount);
        }

        /// <summary>RG-LT1–4 lite : délettrage autorisé uniquement si la période comptable (Company.OpenFiscalPeriodEnd) est encore ouverte.</summary>
        [HttpPost("{id:int}/unletter")]
        [RequirePermission(Permissions.InvoiceUpdate)]
        public async Task<IActionResult> Unletter(int id)
        {
            var group = await this.storage.SelectLetteringGroupByIdAsync(id);
            if (group == null || !group.BelongsToCompany(this.companyContext.GetCurrentCompanyId())) return NotFound();
            if (string.Equals(group.Status, "Unlettered", StringComparison.OrdinalIgnoreCase))
                return Conflict(new { error = "Ce lettrage est déjà délettré." });

            var companyId = group.CompanyId ?? this.companyContext.GetCurrentCompanyId();
            var company = await this.storage.SelectCompanyByIdAsync(companyId);
            var unletterErr = DocumentLifecycleRules.RejectIfCannotUnletter(company?.OpenFiscalPeriodEnd, DateTime.UtcNow);
            if (unletterErr != null) return BadRequest(unletterErr);

            group.Status = "Unlettered";
            group.UnletteredAt = DateTime.UtcNow;
            group.UnletteredBy = SalesDocumentAudit.ActorFrom(User);

            var updated = await this.storage.UpdateLetteringGroupAsync(group);
            return Ok(updated);
        }
    }
}
