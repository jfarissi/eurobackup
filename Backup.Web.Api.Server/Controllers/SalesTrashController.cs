using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Authorization;
using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Models.Entities;
using Backup.Web.Api.Server.Models.Security;
using Backup.Web.Api.Server.Services.Sales;
using Backup.Web.Api.Server.Services.Tenancy;
using Microsoft.AspNetCore.Mvc;
using RESTFulSense.Controllers;

namespace Backup.Web.Api.Server.Controllers
{
    /// <summary>Corbeille ventes : lecture, restauration Draft (avec contrôle doublon métier), purge définitive.</summary>
    [Microsoft.AspNetCore.Authorization.Authorize]
    [ApiController]
    [Route("api/sales/trash")]
    public class SalesTrashController : RESTFulController
    {
        private readonly IStorageBroker storage;
        private readonly ICompanyContextService companyContext;

        public SalesTrashController(IStorageBroker storage, ICompanyContextService companyContext)
        {
            this.storage = storage;
            this.companyContext = companyContext;
        }

        public class TrashItemDto
        {
            public string DocumentType { get; set; } = string.Empty;
            public int Id { get; set; }
            public string Number { get; set; } = string.Empty;
            public string? CustomerName { get; set; }
            public int CustomerId { get; set; }
            public string Status { get; set; } = string.Empty;
            public decimal TotalTTC { get; set; }
            public DateTime? DeletedAt { get; set; }
            public string? DeletedBy { get; set; }
            public bool CanRestore { get; set; }
            public bool CanPurge { get; set; }
            public string? RestoreBlockedReason { get; set; }
        }

        [HttpGet]
        [RequireAnyPermission(Permissions.InvoiceRead, Permissions.OrderRead, Permissions.DeliveryNoteRead, Permissions.QuoteRead)]
        public IActionResult GetAll([FromQuery] string? search = null)
        {
            var companyId = this.companyContext.GetCurrentCompanyId();
            var s = string.IsNullOrWhiteSpace(search) ? null : search.Trim().ToLowerInvariant();
            var items = new List<TrashItemDto>();

            // Materialiser avant les contrôles (évite MySqlConnection already in use / MARS).
            var deletedInvoices = this.storage.SelectDeletedSalesInvoices().ForCompany(companyId).ToList();
            var deletedOrders = this.storage.SelectDeletedSalesOrders().ForCompany(companyId).ToList();
            var deletedNotes = this.storage.SelectDeletedSalesDeliveryNotes().ForCompany(companyId).ToList();
            var deletedQuotes = this.storage.SelectDeletedQuotes().ForCompany(companyId).ToList();

            foreach (var inv in deletedInvoices)
            {
                if (s != null
                    && !(inv.InvoiceNumber?.ToLowerInvariant().Contains(s) == true
                        || inv.Customer?.Name?.ToLowerInvariant().Contains(s) == true))
                    continue;
                var block = GetInvoiceRestoreBlockReason(inv);
                items.Add(ToDto("Invoice", inv.Id, inv.InvoiceNumber, inv.CustomerId, inv.Customer?.Name,
                    inv.Status, inv.TotalTTC, inv.DeletedAt, inv.DeletedBy, block));
            }

            foreach (var order in deletedOrders)
            {
                if (s != null
                    && !(order.OrderNumber?.ToLowerInvariant().Contains(s) == true
                        || order.Customer?.Name?.ToLowerInvariant().Contains(s) == true))
                    continue;
                var block = GetOrderRestoreBlockReason(order);
                items.Add(ToDto("Order", order.Id, order.OrderNumber, order.CustomerId, order.Customer?.Name,
                    order.Status, order.TotalTTC, order.DeletedAt, order.DeletedBy, block));
            }

            foreach (var note in deletedNotes)
            {
                if (s != null
                    && !(note.DeliveryNumber?.ToLowerInvariant().Contains(s) == true
                        || note.Customer?.Name?.ToLowerInvariant().Contains(s) == true))
                    continue;
                var block = GetDeliveryNoteRestoreBlockReason(note);
                items.Add(ToDto("DeliveryNote", note.Id, note.DeliveryNumber, note.CustomerId, note.Customer?.Name,
                    note.Status, note.TotalTTC, note.DeletedAt, note.DeletedBy, block));
            }

            foreach (var quote in deletedQuotes)
            {
                if (s != null
                    && !(quote.QuoteNumber?.ToLowerInvariant().Contains(s) == true
                        || quote.Customer?.Name?.ToLowerInvariant().Contains(s) == true))
                    continue;
                var block = GetQuoteRestoreBlockReason(quote);
                items.Add(ToDto("Quote", quote.Id, quote.QuoteNumber, quote.CustomerId, quote.Customer?.Name,
                    quote.Status, quote.TotalTTC, quote.DeletedAt, quote.DeletedBy, block));
            }

            return Ok(items
                .OrderByDescending(i => i.DeletedAt ?? DateTime.MinValue)
                .ThenByDescending(i => i.Id)
                .ToList());
        }

