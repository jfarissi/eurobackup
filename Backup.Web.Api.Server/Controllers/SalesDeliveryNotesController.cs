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
    public class SalesDeliveryNotesController : RESTFulController
    {
        private readonly IStorageBroker storage;
        private readonly INumberingSequenceService numberingService;
        private readonly ICompanyContextService companyContext;

        public SalesDeliveryNotesController(IStorageBroker storage, INumberingSequenceService numberingService, ICompanyContextService companyContext)
        {
            this.storage = storage;
            this.numberingService = numberingService;
            this.companyContext = companyContext;
        }

        [HttpGet]
        [RequirePermission(Permissions.DeliveryNoteRead)]
        public IActionResult GetAll([FromQuery] string? search = null, [FromQuery] int? salesOrderId = null)
        {
            var query = this.storage.SelectAllSalesDeliveryNotes().ForCompany(this.companyContext.GetCurrentCompanyId());
            if (salesOrderId.HasValue)
                query = query.Where(n => n.SalesOrderId == salesOrderId.Value);
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.ToLowerInvariant();
                query = query.Where(n => n.DeliveryNumber.ToLower().Contains(s)
                    || (n.Customer != null && n.Customer.Name.ToLower().Contains(s)));
            }
            return Ok(query.OrderByDescending(n => n.DeliveryDate).ToList());
        }

        [HttpGet("{id:int}")]
        [RequirePermission(Permissions.DeliveryNoteRead)]
        public async Task<IActionResult> GetById(int id)
        {
            var note = await this.storage.SelectSalesDeliveryNoteByIdAsync(id);
            if (note == null || !note.BelongsToCompany(this.companyContext.GetCurrentCompanyId())) return NotFound();
            return Ok(note);
        }

        [HttpPost]
        [RequirePermission(Permissions.DeliveryNoteCreate)]
        public async Task<IActionResult> Post([FromBody] SalesDeliveryNote note)
        {
            note.EnsureCompanyId(this.companyContext.GetCurrentCompanyId());
            if (string.IsNullOrWhiteSpace(note.DeliveryNumber))
                note.DeliveryNumber = await this.numberingService.GetNextNumberAsync("SalesDeliveryNote", note.CompanyId);
            note.DeliveryDate = note.DeliveryDate == default ? DateTime.UtcNow : note.DeliveryDate;
            note.CreatedAt = DateTime.UtcNow;
            RecalcTotals(note);

            // Sync delivered quantities back to linked sales order lines
            if (note.SalesOrderId.HasValue)
                await SyncOrderDelivered(note.SalesOrderId.Value, note.Lines.Select(l => (l.ProductKey, l.DeliveredQuantity)).ToList());

            var created = await this.storage.InsertSalesDeliveryNoteAsync(note);
            return Created(created);
        }

        [HttpPut("{id:int}")]
        [RequirePermission(Permissions.DeliveryNoteCreate)]
        public async Task<IActionResult> Put(int id, [FromBody] SalesDeliveryNote note)
        {
            var existing = await this.storage.SelectSalesDeliveryNoteByIdAsync(id);
            if (existing == null || !existing.BelongsToCompany(this.companyContext.GetCurrentCompanyId())) return NotFound();

            existing.CustomerId = note.CustomerId;
            existing.SalesOrderId = note.SalesOrderId;
            existing.DeliveryDate = note.DeliveryDate;
            existing.Status = note.Status;
            existing.DeliveryAddress = note.DeliveryAddress;
            existing.Notes = note.Notes;
            existing.Lines = note.Lines;
            RecalcTotals(existing);

            var updated = await this.storage.UpdateSalesDeliveryNoteAsync(existing);
            return Ok(updated);
        }

        [HttpDelete("{id:int}")]
        [RequirePermission(Permissions.DeliveryNoteDelete)]
        public async Task<IActionResult> Delete(int id)
        {
            var existing = await this.storage.SelectSalesDeliveryNoteByIdAsync(id);
            if (existing == null || !existing.BelongsToCompany(this.companyContext.GetCurrentCompanyId())) return NotFound();
            await this.storage.DeleteSalesDeliveryNoteAsync(existing);
            return NoContent();
        }

        /// <summary>Génère une facture depuis ce bon de livraison.</summary>
        [HttpPost("{id:int}/convert-to-invoice")]
        [RequirePermission(Permissions.InvoiceCreate)]
        public async Task<IActionResult> ConvertToInvoice(int id)
        {
            var note = await this.storage.SelectSalesDeliveryNoteByIdAsync(id);
            if (note == null || !note.BelongsToCompany(this.companyContext.GetCurrentCompanyId()))
                return NotFound("Bon de livraison introuvable.");
            if (note.SalesInvoiceId.HasValue)
                return Conflict(new { error = $"Ce BL a déjà été facturé (facture #{note.SalesInvoiceId})." });

            var invoice = new SalesInvoice
            {
                InvoiceNumber = await this.numberingService.GetNextNumberAsync("Invoice", note.CompanyId),
                CustomerId = note.CustomerId,
                Date = DateTime.UtcNow,
                DueDate = DateTime.UtcNow.AddDays(30),
                Status = "Draft",
                TotalHT = note.TotalHT,
                TotalVat = note.TotalVat,
                TotalTTC = note.TotalTTC,
                Notes = $"Facture générée depuis BL {note.DeliveryNumber}",
                CompanyId = note.CompanyId ?? this.companyContext.GetCurrentCompanyId(),
                CreatedAt = DateTime.UtcNow,
                Lines = note.Lines.Select((l, i) => new SalesInvoiceLine
                {
                    ProductKey = l.ProductKey,
                    Description = l.Description,
                    Quantity = l.DeliveredQuantity,
                    UnitPrice = l.UnitPrice,
                    VatRate = l.VatRate,
                    TotalHT = l.TotalHT,
                    TotalTTC = l.TotalTTC,
                    LineNumber = i + 1
                }).ToList()
            };
            var createdInvoice = await this.storage.InsertSalesInvoiceAsync(invoice);

            note.SalesInvoiceId = createdInvoice.Id;
            note.Status = "Invoiced";
            await this.storage.UpdateSalesDeliveryNoteAsync(note);

            // Mark linked order as Invoiced
            if (note.SalesOrderId.HasValue)
            {
                var order = await this.storage.SelectSalesOrderByIdAsync(note.SalesOrderId.Value);
                if (order != null && order.Status != "Invoiced")
                {
                    order.Status = "Invoiced";
                    await this.storage.UpdateSalesOrderAsync(order);
                }
            }

            return Ok(createdInvoice);
        }

        /// <summary>Génère un BL depuis une commande client existante.</summary>
        [HttpPost("from-order/{salesOrderId:int}")]
        [RequirePermission(Permissions.DeliveryNoteCreate)]
        public async Task<IActionResult> CreateFromOrder(int salesOrderId)
        {
            var order = await this.storage.SelectSalesOrderByIdAsync(salesOrderId);
            if (order == null || !order.BelongsToCompany(this.companyContext.GetCurrentCompanyId()))
                return NotFound("Commande client introuvable.");

            var note = new SalesDeliveryNote
            {
                DeliveryNumber = await this.numberingService.GetNextNumberAsync("SalesDeliveryNote", order.CompanyId),
                CustomerId = order.CustomerId,
                SalesOrderId = order.Id,
                DeliveryDate = DateTime.UtcNow,
                Status = "Draft",
                TotalHT = order.TotalHT,
                TotalVat = order.TotalVat,
                TotalTTC = order.TotalTTC,
                Notes = $"BL généré depuis commande {order.OrderNumber}",
                CompanyId = order.CompanyId ?? this.companyContext.GetCurrentCompanyId(),
                CreatedAt = DateTime.UtcNow,
                Lines = order.Lines.Select((l, i) => new SalesDeliveryNoteLine
                {
                    ProductKey = l.ProductKey,
                    Description = l.Description,
                    OrderedQuantity = l.Quantity,
                    DeliveredQuantity = l.Quantity,
                    UnitPrice = l.UnitPrice,
                    VatRate = l.VatRate,
                    TotalHT = l.TotalHT,
                    TotalTTC = l.TotalTTC,
                    LineNumber = i + 1
                }).ToList()
            };
            RecalcTotals(note);

            order.Status = "Delivered";
            await this.storage.UpdateSalesOrderAsync(order);

            var created = await this.storage.InsertSalesDeliveryNoteAsync(note);
            return Created(created);
        }

        private static void RecalcTotals(SalesDeliveryNote note)
        {
            note.TotalHT = note.Lines.Sum(l => l.DeliveredQuantity * l.UnitPrice);
            note.TotalVat = note.Lines.Sum(l => l.DeliveredQuantity * l.UnitPrice * (l.VatRate / 100m));
            note.TotalTTC = note.TotalHT + note.TotalVat;
            foreach (var l in note.Lines)
            {
                l.TotalHT = l.DeliveredQuantity * l.UnitPrice;
                l.TotalTTC = l.TotalHT * (1 + l.VatRate / 100m);
            }
        }

        private async Task SyncOrderDelivered(int salesOrderId, System.Collections.Generic.List<(string ProductKey, decimal Qty)> deliveries)
        {
            var order = await this.storage.SelectSalesOrderByIdAsync(salesOrderId);
            if (order == null) return;
            foreach (var (key, qty) in deliveries)
            {
                var line = order.Lines.FirstOrDefault(l => l.ProductKey == key);
                if (line != null) line.DeliveredQuantity += qty;
            }
            var total = order.Lines.Sum(l => l.Quantity);
            var delivered = order.Lines.Sum(l => l.DeliveredQuantity);
            order.Status = delivered >= total ? "Delivered" : "PartiallyDelivered";
            await this.storage.UpdateSalesOrderAsync(order);
        }
    }
}
