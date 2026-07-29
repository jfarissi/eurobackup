using System;
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
    public class SalesInvoicesController : RESTFulController
    {
        private readonly IStorageBroker storage;
        private readonly INumberingSequenceService numberingService;
        private readonly ICompanyContextService companyContext;

        public SalesInvoicesController(IStorageBroker storage, INumberingSequenceService numberingService, ICompanyContextService companyContext)
        {
            this.storage = storage;
            this.numberingService = numberingService;
            this.companyContext = companyContext;
        }

        [HttpGet]
        [RequirePermission(Permissions.InvoiceRead)]
        public IActionResult GetAll([FromQuery] string? search = null)
        {
            var query = this.storage.SelectAllSalesInvoices().ForCompany(this.companyContext.GetCurrentCompanyId());
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.ToLowerInvariant();
                query = query.Where(i => i.InvoiceNumber.ToLower().Contains(s) || (i.Customer != null && i.Customer.Name.ToLower().Contains(s)));
            }
            return Ok(query.OrderByDescending(i => i.Date).ToList());
        }

        [HttpGet("{id:int}")]
        [RequirePermission(Permissions.InvoiceRead)]
        public async Task<IActionResult> GetById(int id)
        {
            var invoice = await this.storage.SelectSalesInvoiceByIdAsync(id);
            if (invoice == null || !invoice.BelongsToCompany(this.companyContext.GetCurrentCompanyId())) return NotFound();
            return Ok(invoice);
        }

        [HttpPost]
        [RequirePermission(Permissions.InvoiceCreate)]
        public async Task<IActionResult> Post([FromBody] SalesInvoice invoice)
        {
            invoice.EnsureCompanyId(this.companyContext.GetCurrentCompanyId());
            if (string.IsNullOrWhiteSpace(invoice.InvoiceNumber))
            {
                invoice.InvoiceNumber = await this.numberingService.GetNextNumberAsync("Invoice", invoice.CompanyId);
            }
            invoice.Date = invoice.Date == default ? DateTime.UtcNow : invoice.Date;
            invoice.DueDate = invoice.DueDate == default ? DateTime.UtcNow.AddDays(30) : invoice.DueDate;
            invoice.CreatedAt = DateTime.UtcNow;
            
            invoice.TotalHT = invoice.Lines.Sum(l => l.Quantity * l.UnitPrice);
            invoice.TotalVat = invoice.Lines.Sum(l => l.Quantity * l.UnitPrice * (l.VatRate / 100m));
            invoice.TotalTTC = invoice.TotalHT + invoice.TotalVat;

            var created = await this.storage.InsertSalesInvoiceAsync(invoice);
            return Created(created);
        }

        public class PaymentRequest
        {
            public decimal Amount { get; set; }
            public string PaymentMethod { get; set; } = "Cash"; // Cash, Card, BankTransfer
            public string? Notes { get; set; }
        }

        [HttpPost("{id:int}/pay")]
        [RequirePermission(Permissions.InvoiceUpdate)]
        public async Task<IActionResult> RecordPayment(int id, [FromBody] PaymentRequest request)
        {
            var invoice = await this.storage.SelectSalesInvoiceByIdAsync(id);
            if (invoice == null || !invoice.BelongsToCompany(this.companyContext.GetCurrentCompanyId())) return NotFound("Invoice not found");
            if (request.Amount <= 0) return BadRequest("Amount must be positive");

            invoice.PaidAmount += request.Amount;
            if (invoice.PaidAmount >= invoice.TotalTTC)
            {
                invoice.Status = "Paid";
            }
            else
            {
                invoice.Status = "PartiallyPaid";
            }

            var paymentNote = $"Payment {request.Amount:0.##} via {request.PaymentMethod}";
            if (!string.IsNullOrWhiteSpace(request.Notes))
            {
                paymentNote = $"{paymentNote} | {request.Notes.Trim()}";
            }

            invoice.Notes = string.IsNullOrWhiteSpace(invoice.Notes)
                ? paymentNote
                : $"{invoice.Notes}{Environment.NewLine}{paymentNote}";

            var updated = await this.storage.UpdateSalesInvoiceAsync(invoice);

            if (string.Equals(request.PaymentMethod, "Cash", StringComparison.OrdinalIgnoreCase))
            {
                var activeSession = await this.storage.SelectActiveCashSessionAsync(invoice.CompanyId);
                if (activeSession != null)
                {
                    await this.storage.InsertCashOperationAsync(new CashOperation
                    {
                        CashSessionId = activeSession.Id,
                        OperationType = "SalePayment",
                        Amount = request.Amount,
                        Description = $"Invoice {invoice.InvoiceNumber}",
                        ReferenceDocument = invoice.InvoiceNumber,
                        CreatedBy = User.Identity?.Name ?? "System",
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }

            return Ok(updated);
        }
    }
}