        [HttpPost("{documentType}/{id:int}/restore")]
        [RequireAnyPermission(
            Permissions.InvoiceUpdate,
            Permissions.OrderUpdate,
            Permissions.DeliveryNoteCreate,
            Permissions.QuoteUpdate)]
        public async Task<IActionResult> Restore(string documentType, int id)
        {
            var companyId = this.companyContext.GetCurrentCompanyId();
            var type = (documentType ?? string.Empty).Trim();
            var actor = SalesDocumentAudit.ActorFrom(User);

            if (string.Equals(type, "Invoice", StringComparison.OrdinalIgnoreCase))
                return await RestoreInvoiceAsync(id, companyId, actor);
            if (string.Equals(type, "Order", StringComparison.OrdinalIgnoreCase))
                return await RestoreOrderAsync(id, companyId, actor);
            if (string.Equals(type, "DeliveryNote", StringComparison.OrdinalIgnoreCase))
                return await RestoreDeliveryNoteAsync(id, companyId, actor);
            if (string.Equals(type, "Quote", StringComparison.OrdinalIgnoreCase))
                return await RestoreQuoteAsync(id, companyId, actor);

            return BadRequest("Type de document non supporté (Invoice, Order, DeliveryNote, Quote).");
        }

        /// <summary>Purge définitive d'un document soft-supprimé (Draft uniquement).</summary>
        [HttpDelete("{documentType}/{id:int}")]
        [RequireAnyPermission(
            Permissions.InvoiceDelete,
            Permissions.OrderDelete,
            Permissions.DeliveryNoteDelete,
            Permissions.QuoteDelete)]
        public async Task<IActionResult> Purge(string documentType, int id)
        {
            var companyId = this.companyContext.GetCurrentCompanyId();
            var type = (documentType ?? string.Empty).Trim();
            var actor = SalesDocumentAudit.ActorFrom(User);

            if (string.Equals(type, "Invoice", StringComparison.OrdinalIgnoreCase))
                return await PurgeInvoiceAsync(id, companyId, actor);
            if (string.Equals(type, "Order", StringComparison.OrdinalIgnoreCase))
                return await PurgeOrderAsync(id, companyId, actor);
            if (string.Equals(type, "DeliveryNote", StringComparison.OrdinalIgnoreCase))
                return await PurgeDeliveryNoteAsync(id, companyId, actor);
            if (string.Equals(type, "Quote", StringComparison.OrdinalIgnoreCase))
                return await PurgeQuoteAsync(id, companyId, actor);

            return BadRequest("Type de document non supporté (Invoice, Order, DeliveryNote, Quote).");
        }

        /// <summary>Vide la corbeille : purge physique de tous les Draft soft-supprimés de la société.</summary>
        [HttpDelete]
        [RequireAnyPermission(
            Permissions.InvoiceDelete,
            Permissions.OrderDelete,
            Permissions.DeliveryNoteDelete,
            Permissions.QuoteDelete)]
        public async Task<IActionResult> EmptyTrash()
        {
            var companyId = this.companyContext.GetCurrentCompanyId();
            var actor = SalesDocumentAudit.ActorFrom(User);
            var purged = 0;

            foreach (var inv in this.storage.SelectDeletedSalesInvoices().ForCompany(companyId).ToList())
            {
                if (!SalesBusinessRules.CanPhysicallyDelete(inv.Status)) continue;
                await SalesDocumentAudit.LogAsync(
                    this.storage, inv.CompanyId ?? companyId, "Invoice", inv.Id,
                    "Purged", actor, $"Purge définitive facture {inv.InvoiceNumber}");
                await this.storage.PurgeSalesInvoiceAsync(inv);
                purged++;
            }

            foreach (var order in this.storage.SelectDeletedSalesOrders().ForCompany(companyId).ToList())
            {
                if (!SalesBusinessRules.CanPhysicallyDelete(order.Status)) continue;
                await SalesDocumentAudit.LogAsync(
                    this.storage, order.CompanyId ?? companyId, "Order", order.Id,
                    "Purged", actor, $"Purge définitive commande {order.OrderNumber}");
                await this.storage.PurgeSalesOrderAsync(order);
                purged++;
            }

            foreach (var note in this.storage.SelectDeletedSalesDeliveryNotes().ForCompany(companyId).ToList())
            {
                if (!SalesBusinessRules.CanPhysicallyDelete(note.Status)) continue;
                await SalesDocumentAudit.LogAsync(
                    this.storage, note.CompanyId ?? companyId, "DeliveryNote", note.Id,
                    "Purged", actor, $"Purge définitive BL {note.DeliveryNumber}");
                await this.storage.PurgeSalesDeliveryNoteAsync(note);
                purged++;
            }

            foreach (var quote in this.storage.SelectDeletedQuotes().ForCompany(companyId).ToList())
            {
                if (!SalesBusinessRules.CanPhysicallyDelete(quote.Status)) continue;
                await SalesDocumentAudit.LogAsync(
                    this.storage, quote.CompanyId ?? companyId, "Quote", quote.Id,
                    "Purged", actor, $"Purge définitive devis {quote.QuoteNumber}");
                await this.storage.PurgeQuoteAsync(quote);
                purged++;
            }

            return Ok(new { purged });
        }

