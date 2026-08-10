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
    /// <summary>Avoir fournisseur (AF) — RG-AF1–5.</summary>
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class SupplierCreditNotesController : RESTFulController
    {
        private readonly IStorageBroker storage;
        private readonly INumberingSequenceService numberingService;
        private readonly ICompanyContextService companyContext;

        public SupplierCreditNotesController(IStorageBroker storage, INumberingSequenceService numberingService, ICompanyContextService companyContext)
        {
            this.storage = storage;
            this.numberingService = numberingService;
            this.companyContext = companyContext;
        }

        public class CancelRequest
        {
            public string? Reason { get; set; }
        }

        [HttpGet]
        [RequirePermission(Permissions.SupplierCreditNoteRead)]
        public IActionResult GetAll(
            [FromQuery] string? search = null,
            [FromQuery] int? supplierId = null,
            [FromQuery] int? supplierInvoiceId = null)
        {
            var query = this.storage.SelectAllSupplierCreditNotes().ForCompany(this.companyContext.GetCurrentCompanyId());

            if (supplierId.HasValue)
                query = query.Where(c => c.SupplierId == supplierId.Value);

            if (supplierInvoiceId.HasValue)
                query = query.Where(c => c.SupplierInvoiceId == supplierInvoiceId.Value);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.ToLowerInvariant();
                query = query.Where(c =>
                    c.CreditNoteNumber.ToLower().Contains(s) ||
                    (c.Supplier != null && c.Supplier.Name.ToLower().Contains(s)) ||
                    (c.Notes != null && c.Notes.ToLower().Contains(s)));
            }

            return Ok(query.OrderByDescending(c => c.Date).ToList());
        }

        [HttpGet("{id:int}")]
        [RequirePermission(Permissions.SupplierCreditNoteRead)]
        public async Task<IActionResult> GetById(int id)
        {
            var creditNote = await this.storage.SelectSupplierCreditNoteByIdAsync(id);
            if (creditNote == null || !creditNote.BelongsToCompany(this.companyContext.GetCurrentCompanyId())) return NotFound();
            return Ok(creditNote);
        }

        [HttpPost]
        [RequirePermission(Permissions.SupplierCreditNoteCreate)]
        public async Task<IActionResult> Post([FromBody] SupplierCreditNoteEntity creditNote)
        {
            creditNote.EnsureCompanyId(this.companyContext.GetCurrentCompanyId());
            NormalizeCreditNote(creditNote);
            var validation = await this.ValidateSupplierCreditNoteAsync(creditNote);
            if (validation != null) return validation;

            await this.EnsureCreditNoteNumberAsync(creditNote);

            var created = await this.storage.InsertSupplierCreditNoteAsync(creditNote);
            await this.AuditSupplierCreditNote(created.Id, "Created", $"Création avoir fournisseur {created.CreditNoteNumber}");
            return Created(created);
        }

        [HttpPut("{id:int}")]
        [RequirePermission(Permissions.SupplierCreditNoteUpdate)]
        public async Task<IActionResult> Put(int id, [FromBody] SupplierCreditNoteEntity creditNote)
        {
            var existing = await this.storage.SelectSupplierCreditNoteByIdAsync(id);
            if (existing == null || !existing.BelongsToCompany(this.companyContext.GetCurrentCompanyId())) return NotFound();

            if (!string.Equals(existing.Status, "Draft", StringComparison.OrdinalIgnoreCase))
                return BadRequest($"Un avoir fournisseur au statut {existing.Status} ne peut plus être modifié.");

            existing.SupplierId = creditNote.SupplierId;
            existing.SupplierInvoiceId = creditNote.SupplierInvoiceId;
            existing.Date = creditNote.Date == default ? existing.Date : creditNote.Date;
            existing.Notes = creditNote.Notes;
            existing.Lines = creditNote.Lines ?? new List<SupplierCreditNoteLineEntity>();

            NormalizeCreditNote(existing);
            var validation = await this.ValidateSupplierCreditNoteAsync(existing, id);
            if (validation != null) return validation;

            var updated = await this.storage.UpdateSupplierCreditNoteAsync(existing);
            return Ok(updated);
        }

        /// <summary>RG-AF3 : validation → écriture comptable inverse (débit 401, crédit 607/44566).</summary>
        [HttpPost("{id:int}/validate")]
        [RequirePermission(Permissions.SupplierCreditNoteUpdate)]
        public async Task<IActionResult> Validate(int id)
        {
            var creditNote = await this.storage.SelectSupplierCreditNoteByIdAsync(id);
            if (creditNote == null || !creditNote.BelongsToCompany(this.companyContext.GetCurrentCompanyId())) return NotFound();

            var validateErr = DocumentLifecycleRules.RejectIfSupplierCreditCannotValidate(creditNote.Status);
            if (validateErr != null) return Conflict(new { error = validateErr });

            if (creditNote.Lines == null || creditNote.Lines.Count == 0)
                return BadRequest("L'avoir fournisseur doit contenir au moins une ligne.");

            NormalizeCreditNote(creditNote);

            var capError = await ValidateCapAsync(creditNote);
            if (capError != null) return BadRequest(capError);

            if (!AccountingLedger.HasPostedEntry(this.storage, AccountingLedger.RefSupplierCreditNote, creditNote.Id, creditNote.CompanyId))
            {
                var (_, glError) = await AccountingLedger.PostSupplierCreditNoteAsync(
                    this.storage, this.numberingService, creditNote, SalesDocumentAudit.ActorFrom(User));
                if (glError != null) return BadRequest(glError);
            }

            creditNote.Status = "Validated";
            var updated = await this.storage.UpdateSupplierCreditNoteAsync(creditNote);
            await this.AuditSupplierCreditNote(updated.Id, "Validated", $"Validation avoir fournisseur {updated.CreditNoteNumber}");
            return Ok(updated);
        }

        /// <summary>RG-AF4 : application → réduit l'encours fournisseur (Balance).</summary>
        [HttpPost("{id:int}/apply")]
        [RequirePermission(Permissions.SupplierCreditNoteUpdate)]
        public async Task<IActionResult> Apply(int id)
        {
            var creditNote = await this.storage.SelectSupplierCreditNoteByIdAsync(id);
            if (creditNote == null || !creditNote.BelongsToCompany(this.companyContext.GetCurrentCompanyId())) return NotFound();

            var applyErr = DocumentLifecycleRules.RejectIfSupplierCreditCannotApply(creditNote.Status);
            if (applyErr != null)
            {
                if (string.Equals(creditNote.Status, "Applied", StringComparison.OrdinalIgnoreCase))
                    return Conflict(new { error = applyErr });
                return BadRequest(applyErr);
            }

            if (string.Equals(creditNote.Status, "Draft", StringComparison.OrdinalIgnoreCase))
                creditNote.Status = "Validated";

            NormalizeCreditNote(creditNote);

            var capError = await ValidateCapAsync(creditNote);
            if (capError != null) return BadRequest(capError);

            if (!AccountingLedger.HasPostedEntry(this.storage, AccountingLedger.RefSupplierCreditNote, creditNote.Id, creditNote.CompanyId))
            {
                var (_, glError) = await AccountingLedger.PostSupplierCreditNoteAsync(
                    this.storage, this.numberingService, creditNote, SalesDocumentAudit.ActorFrom(User));
                if (glError != null) return BadRequest(glError);
            }

            var supplier = await this.storage.SelectSupplierByIdAsync(creditNote.SupplierId);
            if (supplier != null)
            {
                supplier.Balance = Math.Max(0, supplier.Balance - creditNote.TotalTTC);
                supplier.UpdatedAt = DateTime.UtcNow;
                await this.storage.UpdateSupplierAsync(supplier);
            }

            creditNote.Status = "Applied";
            var updated = await this.storage.UpdateSupplierCreditNoteAsync(creditNote);
            await this.AuditSupplierCreditNote(updated.Id, "Applied", $"Application avoir fournisseur {updated.CreditNoteNumber}");
            return Ok(updated);
        }

        /// <summary>RG-AF5 : annulation possible Draft/Validated uniquement (pas après Apply).</summary>
        [HttpPost("{id:int}/cancel")]
        [RequirePermission(Permissions.SupplierCreditNoteUpdate)]
        public async Task<IActionResult> Cancel(int id, [FromBody] CancelRequest? request)
        {
            var creditNote = await this.storage.SelectSupplierCreditNoteByIdAsync(id);
            if (creditNote == null || !creditNote.BelongsToCompany(this.companyContext.GetCurrentCompanyId())) return NotFound();

            var cancelErr = DocumentLifecycleRules.RejectIfSupplierCreditCannotCancel(creditNote.Status);
            if (cancelErr != null)
            {
                if (string.Equals(creditNote.Status, "Cancelled", StringComparison.OrdinalIgnoreCase))
                    return Conflict(new { error = cancelErr });
                return BadRequest(cancelErr);
            }

            creditNote.Status = "Cancelled";
            var motif = string.IsNullOrWhiteSpace(request?.Reason) ? "Annulation" : request!.Reason!.Trim();
            creditNote.Notes = string.IsNullOrWhiteSpace(creditNote.Notes) ? motif : $"{creditNote.Notes}\n{motif}";

            var updated = await this.storage.UpdateSupplierCreditNoteAsync(creditNote);
            await this.AuditSupplierCreditNote(updated.Id, "Cancelled", $"Annulation avoir fournisseur {updated.CreditNoteNumber}", motif);
            return Ok(updated);
        }

        private async Task<IActionResult?> ValidateSupplierCreditNoteAsync(SupplierCreditNoteEntity creditNote, int? currentId = null)
        {
            if (creditNote.SupplierId <= 0) return BadRequest("SupplierId required");

            var supplier = await this.storage.SelectSupplierByIdAsync(creditNote.SupplierId);
            if (supplier == null) return BadRequest("Supplier not found");

            // RG-AF1 : avoir fournisseur obligatoirement lié à une facture fournisseur.
            if (creditNote.SupplierInvoiceId <= 0)
                return BadRequest("Un avoir fournisseur doit être lié à une facture fournisseur.");

            var invoice = await this.storage.SelectSupplierInvoiceByIdAsync(creditNote.SupplierInvoiceId);
            if (invoice == null) return BadRequest("Linked supplier invoice not found");
            if (invoice.SupplierId != creditNote.SupplierId)
                return BadRequest("Linked supplier invoice must belong to the same supplier.");

            var capError = await ValidateCapAsync(creditNote, currentId);
            if (capError != null) return BadRequest(capError);

            if (!string.IsNullOrWhiteSpace(creditNote.CreditNoteNumber))
            {
                var normalized = creditNote.CreditNoteNumber.Trim().ToLowerInvariant();
                var duplicate = this.storage.SelectAllSupplierCreditNotes()
                    .ForCompany(this.companyContext.GetCurrentCompanyId())
                    .FirstOrDefault(c =>
                        c.CreditNoteNumber.ToLower() == normalized &&
                        (!currentId.HasValue || c.Id != currentId.Value));

                if (duplicate != null)
                {
                    return Conflict(new
                    {
                        error = "A supplier credit note with the same number already exists.",
                        supplierCreditNoteId = duplicate.Id
                    });
                }
            }

            return null;
        }

        /// <summary>RG-AF2 : Σ avoirs fournisseur (hors Cancelled) ≤ TTC facture fournisseur.</summary>
        private async Task<string?> ValidateCapAsync(SupplierCreditNoteEntity creditNote, int? currentId = null)
        {
            if (creditNote.SupplierInvoiceId <= 0) return null;

            var invoice = await this.storage.SelectSupplierInvoiceByIdAsync(creditNote.SupplierInvoiceId);
            if (invoice == null) return "Linked supplier invoice not found";

            var existing = this.storage.SelectAllSupplierCreditNotes()
                .Where(c => c.SupplierInvoiceId == invoice.Id
                    && c.Status != "Cancelled"
                    && (!currentId.HasValue || c.Id != currentId.Value))
                .AsEnumerable()
                .Sum(c => c.TotalTTC);

            var total = existing + creditNote.TotalTTC;
            if (total > invoice.TotalTTC + 0.01m)
            {
                return $"Le total des avoirs fournisseur ({total:0.##} €) dépasserait le TTC de la facture {invoice.InvoiceNumber} ({invoice.TotalTTC:0.##} €).";
            }

            return null;
        }

        private async Task EnsureCreditNoteNumberAsync(SupplierCreditNoteEntity creditNote)
        {
            if (!string.IsNullOrWhiteSpace(creditNote.CreditNoteNumber))
            {
                creditNote.CreditNoteNumber = creditNote.CreditNoteNumber.Trim();
                return;
            }

            creditNote.CreditNoteNumber = await this.numberingService.GetNextNumberAsync("SupplierCreditNote", creditNote.CompanyId);
        }

        private static void NormalizeCreditNote(SupplierCreditNoteEntity creditNote)
        {
            creditNote.Date = creditNote.Date == default ? DateTime.UtcNow : creditNote.Date;
            creditNote.CreatedAt = creditNote.CreatedAt == default ? DateTime.UtcNow : creditNote.CreatedAt;
            creditNote.Status = string.IsNullOrWhiteSpace(creditNote.Status) ? "Draft" : creditNote.Status.Trim();
            creditNote.Lines ??= new List<SupplierCreditNoteLineEntity>();

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

        private async Task AuditSupplierCreditNote(int creditNoteId, string action, string summary, string? details = null)
        {
            await Backup.Web.Api.Server.Services.Sales.SalesDocumentAudit.LogAsync(
                this.storage,
                this.companyContext.GetCurrentCompanyId(),
                "SupplierCreditNote",
                creditNoteId,
                action,
                Backup.Web.Api.Server.Services.Sales.SalesDocumentAudit.ActorFrom(User),
                summary,
                details);
        }
    }
}
