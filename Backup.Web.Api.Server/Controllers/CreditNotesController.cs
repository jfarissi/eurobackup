using System;
using System.Collections.Generic;
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
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class CreditNotesController : RESTFulController
    {
        private readonly IStorageBroker storage;
        private readonly INumberingSequenceService numberingService;
        private readonly ICompanyContextService companyContext;

        public CreditNotesController(IStorageBroker storage, INumberingSequenceService numberingService, ICompanyContextService companyContext)
        {
            this.storage = storage;
            this.numberingService = numberingService;
            this.companyContext = companyContext;
        }

        public class CreateFromInvoiceLineRequest
        {
            public int? InvoiceLineId { get; set; }
            public int? LineNumber { get; set; }
            public string? ProductKey { get; set; }
            public decimal Quantity { get; set; }
        }

        public class CreateFromInvoiceRequest
        {
            public int SalesInvoiceId { get; set; }
            public string? Notes { get; set; }
            public string? CompanyId { get; set; }
            /// <summary>Lignes à créditer ; si null/vide = toute la facture (compat).</summary>
            public List<CreateFromInvoiceLineRequest>? Lines { get; set; }
        }

        [HttpGet]
        [RequirePermission(Permissions.InvoiceRead)]
        public IActionResult GetAll([FromQuery] string? search = null, [FromQuery] int? customerId = null, [FromQuery] int? salesInvoiceId = null)
        {
            var query = this.storage.SelectAllCreditNotes().ForCompany(this.companyContext.GetCurrentCompanyId());

            if (customerId.HasValue)
            {
                query = query.Where(c => c.CustomerId == customerId.Value);
            }

            if (salesInvoiceId.HasValue)
            {
                query = query.Where(c => c.SalesInvoiceId == salesInvoiceId.Value);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.ToLowerInvariant();
                query = query.Where(c =>
                    c.CreditNoteNumber.ToLower().Contains(s) ||
                    (c.Customer != null && c.Customer.Name.ToLower().Contains(s)) ||
                    (c.Notes != null && c.Notes.ToLower().Contains(s)));
            }

            return Ok(query.OrderByDescending(c => c.Date).ToList());
        }

        [HttpGet("{id:int}")]
        [RequirePermission(Permissions.InvoiceRead)]
        public async Task<IActionResult> GetById(int id)
        {
            var creditNote = await this.storage.SelectCreditNoteByIdAsync(id);
            if (creditNote == null || !creditNote.BelongsToCompany(this.companyContext.GetCurrentCompanyId())) return NotFound();
            return Ok(creditNote);
        }

        [HttpPost]
        [RequirePermission(Permissions.InvoiceCreate)]
        public async Task<IActionResult> Post([FromBody] CreditNoteEntity creditNote)
        {
            creditNote.EnsureCompanyId(this.companyContext.GetCurrentCompanyId());
            NormalizeCreditNote(creditNote);
            var validation = await this.ValidateCreditNoteAsync(creditNote);
            if (validation != null) return validation;

            // RG-CP1 : devise figée à la création depuis Company.DefaultCurrencyCode.
            creditNote.CurrencyCode = await SalesBusinessRules.ResolveCompanyCurrencyAsync(this.storage, creditNote.CompanyId);

            await this.EnsureCreditNoteNumberAsync(creditNote);

            var created = await this.storage.InsertCreditNoteAsync(creditNote);
            await SalesDocumentAudit.LogAsync(
                this.storage, created.CompanyId ?? this.companyContext.GetCurrentCompanyId(),
                "CreditNote", created.Id, "Created", SalesDocumentAudit.ActorFrom(User),
                $"Création avoir {created.CreditNoteNumber}");
            return Created(created);
        }

        [HttpPut("{id:int}")]
        [RequirePermission(Permissions.InvoiceUpdate)]
        public async Task<IActionResult> Put(int id, [FromBody] CreditNoteEntity creditNote)
        {
            var existing = await this.storage.SelectCreditNoteByIdAsync(id);
            if (existing == null || !existing.BelongsToCompany(this.companyContext.GetCurrentCompanyId())) return NotFound();

            if (!string.Equals(existing.Status, "Draft", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest("Only draft credit notes can be fully edited.");
            }

            existing.CreditNoteNumber = string.IsNullOrWhiteSpace(creditNote.CreditNoteNumber)
                ? existing.CreditNoteNumber
                : creditNote.CreditNoteNumber.Trim();
            existing.CustomerId = creditNote.CustomerId;
            existing.SalesInvoiceId = creditNote.SalesInvoiceId;
            existing.Date = creditNote.Date == default ? existing.Date : creditNote.Date;
            existing.Status = string.IsNullOrWhiteSpace(creditNote.Status) ? existing.Status : creditNote.Status.Trim();
            existing.Notes = creditNote.Notes;
            existing.CompanyId = creditNote.CompanyId;
            existing.Lines = creditNote.Lines ?? new List<CreditNoteLineEntity>();

            NormalizeCreditNote(existing);
            var validation = await this.ValidateCreditNoteAsync(existing, id);
            if (validation != null) return validation;

            var updated = await this.storage.UpdateCreditNoteAsync(existing);
            return Ok(updated);
        }

        [HttpPost("from-invoice")]
        [RequirePermission(Permissions.InvoiceCreate)]
        public async Task<IActionResult> CreateFromInvoice([FromBody] CreateFromInvoiceRequest request)
        {
            if (request.SalesInvoiceId <= 0) return BadRequest("SalesInvoiceId required");

            var invoice = await this.storage.SelectSalesInvoiceByIdAsync(request.SalesInvoiceId);
            if (invoice == null || !invoice.BelongsToCompany(this.companyContext.GetCurrentCompanyId())) return NotFound("Sales invoice not found");

            var invoiceLines = invoice.Lines ?? new List<SalesInvoiceLine>();
            if (invoiceLines.Count == 0) return BadRequest("Sales invoice has no lines.");

            List<CreditNoteLineEntity> creditLines;
            if (request.Lines == null)
            {
                // Compat : avoir complet si aucune sélection envoyée
                creditLines = invoiceLines.Select((line, index) => MapInvoiceLineToCredit(line, line.Quantity, index)).ToList();
            }
            else if (request.Lines.Count == 0)
            {
                return BadRequest("Select at least one invoice line with a positive quantity.");
            }
            else
            {
                creditLines = new List<CreditNoteLineEntity>();
                var usedInvoiceLineIds = new HashSet<int>();

                foreach (var selection in request.Lines)
                {
                    if (selection.Quantity <= 0) continue;

                    var invoiceLine = ResolveInvoiceLine(invoiceLines, selection, usedInvoiceLineIds);
                    if (invoiceLine == null)
                        return BadRequest($"Invoice line not found for selection (lineNumber={selection.LineNumber}, productKey={selection.ProductKey}).");

                    if (invoiceLine.Id > 0) usedInvoiceLineIds.Add(invoiceLine.Id);

                    var qty = Math.Min(selection.Quantity, invoiceLine.Quantity);
                    if (qty <= 0) continue;

                    creditLines.Add(MapInvoiceLineToCredit(invoiceLine, qty, creditLines.Count));
                }

                if (creditLines.Count == 0)
                    return BadRequest("Select at least one invoice line with a positive quantity.");
            }

            var creditNote = new CreditNoteEntity
            {
                CustomerId = invoice.CustomerId,
                SalesInvoiceId = invoice.Id,
                Date = DateTime.UtcNow,
                Status = "Draft",
                Notes = string.IsNullOrWhiteSpace(request.Notes)
                    ? $"Created from sales invoice {invoice.InvoiceNumber}"
                    : request.Notes.Trim(),
                CompanyId = request.CompanyId ?? invoice.CompanyId ?? this.companyContext.GetCurrentCompanyId(),
                CreatedAt = DateTime.UtcNow,
                Lines = creditLines
            };

            await this.EnsureCreditNoteNumberAsync(creditNote);
            NormalizeCreditNote(creditNote);

            var capError = SalesBusinessRules.ValidateCreditCap(this.storage, invoice, creditNote.TotalTTC);
            if (capError != null) return BadRequest(capError);

            var created = await this.storage.InsertCreditNoteAsync(creditNote);
            return Created(created);
        }

        private static SalesInvoiceLine? ResolveInvoiceLine(
            List<SalesInvoiceLine> invoiceLines,
            CreateFromInvoiceLineRequest selection,
            HashSet<int> usedInvoiceLineIds)
        {
            if (selection.InvoiceLineId.HasValue && selection.InvoiceLineId.Value > 0)
            {
                return invoiceLines.FirstOrDefault(l =>
                    l.Id == selection.InvoiceLineId.Value &&
                    !usedInvoiceLineIds.Contains(l.Id));
            }

            if (selection.LineNumber.HasValue && selection.LineNumber.Value > 0)
            {
                var byNumber = invoiceLines.FirstOrDefault(l =>
                    l.LineNumber == selection.LineNumber.Value &&
                    (l.Id <= 0 || !usedInvoiceLineIds.Contains(l.Id)));
                if (byNumber != null) return byNumber;
            }

            if (!string.IsNullOrWhiteSpace(selection.ProductKey))
            {
                var key = selection.ProductKey.Trim();
                return invoiceLines.FirstOrDefault(l =>
                    string.Equals(l.ProductKey?.Trim(), key, StringComparison.OrdinalIgnoreCase) &&
                    (l.Id <= 0 || !usedInvoiceLineIds.Contains(l.Id)));
            }

            return null;
        }

        private static CreditNoteLineEntity MapInvoiceLineToCredit(SalesInvoiceLine line, decimal quantity, int index)
        {
            return new CreditNoteLineEntity
            {
                ProductKey = line.ProductKey,
                Description = line.Description,
                Quantity = quantity,
                UnitPrice = line.UnitPrice,
                VatRate = line.VatRate,
                TotalHT = quantity * line.UnitPrice,
                TotalTTC = quantity * line.UnitPrice * (1 + (line.VatRate / 100m)),
                LineNumber = line.LineNumber > 0 ? line.LineNumber : index + 1
            };
        }

        [HttpPost("{id:int}/validate")]
        [RequirePermission(Permissions.InvoiceUpdate)]
        public async Task<IActionResult> Validate(int id)
        {
            var creditNote = await this.storage.SelectCreditNoteByIdAsync(id);
            if (creditNote == null || !creditNote.BelongsToCompany(this.companyContext.GetCurrentCompanyId())) return NotFound("Credit note not found");

            if (!string.Equals(creditNote.Status, "Draft", StringComparison.OrdinalIgnoreCase))
            {
                return Conflict(new { error = $"Credit note #{id} is already {creditNote.Status}." });
            }

            if (creditNote.Lines == null || creditNote.Lines.Count == 0)
            {
                return BadRequest("Credit note must contain at least one line.");
            }

            NormalizeCreditNote(creditNote);
            if (creditNote.SalesInvoiceId.HasValue)
            {
                var invoice = await this.storage.SelectSalesInvoiceByIdAsync(creditNote.SalesInvoiceId.Value);
                if (invoice == null) return BadRequest("Linked sales invoice not found");
                var parentErr = SalesBusinessRules.RejectIfParentUnusable(invoice.Status, $"facture {invoice.InvoiceNumber}");
                if (parentErr != null) return BadRequest(parentErr);
                var capError = SalesBusinessRules.ValidateCreditCap(this.storage, invoice, creditNote.TotalTTC, creditNote.Id);
                if (capError != null) return BadRequest(capError);
            }

            // RG-AC4 : retour physique (BRC) obligatoirement Intégré avant de valider l'avoir lié.
            var returnErr = await ValidateLinkedSalesReturnIntegratedAsync(creditNote);
            if (returnErr != null) return returnErr;

            // RG-AC3 : écriture comptable inverse à la validation.
            if (!AccountingLedger.HasPostedEntry(this.storage, AccountingLedger.RefCreditNote, creditNote.Id, creditNote.CompanyId))
            {
                var (_, glError) = await AccountingLedger.PostCreditNoteAsync(
                    this.storage, this.numberingService, creditNote, SalesDocumentAudit.ActorFrom(User));
                if (glError != null) return BadRequest(glError);
            }

            // Avoir lié à une facture : validation = compensation (reste dû + statut facture).
            // Évite le piège « Validated » sans Apply qui laisse Payé=0 / Reste=TTC.
            if (creditNote.SalesInvoiceId.HasValue)
            {
                var settleError = await this.SettleCreditNoteOnInvoiceAsync(creditNote);
                if (settleError != null) return settleError;

                creditNote.Status = "Applied";
                var applied = await this.storage.UpdateCreditNoteAsync(creditNote);
                await SalesDocumentAudit.LogAsync(
                    this.storage, applied.CompanyId ?? this.companyContext.GetCurrentCompanyId(),
                    "CreditNote", applied.Id, "Applied", SalesDocumentAudit.ActorFrom(User),
                    $"Validation+compensation avoir {applied.CreditNoteNumber}");
                return Ok(applied);
            }

            creditNote.Status = "Validated";
            var updated = await this.storage.UpdateCreditNoteAsync(creditNote);
            await SalesDocumentAudit.LogAsync(
                this.storage, updated.CompanyId ?? this.companyContext.GetCurrentCompanyId(),
                "CreditNote", updated.Id, "Validated", SalesDocumentAudit.ActorFrom(User),
                $"Validation avoir {updated.CreditNoteNumber}");
            return Ok(updated);
        }

        [HttpPost("{id:int}/apply")]
        [RequirePermission(Permissions.InvoiceUpdate)]
        public async Task<IActionResult> Apply(int id)
        {
            var creditNote = await this.storage.SelectCreditNoteByIdAsync(id);
            if (creditNote == null || !creditNote.BelongsToCompany(this.companyContext.GetCurrentCompanyId())) return NotFound("Credit note not found");

            if (string.Equals(creditNote.Status, "Applied", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(creditNote.Status, "Refunded", StringComparison.OrdinalIgnoreCase))
            {
                return Conflict(new { error = $"Credit note #{id} is already {creditNote.Status}." });
            }

            if (string.Equals(creditNote.Status, "Draft", StringComparison.OrdinalIgnoreCase))
            {
                creditNote.Status = "Validated";
            }

            NormalizeCreditNote(creditNote);

            // RG-AC4 : retour physique (BRC) obligatoirement Intégré avant compensation de l'avoir lié.
            var returnErr = await ValidateLinkedSalesReturnIntegratedAsync(creditNote);
            if (returnErr != null) return returnErr;

            if (creditNote.SalesInvoiceId.HasValue)
            {
                var settleError = await this.SettleCreditNoteOnInvoiceAsync(creditNote);
                if (settleError != null) return settleError;
            }

            // RG-A3 : écriture inverse avant maj encours
            if (!AccountingLedger.HasPostedEntry(this.storage, AccountingLedger.RefCreditNote, creditNote.Id, creditNote.CompanyId))
            {
                var (_, glError) = await AccountingLedger.PostCreditNoteAsync(
                    this.storage, this.numberingService, creditNote, SalesDocumentAudit.ActorFrom(User));
                if (glError != null) return BadRequest(glError);
            }

            if (!creditNote.SalesInvoiceId.HasValue)
            {
                var customer = await this.storage.SelectCustomerByIdAsync(creditNote.CustomerId);
                if (customer != null)
                {
                    customer.Balance -= creditNote.TotalTTC;
                    customer.UpdatedAt = DateTime.UtcNow;
                    await this.storage.UpdateCustomerAsync(customer);
                }
            }

            creditNote.Status = "Applied";
            var updated = await this.storage.UpdateCreditNoteAsync(creditNote);
            await SalesDocumentAudit.LogAsync(
                this.storage, updated.CompanyId ?? this.companyContext.GetCurrentCompanyId(),
                "CreditNote", updated.Id, "Applied", SalesDocumentAudit.ActorFrom(User),
                $"Compensation avoir {updated.CreditNoteNumber}");
            return Ok(updated);
        }

        /// <summary>Compensation facture + encours client (sans changer le statut de l'avoir).</summary>
        private async Task<IActionResult?> SettleCreditNoteOnInvoiceAsync(CreditNoteEntity creditNote)
        {
            if (!creditNote.SalesInvoiceId.HasValue) return null;

            var invoice = await this.storage.SelectSalesInvoiceByIdAsync(creditNote.SalesInvoiceId.Value);
            if (invoice == null) return BadRequest("Linked sales invoice not found");

            if (invoice.CustomerId != creditNote.CustomerId)
                return BadRequest("Credit note and sales invoice must belong to the same customer.");

            var capError = SalesBusinessRules.ValidateCreditCap(this.storage, invoice, creditNote.TotalTTC, creditNote.Id);
            if (capError != null) return BadRequest(capError);

            // Exclure cet avoir s'il est déjà Validated (comptabilisé dans GetAppliedCreditTotal).
            var creditedAfter = SalesInvoiceSettlement.GetAppliedCreditTotal(this.storage, invoice.Id, creditNote.Id)
                + creditNote.TotalTTC;
            SalesInvoiceSettlement.RefreshPaymentStatus(invoice, creditedAfter);

            var applyNote = $"Avoir {creditNote.CreditNoteNumber} compensé ({creditNote.TotalTTC:0.##} €)";
            invoice.Notes = string.IsNullOrWhiteSpace(invoice.Notes)
                ? applyNote
                : $"{invoice.Notes}{Environment.NewLine}{applyNote}";

            await this.storage.UpdateSalesInvoiceAsync(invoice);

            var customer = await this.storage.SelectCustomerByIdAsync(creditNote.CustomerId);
            if (customer != null)
            {
                customer.Balance -= creditNote.TotalTTC;
                customer.UpdatedAt = DateTime.UtcNow;
                await this.storage.UpdateCustomerAsync(customer);
            }

            return null;
        }

        /// <summary>RG-AC5 : remboursement bancaire (sans compensation sur facture).</summary>
        [HttpPost("{id:int}/refund")]
        [RequirePermission(Permissions.InvoiceUpdate)]
        public async Task<IActionResult> Refund(int id, [FromBody] RefundRequest? request)
        {
            var creditNote = await this.storage.SelectCreditNoteByIdAsync(id);
            if (creditNote == null || !creditNote.BelongsToCompany(this.companyContext.GetCurrentCompanyId()))
                return NotFound("Credit note not found");

            var refundErr = DocumentLifecycleRules.RejectIfCreditNoteCannotRefund(creditNote.Status);
            if (refundErr != null)
            {
                if (string.Equals(creditNote.Status, "Refunded", StringComparison.OrdinalIgnoreCase))
                    return Conflict(new { error = refundErr });
                return BadRequest(refundErr);
            }

            if (!AccountingLedger.HasPostedEntry(this.storage, AccountingLedger.RefCreditNote, creditNote.Id, creditNote.CompanyId))
            {
                var (_, glError) = await AccountingLedger.PostCreditNoteAsync(
                    this.storage, this.numberingService, creditNote, SalesDocumentAudit.ActorFrom(User));
                if (glError != null) return BadRequest(glError);
            }

            var customer = await this.storage.SelectCustomerByIdAsync(creditNote.CustomerId);
            if (customer != null)
            {
                customer.Balance -= creditNote.TotalTTC;
                customer.UpdatedAt = DateTime.UtcNow;
                await this.storage.UpdateCustomerAsync(customer);
            }

            var note = string.IsNullOrWhiteSpace(request?.Reference)
                ? "Remboursement bancaire"
                : $"Remboursement bancaire — {request!.Reference!.Trim()}";
            creditNote.Notes = string.IsNullOrWhiteSpace(creditNote.Notes)
                ? note
                : $"{creditNote.Notes}{Environment.NewLine}{note}";
            creditNote.Status = "Refunded";

            var updated = await this.storage.UpdateCreditNoteAsync(creditNote);
            await SalesDocumentAudit.LogAsync(
                this.storage, updated.CompanyId ?? this.companyContext.GetCurrentCompanyId(),
                "CreditNote", updated.Id, "Refunded", SalesDocumentAudit.ActorFrom(User),
                $"Remboursement avoir {updated.CreditNoteNumber}");
            return Ok(updated);
        }

        public class RefundRequest
        {
            public string? Reference { get; set; }
            public string? Notes { get; set; }
        }

        [HttpPost("{id:int}/cancel")]
        [RequirePermission(Permissions.InvoiceUpdate)]
        public async Task<IActionResult> Cancel(int id, [FromBody] CancelRequest? request)
        {
            var creditNote = await this.storage.SelectCreditNoteByIdAsync(id);
            if (creditNote == null || !creditNote.BelongsToCompany(this.companyContext.GetCurrentCompanyId())) return NotFound();
            if (string.Equals(creditNote.Status, "Applied", StringComparison.OrdinalIgnoreCase)
                || string.Equals(creditNote.Status, "Refunded", StringComparison.OrdinalIgnoreCase))
                return BadRequest("Un avoir déjà appliqué ne peut pas être annulé.");
            if (string.Equals(creditNote.Status, "Cancelled", StringComparison.OrdinalIgnoreCase))
                return Conflict(new { error = "Avoir déjà annulé." });

            creditNote.Status = "Cancelled";
            var motif = string.IsNullOrWhiteSpace(request?.Reason) ? "Annulation" : request!.Reason!.Trim();
            creditNote.Notes = string.IsNullOrWhiteSpace(creditNote.Notes) ? motif : $"{creditNote.Notes}\n{motif}";
            var updated = await this.storage.UpdateCreditNoteAsync(creditNote);
            return Ok(updated);
        }

        public class CancelRequest
        {
            public string? Reason { get; set; }
        }

        /// <summary>RG-AC4 : quand un avoir est lié à un retour physique (BRC), celui-ci doit être Intégré.</summary>
        private async Task<IActionResult?> ValidateLinkedSalesReturnIntegratedAsync(CreditNoteEntity creditNote)
        {
            if (!creditNote.SalesReturnId.HasValue || creditNote.SalesReturnId.Value <= 0) return null;

            var salesReturn = await this.storage.SelectSalesReturnByIdAsync(creditNote.SalesReturnId.Value);
            var err = SalesBusinessRules.RejectIfSalesReturnNotIntegrated(salesReturn, creditNote.SalesReturnId);
            return err == null ? null : BadRequest(err);
        }

        private async Task<IActionResult?> ValidateCreditNoteAsync(CreditNoteEntity creditNote, int? currentId = null)
        {
            if (creditNote.CustomerId <= 0) return BadRequest("CustomerId required");

            var customer = await this.storage.SelectCustomerByIdAsync(creditNote.CustomerId);
            if (customer == null) return BadRequest("Customer not found");
            var partyErr = SalesBusinessRules.RejectIfPartyNotActive(customer.Status, customer.Name);
            if (partyErr != null) return BadRequest(partyErr);

            // RG-AC1 : avoir obligatoirement lié à une facture.
            if (!creditNote.SalesInvoiceId.HasValue || creditNote.SalesInvoiceId.Value <= 0)
                return BadRequest("Un avoir doit être lié à une facture client.");

            {
                var invoice = await this.storage.SelectSalesInvoiceByIdAsync(creditNote.SalesInvoiceId.Value);
                if (invoice == null) return BadRequest("Linked sales invoice not found");
                var parentErr = SalesBusinessRules.RejectIfParentUnusable(invoice.Status, $"facture {invoice.InvoiceNumber}");
                if (parentErr != null) return BadRequest(parentErr);
                if (invoice.CustomerId != creditNote.CustomerId)
                {
                    return BadRequest("Linked sales invoice must belong to the same customer.");
                }

                var capError = SalesBusinessRules.ValidateCreditCap(
                    this.storage,
                    invoice,
                    creditNote.TotalTTC,
                    currentId);
                if (capError != null) return BadRequest(capError);
            }

            if (!string.IsNullOrWhiteSpace(creditNote.CreditNoteNumber))
            {
                var normalized = creditNote.CreditNoteNumber.Trim().ToLowerInvariant();
                var duplicate = this.storage.SelectAllCreditNotes()
                    .ForCompany(this.companyContext.GetCurrentCompanyId())
                    .FirstOrDefault(c =>
                        c.CreditNoteNumber.ToLower() == normalized &&
                        (!currentId.HasValue || c.Id != currentId.Value));

                if (duplicate != null)
                {
                    return Conflict(new
                    {
                        error = "A credit note with the same number already exists.",
                        creditNoteId = duplicate.Id
                    });
                }
            }

            return null;
        }

        private async Task EnsureCreditNoteNumberAsync(CreditNoteEntity creditNote)
        {
            if (!string.IsNullOrWhiteSpace(creditNote.CreditNoteNumber))
            {
                creditNote.CreditNoteNumber = creditNote.CreditNoteNumber.Trim();
                return;
            }

            creditNote.CreditNoteNumber = await this.numberingService.GetNextNumberAsync("CreditNote", creditNote.CompanyId);
        }

        private static void NormalizeCreditNote(CreditNoteEntity creditNote)
        {
            creditNote.Date = creditNote.Date == default ? DateTime.UtcNow : creditNote.Date;
            creditNote.CreatedAt = creditNote.CreatedAt == default ? DateTime.UtcNow : creditNote.CreatedAt;
            creditNote.Status = string.IsNullOrWhiteSpace(creditNote.Status) ? "Draft" : creditNote.Status.Trim();
            creditNote.Lines ??= new List<CreditNoteLineEntity>();

            for (int i = 0; i < creditNote.Lines.Count; i++)
            {
                var line = creditNote.Lines[i];
                line.LineNumber = line.LineNumber <= 0 ? i + 1 : line.LineNumber;
                line.Description = line.Description?.Trim() ?? string.Empty;
                line.ProductKey = line.ProductKey?.Trim() ?? string.Empty;
                line.TotalHT = line.Quantity * line.UnitPrice;
                line.TotalTTC = line.TotalHT * (1 + (line.VatRate / 100m));
            }

            creditNote.TotalHT = creditNote.Lines.Sum(l => l.TotalHT);
            creditNote.TotalVat = creditNote.Lines.Sum(l => l.TotalTTC - l.TotalHT);
            creditNote.TotalTTC = creditNote.Lines.Sum(l => l.TotalTTC);
        }
    }
}