        private static TrashItemDto ToDto(
            string type, int id, string number, int customerId, string? customerName,
            string status, decimal totalTtc, DateTime? deletedAt, string? deletedBy, string? blockReason)
        {
            var canStatus = SalesBusinessRules.CanRestoreSoftDeleted(status);
            return new TrashItemDto
            {
                DocumentType = type,
                Id = id,
                Number = number,
                CustomerId = customerId,
                CustomerName = customerName,
                Status = status,
                TotalTTC = totalTtc,
                DeletedAt = deletedAt,
                DeletedBy = deletedBy,
                RestoreBlockedReason = canStatus ? blockReason : "Seuls les brouillons peuvent être restaurés.",
                CanRestore = canStatus && string.IsNullOrWhiteSpace(blockReason),
                CanPurge = SalesBusinessRules.CanPhysicallyDelete(status)
            };
        }

        private string? GetInvoiceRestoreBlockReason(SalesInvoice invoice)
        {
            var number = (invoice.InvoiceNumber ?? string.Empty).ToLowerInvariant();
            var numberTaken = this.storage.SelectAllSalesInvoices()
                .Any(i => i.Id != invoice.Id && i.InvoiceNumber.ToLower() == number);
            if (numberTaken)
                return $"Le numéro {invoice.InvoiceNumber} est déjà utilisé par une facture active.";

            if (invoice.SalesOrderId.HasValue)
            {
                var siblings = this.storage.SelectAllSalesInvoices()
                    .Where(i => i.Id != invoice.Id && i.SalesOrderId == invoice.SalesOrderId)
                    .Select(i => i.InvoiceNumber)
                    .ToList();
                if (siblings.Count > 0)
                    return $"Doublon métier : la commande a déjà une facture active ({string.Join(", ", siblings)}).";

                var order = this.storage.SelectAllSalesOrders()
                    .FirstOrDefault(o => o.Id == invoice.SalesOrderId.Value);
                if (order?.Lines != null)
                {
                    foreach (var line in invoice.Lines ?? new List<SalesInvoiceLine>())
                    {
                        var key = line.ProductKey?.Trim();
                        var orderLine = order.Lines.FirstOrDefault(l =>
                            string.Equals(l.ProductKey?.Trim(), key, StringComparison.OrdinalIgnoreCase));
                        if (orderLine == null) continue;
                        var remaining = orderLine.Quantity - orderLine.InvoicedQuantity;
                        if (line.Quantity > remaining + 0.0001m)
                            return $"Doublon métier : qté déjà facturée insuffisante pour '{line.ProductKey}' (reste {remaining:0.####}).";
                    }
                }
            }

            return null;
        }

        private string? GetOrderRestoreBlockReason(SalesOrder order)
        {
            var number = (order.OrderNumber ?? string.Empty).ToLowerInvariant();
            var numberTaken = this.storage.SelectAllSalesOrders()
                .Any(o => o.Id != order.Id && o.OrderNumber.ToLower() == number);
            return numberTaken
                ? $"Le numéro {order.OrderNumber} est déjà utilisé par une commande active."
                : null;
        }

