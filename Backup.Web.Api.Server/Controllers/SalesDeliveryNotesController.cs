using System;
using System.Linq;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Authorization;
using Authorize = Microsoft.AspNetCore.Authorization.AuthorizeAttribute;
using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Models.Entities;
using Backup.Web.Api.Server.Models.Security;
using Backup.Web.Api.Server.Services.Numbering;
using Backup.Web.Api.Server.Services.Sales;
using Backup.Web.Api.Server.Services.Stock;
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
            // RG-BL1 : BL obligatoirement lié à une commande.
            if (!note.SalesOrderId.HasValue || note.SalesOrderId.Value <= 0)
                return BadRequest("Un bon de livraison doit être lié à une commande confirmée (parcours Commande → BL).");

            var order = await this.storage.SelectSalesOrderByIdAsync(note.SalesOrderId.Value);
            if (order == null || !order.BelongsToCompany(this.companyContext.GetCurrentCompanyId()))
                return BadRequest("Commande liée introuvable.");
            var confirmErr = SalesBusinessRules.RejectIfOrderNotConfirmedForDelivery(order.Status);
            if (confirmErr != null) return BadRequest(confirmErr);

            var parentErr = SalesBusinessRules.RejectIfParentUnusable(order.Status, $"commande {order.OrderNumber}");
            if (parentErr != null) return BadRequest(parentErr);

            var customerError = SalesBusinessRules.ValidateSameCustomer(order.CustomerId, note.CustomerId, "commande → BL");
            if (customerError != null) return BadRequest(customerError);

            if (string.IsNullOrWhiteSpace(note.DeliveryNumber))
                note.DeliveryNumber = await this.numberingService.GetNextNumberAsync("SalesDeliveryNote", note.CompanyId);
            note.DeliveryDate = note.DeliveryDate == default ? DateTime.UtcNow : note.DeliveryDate;
            note.CreatedAt = DateTime.UtcNow;
            if (string.IsNullOrWhiteSpace(note.Status)) note.Status = "Draft";
            note.CustomerId = order.CustomerId;
            RecalcTotals(note);

            // RG-V5 : stock + qté livrée commande uniquement à la validation
            var created = await this.storage.InsertSalesDeliveryNoteAsync(note);
            await SalesDocumentAudit.LogAsync(
                this.storage,
                note.CompanyId ?? this.companyContext.GetCurrentCompanyId(),
                "DeliveryNote",
                created.Id,
                "Created",
                SalesDocumentAudit.ActorFrom(User),
                $"Création BL {created.DeliveryNumber}");
            return Created(created);
        }

        [HttpPut("{id:int}")]
        [RequirePermission(Permissions.DeliveryNoteCreate)]
        public async Task<IActionResult> Put(int id, [FromBody] SalesDeliveryNote note)
        {
            var existing = await this.storage.SelectSalesDeliveryNoteByIdAsync(id);
            if (existing == null || !existing.BelongsToCompany(this.companyContext.GetCurrentCompanyId())) return NotFound();
            if (!string.Equals(existing.Status, "Draft", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(existing.Status))
            {
                return BadRequest("Seuls les BL brouillon peuvent être entièrement modifiés.");
            }

            if (!note.SalesOrderId.HasValue || note.SalesOrderId.Value <= 0)
                return BadRequest("Un bon de livraison doit rester lié à une commande.");

            var order = await this.storage.SelectSalesOrderByIdAsync(note.SalesOrderId.Value);
            if (order == null) return BadRequest("Commande liée introuvable.");
            var customerError = SalesBusinessRules.ValidateSameCustomer(order.CustomerId, note.CustomerId, "commande → BL");
            if (customerError != null) return BadRequest(customerError);

            existing.CustomerId = order.CustomerId;
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
            if (!SalesBusinessRules.CanPhysicallyDelete(existing.Status))
                return BadRequest("Seuls les brouillons peuvent être supprimés. Utilisez l'annulation pour les documents validés.");

            var actor = SalesDocumentAudit.ActorFrom(User);
            SalesBusinessRules.SoftDelete(existing, actor);
            await this.storage.DeleteSalesDeliveryNoteAsync(existing);
            await SalesDocumentAudit.LogAsync(
                this.storage,
                existing.CompanyId ?? this.companyContext.GetCurrentCompanyId(),
                "DeliveryNote",
                existing.Id,
                "Deleted",
                actor,
                $"Suppression soft BL {existing.DeliveryNumber}");
            return NoContent();
        }

        [HttpPost("{id:int}/cancel")]
        [RequirePermission(Permissions.DeliveryNoteCreate)]
        public async Task<IActionResult> Cancel(int id, [FromBody] CancelRequest? request)
        {
            var note = await this.storage.SelectSalesDeliveryNoteByIdAsync(id);
            if (note == null || !note.BelongsToCompany(this.companyContext.GetCurrentCompanyId())) return NotFound();
            if (string.Equals(note.Status, "Cancelled", StringComparison.OrdinalIgnoreCase))
                return Conflict(new { error = "BL déjà annulé." });
            if (note.SalesInvoiceId.HasValue || string.Equals(note.Status, "Invoiced", StringComparison.OrdinalIgnoreCase))
                return BadRequest("Un BL déjà facturé ne peut pas être annulé.");

            if (string.IsNullOrWhiteSpace(request?.Reason))
                return BadRequest("Motif d'annulation obligatoire.");

            var wasDelivered = string.Equals(note.Status, "Delivered", StringComparison.OrdinalIgnoreCase);
            if (wasDelivered)
            {
                await ReverseStockOutAsync(note);
                if (note.SalesOrderId.HasValue)
                    await SyncOrderDelivered(note.SalesOrderId.Value, note.Lines.Select(l => (l.ProductKey, -l.DeliveredQuantity)).ToList());
            }

            note.Status = "Cancelled";
            var motif = request!.Reason!.Trim();
            note.Notes = string.IsNullOrWhiteSpace(note.Notes) ? $"Annulation : {motif}" : $"{note.Notes}\nAnnulation : {motif}";
            await this.storage.UpdateSalesDeliveryNoteAsync(note);
            await SalesDocumentAudit.LogAsync(
                this.storage,
                note.CompanyId ?? this.companyContext.GetCurrentCompanyId(),
                "DeliveryNote",
                note.Id,
                "Cancelled",
                SalesDocumentAudit.ActorFrom(User),
                $"Annulation BL {note.DeliveryNumber}",
                motif);
            return Ok(note);
        }

        /// <summary>RG-V5 : valide le BL → sortie stock + maj qté livrée commande.</summary>
        [HttpPost("{id:int}/validate")]
        [RequirePermission(Permissions.DeliveryNoteCreate)]
        public async Task<IActionResult> Validate(int id)
        {
            var note = await this.storage.SelectSalesDeliveryNoteByIdAsync(id);
            if (note == null || !note.BelongsToCompany(this.companyContext.GetCurrentCompanyId())) return NotFound();
            if (string.Equals(note.Status, "Delivered", StringComparison.OrdinalIgnoreCase)
                || string.Equals(note.Status, "Invoiced", StringComparison.OrdinalIgnoreCase))
                return Conflict(new { error = $"BL déjà {note.Status}." });
            if (string.Equals(note.Status, "Cancelled", StringComparison.OrdinalIgnoreCase))
                return BadRequest("Un BL annulé ne peut pas être validé.");
            if (note.Lines == null || note.Lines.Count == 0)
                return BadRequest("Le BL doit contenir au moins une ligne.");

            var companyId = note.CompanyId ?? this.companyContext.GetCurrentCompanyId();
            if (!note.Lines.Any(l => l.DeliveredQuantity > 0))
                return BadRequest("Aucune quantité à livrer : ajustez les quantités livrées (stock insuffisant ou qté à 0).");

            SalesOrder? linkedOrder = null;
            if (note.SalesOrderId.HasValue)
                linkedOrder = await this.storage.SelectSalesOrderByIdAsync(note.SalesOrderId.Value);

            foreach (var line in note.Lines.Where(l => l.DeliveredQuantity > 0))
            {
                var orderLine = linkedOrder?.Lines?.FirstOrDefault(ol =>
                    string.Equals(ol.ProductKey?.Trim(), line.ProductKey?.Trim(), StringComparison.OrdinalIgnoreCase));
                var ownReserved = orderLine?.ReservedQuantity ?? 0m;
                var stockError = StockLedger.ValidateAvailable(
                    this.storage, companyId, line.ProductKey, line.DeliveredQuantity, ownReserved);
                if (stockError != null) return BadRequest(stockError);
            }

            foreach (var line in note.Lines.Where(l => l.DeliveredQuantity > 0))
            {
                var orderLine = linkedOrder?.Lines?.FirstOrDefault(ol =>
                    string.Equals(ol.ProductKey?.Trim(), line.ProductKey?.Trim(), StringComparison.OrdinalIgnoreCase));
                if (orderLine != null && orderLine.ReservedQuantity > 0)
                {
                    var release = Math.Min(line.DeliveredQuantity, orderLine.ReservedQuantity);
                    await StockLedger.ReleaseAsync(
                        this.storage, companyId, line.ProductKey, release, $"Consommation réservation BL {note.DeliveryNumber}");
                    orderLine.ReservedQuantity -= release;
                }

                var reason = string.IsNullOrWhiteSpace(line.LotNumber)
                    ? $"Sortie stock BL {note.DeliveryNumber}"
                    : $"Sortie stock BL {note.DeliveryNumber} (lot {line.LotNumber})";
                await StockLedger.ApplyAsync(
                    this.storage,
                    companyId,
                    line.ProductKey,
                    "Out",
                    line.DeliveredQuantity,
                    $"BL:{note.DeliveryNumber}",
                    reason,
                    User.Identity?.Name);
            }

            if (linkedOrder != null)
            {
                await this.storage.UpdateSalesOrderAsync(linkedOrder); // persiste réservations consommées
                await SyncOrderDelivered(linkedOrder.Id, note.Lines.Select(l => (l.ProductKey, l.DeliveredQuantity)).ToList());
            }

            note.Status = "Delivered";
            var updated = await this.storage.UpdateSalesDeliveryNoteAsync(note);
            await SalesDocumentAudit.LogAsync(
                this.storage,
                note.CompanyId ?? this.companyContext.GetCurrentCompanyId(),
                "DeliveryNote",
                updated.Id,
                "Validated",
                SalesDocumentAudit.ActorFrom(User),
                $"Validation BL {updated.DeliveryNumber}");

            var reorderNotes = await StockReorderHelper.SuggestDraftPurchaseOrdersAsync(
                this.storage,
                this.numberingService,
                companyId,
                note.Lines.Where(l => l.DeliveredQuantity > 0).Select(l => l.ProductKey));
            if (reorderNotes.Count > 0)
            {
                updated.Notes = string.IsNullOrWhiteSpace(updated.Notes)
                    ? string.Join(Environment.NewLine, reorderNotes)
                    : $"{updated.Notes}{Environment.NewLine}{string.Join(Environment.NewLine, reorderNotes)}";
                updated = await this.storage.UpdateSalesDeliveryNoteAsync(updated);
            }

            return Ok(updated);
        }

        /// <summary>Génère une facture depuis ce bon de livraison.</summary>
        [HttpPost("{id:int}/convert-to-invoice")]
        [RequirePermission(Permissions.InvoiceCreate)]
        public async Task<IActionResult> ConvertToInvoice(int id)
        {
            var note = await this.storage.SelectSalesDeliveryNoteByIdAsync(id);
            if (note == null || !note.BelongsToCompany(this.companyContext.GetCurrentCompanyId()))
                return NotFound("Bon de livraison introuvable.");
            if (string.Equals(note.Status, "Cancelled", StringComparison.OrdinalIgnoreCase))
                return BadRequest("Un BL annulé ne peut pas être facturé.");
            if (!string.Equals(note.Status, "Delivered", StringComparison.OrdinalIgnoreCase))
                return BadRequest("Le BL doit être validé (livré) avant facturation.");
            if (note.SalesInvoiceId.HasValue)
                return Conflict(new { error = $"Ce BL a déjà été facturé (facture #{note.SalesInvoiceId})." });

            if (note.SalesOrderId.HasValue)
            {
                var linkedOrder = await this.storage.SelectSalesOrderByIdAsync(note.SalesOrderId.Value);
                if (linkedOrder != null)
                {
                    var customerError = SalesBusinessRules.ValidateSameCustomer(linkedOrder.CustomerId, note.CustomerId, "commande → BL → facture");
                    if (customerError != null) return BadRequest(customerError);
                }
            }

            var invoiceCompanyId = note.CompanyId ?? this.companyContext.GetCurrentCompanyId();
            var company = await this.storage.SelectCompanyByIdAsync(invoiceCompanyId);
            var invoice = new SalesInvoice
            {
                InvoiceNumber = ProvisionalDocumentNumber.Create(),
                CustomerId = note.CustomerId,
                Date = DateTime.UtcNow,
                DueDate = DateTime.UtcNow.AddDays(30),
                Status = "Draft",
                TotalHT = note.TotalHT,
                TotalVat = note.TotalVat,
                TotalTTC = note.TotalTTC,
                Notes = $"Facture générée depuis BL {note.DeliveryNumber}",
                CompanyId = invoiceCompanyId,
                // RG-CP1 : devise figée à la création depuis Company.DefaultCurrencyCode.
                CurrencyCode = string.IsNullOrWhiteSpace(company?.DefaultCurrencyCode) ? "EUR" : company!.DefaultCurrencyCode,
                CreatedAt = DateTime.UtcNow,
                Lines = note.Lines
                    .Where(l => l.DeliveredQuantity > 0)
                    .Select((l, i) => new SalesInvoiceLine
                    {
                        ProductKey = l.ProductKey,
                        Description = l.Description,
                        Quantity = l.DeliveredQuantity,
                        OrderedQuantity = l.OrderedQuantity,
                        DeliveredQuantity = l.DeliveredQuantity,
                        UnitPrice = l.UnitPrice,
                        VatRate = l.VatRate,
                        TotalHT = l.TotalHT,
                        TotalTTC = l.TotalTTC,
                        LineNumber = i + 1,
                        // RG-LS1–5 lite : reprise du n° de lot saisi sur le BL.
                        LotNumber = l.LotNumber,
                        SupplierId = l.SupplierId
                    }).ToList()
            };
            if (invoice.Lines.Count == 0)
                return BadRequest("Aucune ligne livrée à facturer sur ce BL.");
            if (note.SalesOrderId.HasValue)
                invoice.SalesOrderId = note.SalesOrderId;

            var customer = await this.storage.SelectCustomerByIdAsync(note.CustomerId);
            invoice.DueDate = PaymentTermsHelper.ComputeDueDate(invoice.Date, customer?.PaymentTerms);

            var createdInvoice = await this.storage.InsertSalesInvoiceAsync(invoice);

            note.SalesInvoiceId = createdInvoice.Id;
            note.Status = "Invoiced";
            await this.storage.UpdateSalesDeliveryNoteAsync(note);

            if (note.SalesOrderId.HasValue)
            {
                var order = await this.storage.SelectSalesOrderByIdAsync(note.SalesOrderId.Value);
                if (order != null)
                {
                    SalesBusinessRules.AddInvoicedQuantities(
                        order,
                        note.Lines.Select(l => (l.ProductKey, l.DeliveredQuantity)));
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
            if (string.Equals(order.Status, "Cancelled", StringComparison.OrdinalIgnoreCase))
                return BadRequest("Une commande annulée ne peut pas générer de BL.");
            if (string.Equals(order.Status, "Draft", StringComparison.OrdinalIgnoreCase)
                || string.Equals(order.Status, "Pending", StringComparison.OrdinalIgnoreCase))
                return BadRequest("Une commande en brouillon ou en attente ne peut pas générer de BL. Confirmez/validez d'abord la commande.");

            var companyId = order.CompanyId ?? this.companyContext.GetCurrentCompanyId();
            var lines = new System.Collections.Generic.List<SalesDeliveryNoteLine>();
            var backorderKeys = new System.Collections.Generic.List<string>();
            var lineNumber = 0;
            foreach (var l in order.Lines)
            {
                // Reliquat = cmd − qtés déjà sur BL actifs (Draft inclus), pas seulement DeliveredQuantity validée
                var remaining = SalesBusinessRules.RemainingToShip(this.storage, order, l);
                if (remaining <= 0) continue;

                var onHand = StockLedger.GetAvailable(this.storage, companyId, l.ProductKey) + l.ReservedQuantity;
                var key = string.IsNullOrWhiteSpace(l.ProductKey) ? l.Description : l.ProductKey.Trim();
                if (onHand <= 0.0001m)
                {
                    backorderKeys.Add(key);
                    continue;
                }

                var delivered = Math.Min(remaining, onHand);
                if (delivered + 0.0001m < remaining)
                    backorderKeys.Add($"{key} (reliquat {remaining - delivered:0.####})");

                lineNumber++;
                lines.Add(new SalesDeliveryNoteLine
                {
                    ProductKey = l.ProductKey,
                    Description = l.Description,
                    OrderedQuantity = l.Quantity,
                    DeliveredQuantity = delivered,
                    UnitPrice = l.UnitPrice,
                    VatRate = l.VatRate,
                    TotalHT = delivered * l.UnitPrice,
                    TotalTTC = delivered * l.UnitPrice * (1 + l.VatRate / 100m),
                    LineNumber = lineNumber,
                    SupplierId = l.SupplierId
                });
            }

            if (lines.Count == 0)
            {
                var hasActiveDraft = this.storage.SelectAllSalesDeliveryNotes()
                    .Where(n => n.SalesOrderId == order.Id)
                    .AsEnumerable()
                    .Any(n => string.Equals(n.Status, "Draft", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(n.Status, "Sent", StringComparison.OrdinalIgnoreCase));
                if (hasActiveDraft)
                    return BadRequest("Un BL brouillon existe déjà pour cette commande. Validez-le, annulez-le ou créez un BL pour le reliquat après validation.");
                if (backorderKeys.Count > 0)
                {
                    return BadRequest(
                        $"Aucun produit disponible en stock. Reliquat : {string.Join(", ", backorderKeys)}.");
                }
                return BadRequest("Aucune quantité restante à livrer sur cette commande.");
            }

            var notes = $"BL généré depuis commande {order.OrderNumber}";
            if (backorderKeys.Count > 0)
                notes += $"{Environment.NewLine}Reliquat (hors BL) : {string.Join(", ", backorderKeys)}.";

            var note = new SalesDeliveryNote
            {
                DeliveryNumber = await this.numberingService.GetNextNumberAsync("SalesDeliveryNote", order.CompanyId),
                CustomerId = order.CustomerId,
                SalesOrderId = order.Id,
                DeliveryDate = DateTime.UtcNow,
                Status = "Draft",
                Notes = notes,
                CompanyId = companyId,
                CreatedAt = DateTime.UtcNow,
                Lines = lines
            };

            RecalcTotals(note);

            var created = await this.storage.InsertSalesDeliveryNoteAsync(note);
            await SalesDocumentAudit.LogAsync(
                this.storage,
                companyId,
                "DeliveryNote",
                created.Id,
                "Created",
                SalesDocumentAudit.ActorFrom(User),
                $"BL {created.DeliveryNumber} depuis commande {order.OrderNumber}");
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
                var line = order.Lines.FirstOrDefault(l =>
                    string.Equals(l.ProductKey?.Trim(), key?.Trim(), StringComparison.OrdinalIgnoreCase));
                if (line == null) continue;
                line.DeliveredQuantity = Math.Max(0, Math.Min(line.Quantity, line.DeliveredQuantity + qty));
            }
            SalesBusinessRules.RefreshOrderFulfillmentStatus(order);
            await this.storage.UpdateSalesOrderAsync(order);
        }

        private async Task ReverseStockOutAsync(SalesDeliveryNote note)
        {
            var companyId = note.CompanyId ?? this.companyContext.GetCurrentCompanyId();
            foreach (var line in note.Lines.Where(l => l.DeliveredQuantity > 0))
            {
                await StockLedger.ApplyAsync(
                    this.storage,
                    companyId,
                    line.ProductKey,
                    "In",
                    line.DeliveredQuantity,
                    $"BL-CANCEL:{note.DeliveryNumber}",
                    $"Annulation BL {note.DeliveryNumber}",
                    User.Identity?.Name);
            }
        }

        public class CancelRequest
        {
            public string? Reason { get; set; }
        }
    }
}
