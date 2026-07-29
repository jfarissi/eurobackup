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

        public class CreateFromInvoiceRequest
        {
            public int SalesInvoiceId { get; set; }
            public string? Notes { get; set; }
            public string? CompanyId { get; set; }
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
            var validation = await this.ValidateCreditNoteAsync(creditNote);
            if (validation != null) return validation;

            await this.EnsureCreditNoteNumberAsync(creditNote);
            NormalizeCreditNote(creditNote);

            var created = await this.storage.InsertCreditNoteAsync(creditNote);
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

            var validation = await this.ValidateCreditNoteAsync(creditNote, id);
            if (validation != null) return validation;

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
                Lines = invoice.Lines.Select((line, index) => new CreditNoteLineEntity
                {
                    ProductKey = line.ProductKey,
                    Description = line.Description,
                    Quantity = line.Quantity,
                    UnitPrice = line.UnitPrice,
                    VatRate = line.VatRate,
                    TotalHT = line.TotalHT,
                    TotalTTC = line.TotalTTC,
                    LineNumber = line.LineNumber > 0 ? line.LineNumber : index + 1
                }).ToList()
            };

            await this.EnsureCreditNoteNumberAsync(creditNote);
            NormalizeCreditNote(creditNote);

            var created = await this.storage.InsertCreditNoteAsync(creditNote);
            return Created(created);
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

            creditNote.Status = "Validated";
            NormalizeCreditNote(creditNote);

            var updated = await this.storage.UpdateCreditNoteAsync(creditNote);
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

            if (creditNote.SalesInvoiceId.HasValue)
            {
                var invoice = await this.storage.SelectSalesInvoiceByIdAsync(creditNote.SalesInvoiceId.Value);
                if (invoice == null) return BadRequest("Linked sales invoice not found");

                if (invoice.CustomerId != creditNote.CustomerId)
                {
                    return BadRequest("Credit note and sales invoice must belong to the same customer.");
                }

                invoice.PaidAmount = Math.Min(invoice.TotalTTC, invoice.PaidAmount + creditNote.TotalTTC);
                invoice.Status = invoice.PaidAmount >= invoice.TotalTTC
                    ? "Paid"
                    : (invoice.PaidAmount > 0 ? "PartiallyPaid" : invoice.Status);

                var applyNote = $"Credit note {creditNote.CreditNoteNumber} applied ({creditNote.TotalTTC:0.##})";
                invoice.Notes = string.IsNullOrWhiteSpace(invoice.Notes)
                    ? applyNote
                    : $"{invoice.Notes}{Environment.NewLine}{applyNote}";

                await this.storage.UpdateSalesInvoiceAsync(invoice);
            }

            var customer = await this.storage.SelectCustomerByIdAsync(creditNote.CustomerId);
            if (customer != null)
            {
                customer.Balance -= creditNote.TotalTTC;
                customer.UpdatedAt = DateTime.UtcNow;
                await this.storage.UpdateCustomerAsync(customer);
            }

            creditNote.Status = "Applied";
            var updated = await this.storage.UpdateCreditNoteAsync(creditNote);
            return Ok(updated);
        }

        private async Task<IActionResult?> ValidateCreditNoteAsync(CreditNoteEntity creditNote, int? currentId = null)
        {
            if (creditNote.CustomerId <= 0) return BadRequest("CustomerId required");

            var customer = await this.storage.SelectCustomerByIdAsync(creditNote.CustomerId);
            if (customer == null) return BadRequest("Customer not found");

            if (creditNote.SalesInvoiceId.HasValue)
            {
                var invoice = await this.storage.SelectSalesInvoiceByIdAsync(creditNote.SalesInvoiceId.Value);
                if (invoice == null) return BadRequest("Linked sales invoice not found");
                if (invoice.CustomerId != creditNote.CustomerId)
                {
                    return BadRequest("Linked sales invoice must belong to the same customer.");
                }
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