        private string? GetDeliveryNoteRestoreBlockReason(SalesDeliveryNote note)
        {
            var number = (note.DeliveryNumber ?? string.Empty).ToLowerInvariant();
            var numberTaken = this.storage.SelectAllSalesDeliveryNotes()
                .Any(n => n.Id != note.Id && n.DeliveryNumber.ToLower() == number);
            if (numberTaken)
                return $"Le numéro {note.DeliveryNumber} est déjà utilisé par un BL actif.";

            if (note.SalesOrderId.HasValue)
            {
                var openDn = this.storage.SelectAllSalesDeliveryNotes()
                    .Where(n => n.Id != note.Id
                        && n.SalesOrderId == note.SalesOrderId
                        && n.Status.ToLower() != "cancelled")
                    .Select(n => n.DeliveryNumber)
                    .ToList();
                if (openDn.Count > 0)
                    return $"Doublon métier : un BL actif existe déjà pour cette commande ({string.Join(", ", openDn)}).";
            }

            return null;
        }

        private string? GetQuoteRestoreBlockReason(Quote quote)
        {
            var number = (quote.QuoteNumber ?? string.Empty).ToLowerInvariant();
            var numberTaken = this.storage.SelectAllQuotes()
                .Any(q => q.Id != quote.Id && q.QuoteNumber.ToLower() == number);
            return numberTaken
                ? $"Le numéro {quote.QuoteNumber} est déjà utilisé par un devis actif."
                : null;
        }

        private async Task<IActionResult> RestoreInvoiceAsync(int id, string? companyId, string actor)
        {
            var invoice = await this.storage.SelectSalesInvoiceByIdIncludingDeletedAsync(id);
            if (invoice == null || !invoice.BelongsToCompany(companyId) || !invoice.IsDeleted)
                return NotFound();
            if (!SalesBusinessRules.CanRestoreSoftDeleted(invoice.Status))
                return BadRequest("Seuls les brouillons peuvent être restaurés.");

            var block = GetInvoiceRestoreBlockReason(invoice);
            if (block != null)
                return Conflict(new { error = block });

            SalesBusinessRules.RestoreSoftDelete(invoice);
            await this.storage.UpdateSalesInvoiceAsync(invoice);

            if (invoice.SalesOrderId.HasValue)
            {
                var order = await this.storage.SelectSalesOrderByIdAsync(invoice.SalesOrderId.Value);
                if (order != null)
                {
                    var deltas = invoice.Lines
                        .Select(l => (l.ProductKey, Delta: l.Quantity))
                        .ToList();
                    SalesBusinessRules.AdjustInvoicedQuantities(order, deltas);
                    await this.storage.UpdateSalesOrderAsync(order);
                }
            }

            await SalesDocumentAudit.LogAsync(
                this.storage, invoice.CompanyId ?? companyId, "Invoice", invoice.Id,
                "Restored", actor, $"Restauration facture {invoice.InvoiceNumber}");

            return Ok(invoice);
        }

        private async Task<IActionResult> RestoreOrderAsync(int id, string? companyId, string actor)
        {
            var order = await this.storage.SelectSalesOrderByIdIncludingDeletedAsync(id);
            if (order == null || !order.BelongsToCompany(companyId) || !order.IsDeleted)
                return NotFound();
            if (!SalesBusinessRules.CanRestoreSoftDeleted(order.Status))
                return BadRequest("Seuls les brouillons peuvent être restaurés.");

            var block = GetOrderRestoreBlockReason(order);
            if (block != null)
                return Conflict(new { error = block });

            SalesBusinessRules.RestoreSoftDelete(order);
            await this.storage.UpdateSalesOrderAsync(order);
            await SalesDocumentAudit.LogAsync(
                this.storage, order.CompanyId ?? companyId, "Order", order.Id,
                "Restored", actor, $"Restauration commande {order.OrderNumber}");
            return Ok(order);
        }

        private async Task<IActionResult> RestoreDeliveryNoteAsync(int id, string? companyId, string actor)
        {
            var note = await this.storage.SelectSalesDeliveryNoteByIdIncludingDeletedAsync(id);
            if (note == null || !note.BelongsToCompany(companyId) || !note.IsDeleted)
                return NotFound();
            if (!SalesBusinessRules.CanRestoreSoftDeleted(note.Status))
                return BadRequest("Seuls les brouillons peuvent être restaurés.");

            var block = GetDeliveryNoteRestoreBlockReason(note);
            if (block != null)
                return Conflict(new { error = block });

            SalesBusinessRules.RestoreSoftDelete(note);
            await this.storage.UpdateSalesDeliveryNoteAsync(note);
            await SalesDocumentAudit.LogAsync(
                this.storage, note.CompanyId ?? companyId, "DeliveryNote", note.Id,
                "Restored", actor, $"Restauration BL {note.DeliveryNumber}");
            return Ok(note);
        }

        private async Task<IActionResult> RestoreQuoteAsync(int id, string? companyId, string actor)
        {
            var quote = await this.storage.SelectQuoteByIdIncludingDeletedAsync(id);
            if (quote == null || !quote.BelongsToCompany(companyId) || !quote.IsDeleted)
                return NotFound();
            if (!SalesBusinessRules.CanRestoreSoftDeleted(quote.Status))
                return BadRequest("Seuls les brouillons peuvent être restaurés.");

            var block = GetQuoteRestoreBlockReason(quote);
            if (block != null)
                return Conflict(new { error = block });

            SalesBusinessRules.RestoreSoftDelete(quote);
            await this.storage.UpdateQuoteAsync(quote);
            await SalesDocumentAudit.LogAsync(
                this.storage, quote.CompanyId ?? companyId, "Quote", quote.Id,
                "Restored", actor, $"Restauration devis {quote.QuoteNumber}");
            return Ok(quote);
        }

        private async Task<IActionResult> PurgeInvoiceAsync(int id, string? companyId, string actor)
        {
            var invoice = await this.storage.SelectSalesInvoiceByIdIncludingDeletedAsync(id);
            if (invoice == null || !invoice.BelongsToCompany(companyId) || !invoice.IsDeleted)
                return NotFound();
            if (!SalesBusinessRules.CanPhysicallyDelete(invoice.Status))
                return BadRequest("Seuls les brouillons peuvent être purgés définitivement.");

            await SalesDocumentAudit.LogAsync(
                this.storage, invoice.CompanyId ?? companyId, "Invoice", invoice.Id,
                "Purged", actor, $"Purge définitive facture {invoice.InvoiceNumber}");
            await this.storage.PurgeSalesInvoiceAsync(invoice);
            return NoContent();
        }

        private async Task<IActionResult> PurgeOrderAsync(int id, string? companyId, string actor)
        {
            var order = await this.storage.SelectSalesOrderByIdIncludingDeletedAsync(id);
            if (order == null || !order.BelongsToCompany(companyId) || !order.IsDeleted)
                return NotFound();
            if (!SalesBusinessRules.CanPhysicallyDelete(order.Status))
                return BadRequest("Seuls les brouillons peuvent être purgés définitivement.");

            await SalesDocumentAudit.LogAsync(
                this.storage, order.CompanyId ?? companyId, "Order", order.Id,
                "Purged", actor, $"Purge définitive commande {order.OrderNumber}");
            await this.storage.PurgeSalesOrderAsync(order);
            return NoContent();
        }

        private async Task<IActionResult> PurgeDeliveryNoteAsync(int id, string? companyId, string actor)
        {
            var note = await this.storage.SelectSalesDeliveryNoteByIdIncludingDeletedAsync(id);
            if (note == null || !note.BelongsToCompany(companyId) || !note.IsDeleted)
                return NotFound();
            if (!SalesBusinessRules.CanPhysicallyDelete(note.Status))
                return BadRequest("Seuls les brouillons peuvent être purgés définitivement.");

            await SalesDocumentAudit.LogAsync(
                this.storage, note.CompanyId ?? companyId, "DeliveryNote", note.Id,
                "Purged", actor, $"Purge définitive BL {note.DeliveryNumber}");
            await this.storage.PurgeSalesDeliveryNoteAsync(note);
            return NoContent();
        }

        private async Task<IActionResult> PurgeQuoteAsync(int id, string? companyId, string actor)
        {
            var quote = await this.storage.SelectQuoteByIdIncludingDeletedAsync(id);
            if (quote == null || !quote.BelongsToCompany(companyId) || !quote.IsDeleted)
                return NotFound();
            if (!SalesBusinessRules.CanPhysicallyDelete(quote.Status))
                return BadRequest("Seuls les brouillons peuvent être purgés définitivement.");

            await SalesDocumentAudit.LogAsync(
                this.storage, quote.CompanyId ?? companyId, "Quote", quote.Id,
                "Purged", actor, $"Purge définitive devis {quote.QuoteNumber}");
            await this.storage.PurgeQuoteAsync(quote);
            return NoContent();
        }
    }
}
