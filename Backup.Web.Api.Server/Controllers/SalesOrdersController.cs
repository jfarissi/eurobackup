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
using Backup.Web.Api.Server.Services.Pricing;
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
    public class SalesOrdersController : RESTFulController
    {
        private readonly IStorageBroker storage;
        private readonly INumberingSequenceService numberingService;
        private readonly ICompanyContextService companyContext;
        private readonly IErpPricingService erpPricing;

        public SalesOrdersController(
            IStorageBroker storage,
            INumberingSequenceService numberingService,
            ICompanyContextService companyContext,
            IErpPricingService erpPricing)
        {
            this.storage = storage;
            this.numberingService = numberingService;
            this.companyContext = companyContext;
            this.erpPricing = erpPricing;
        }

        /// <summary>RG-PT1–5 lite : si UnitPrice &lt;= 0, tente le tarif client (CustomerPriceListItem) puis l'ERP.</summary>
        private async Task ApplyPriceListFallbackAsync(int customerId, string? companyId, List<SalesOrderLine> lines)
        {
            foreach (var line in lines)
            {
                if (line.UnitPrice > 0 || string.IsNullOrWhiteSpace(line.ProductKey)) continue;

                var priceListItem = this.storage.SelectAllCustomerPriceListItems()
                    .ForCompany(companyId)
                    .AsEnumerable()
                    .FirstOrDefault(p => p.CustomerId == customerId
                        && string.Equals(p.ProductKey?.Trim(), line.ProductKey.Trim(), StringComparison.OrdinalIgnoreCase)
                        && (!p.ValidFrom.HasValue || p.ValidFrom.Value.Date <= DateTime.UtcNow.Date)
                        && (!p.ValidTo.HasValue || p.ValidTo.Value.Date >= DateTime.UtcNow.Date));

                if (priceListItem != null)
                {
                    line.UnitPrice = priceListItem.UnitPrice;
                    if (priceListItem.VatRate.HasValue) line.VatRate = priceListItem.VatRate.Value;
                    continue;
                }

                var erpPrice = await this.erpPricing.GetProductPriceAsync(line.ProductKey, HttpContext.RequestAborted);
                if (erpPrice.HasValue) line.UnitPrice = erpPrice.Value;
            }
        }

        [HttpGet]
        [RequirePermission(Permissions.OrderRead)]
        public IActionResult GetAll([FromQuery] string? search = null)
        {
            var query = this.storage.SelectAllSalesOrders().ForCompany(this.companyContext.GetCurrentCompanyId());
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.ToLowerInvariant();
                query = query.Where(o => o.OrderNumber.ToLower().Contains(s) || (o.Customer != null && o.Customer.Name.ToLower().Contains(s)));
            }
            return Ok(query.OrderByDescending(o => o.Date).ToList());
        }

        /// <summary>P2 — Pilotage : en attente, reliquats, ruptures stock.</summary>
        [HttpGet("pilotage")]
        [RequirePermission(Permissions.OrderRead)]
        public IActionResult GetPilotage()
        {
            var companyId = this.companyContext.GetCurrentCompanyId();
            var orders = this.storage.SelectAllSalesOrders()
                .ForCompany(companyId)
                .Where(o => o.Status != "Cancelled" && o.Status != "Closed" && o.Status != "Draft")
                .ToList();

            var pendingOrders = orders
                .Where(o => SalesBusinessRules.IsPendingStatus(o.Status))
                .OrderByDescending(o => o.Date)
                .ToList();

            var backorderLines = new List<SalesBackorderLineDto>();
            foreach (var order in orders.Where(o => !SalesBusinessRules.IsPendingStatus(o.Status)))
            {
                foreach (var line in order.Lines ?? new List<SalesOrderLine>())
                {
                    var remaining = SalesBusinessRules.RemainingQuantity(line);
                    if (remaining <= 0.0001m) continue;

                    var onHand = StockLedger.GetAvailable(this.storage, companyId, line.ProductKey);
                    backorderLines.Add(new SalesBackorderLineDto
                    {
                        OrderId = order.Id,
                        OrderNumber = order.OrderNumber,
                        OrderStatus = order.Status,
                        CustomerId = order.CustomerId,
                        CustomerName = order.Customer?.Name ?? $"#{order.CustomerId}",
                        ProductKey = line.ProductKey ?? "",
                        Description = line.Description ?? "",
                        OrderedQuantity = line.Quantity,
                        DeliveredQuantity = line.DeliveredQuantity,
                        RemainingQuantity = remaining,
                        StockOnHand = onHand,
                        IsStockout = onHand + 0.0001m < remaining
                    });
                }
            }

            backorderLines = backorderLines
                .OrderByDescending(l => l.IsStockout)
                .ThenBy(l => l.OrderNumber)
                .ThenBy(l => l.ProductKey)
                .ToList();

            return Ok(new SalesPilotageDto
            {
                PendingCount = pendingOrders.Count,
                BackorderLineCount = backorderLines.Count,
                StockoutLineCount = backorderLines.Count(l => l.IsStockout),
                PendingOrders = pendingOrders,
                BackorderLines = backorderLines,
                StockoutLines = backorderLines.Where(l => l.IsStockout).ToList()
            });
        }

        [HttpGet("{id:int}")]
        [RequirePermission(Permissions.OrderRead)]
        public async Task<IActionResult> GetById(int id)
        {
            var order = await this.storage.SelectSalesOrderByIdAsync(id);
            if (order == null || !order.BelongsToCompany(this.companyContext.GetCurrentCompanyId())) return NotFound();
            return Ok(order);
        }

        [HttpPost]
        [RequirePermission(Permissions.OrderCreate)]
        public async Task<IActionResult> Post([FromBody] SalesOrder order)
        {
            order.EnsureCompanyId(this.companyContext.GetCurrentCompanyId());
            if (order.QuoteId.HasValue)
            {
                var quote = await this.storage.SelectQuoteByIdAsync(order.QuoteId.Value);
                if (quote == null) return BadRequest("Devis lié introuvable.");
                var customerError = SalesBusinessRules.ValidateSameCustomer(quote.CustomerId, order.CustomerId, "devis → commande");
                if (customerError != null) return BadRequest(customerError);
            }

            var party = await this.storage.SelectCustomerByIdAsync(order.CustomerId);
            if (party == null) return BadRequest("Client introuvable.");
            var partyErr = SalesBusinessRules.RejectIfPartyNotActive(party.Status, party.Name);
            if (partyErr != null) return BadRequest(partyErr);

            // RG-FC7 : TVA par défaut selon le pays du client si non renseignée sur la ligne.
            var defaultVatRate = VatLocalization.DefaultRateForCountry(party.Country);
            foreach (var line in order.Lines)
            {
                if (line.VatRate <= 0) line.VatRate = defaultVatRate;
                var discountErr = SalesBusinessRules.ValidateDiscountPercent(line.DiscountPercent, $"ligne {line.ProductKey}");
                if (discountErr != null) return BadRequest(discountErr);
            }
            var headerDiscountErr = SalesBusinessRules.ValidateDiscountPercent(order.HeaderDiscountPercent, "remise pied de page");
            if (headerDiscountErr != null) return BadRequest(headerDiscountErr);

            // RG-PT1–5 lite : si prix de ligne non renseigné, tenter le tarif client puis l'ERP.
            await this.ApplyPriceListFallbackAsync(order.CustomerId, order.CompanyId, order.Lines);

            order.Date = order.Date == default ? DateTime.UtcNow : order.Date;
            order.CreatedAt = DateTime.UtcNow;

            // RG-CP1 : devise figée à la création depuis Company.DefaultCurrencyCode.
            var company = await this.storage.SelectCompanyByIdAsync(order.CompanyId);
            order.CurrencyCode = string.IsNullOrWhiteSpace(company?.DefaultCurrencyCode) ? "EUR" : company!.DefaultCurrencyCode;

            RecalcOrderTotals(order);

            if (string.IsNullOrWhiteSpace(order.Status)) order.Status = "Draft";
            if (!string.Equals(order.Status, "Draft", StringComparison.OrdinalIgnoreCase)
                && !SalesBusinessRules.IsPendingStatus(order.Status))
            {
                var creditError = SalesBusinessRules.ValidateCreditLimit(this.storage, party, order.TotalTTC);
                if (creditError != null)
                {
                    order.Status = "Pending";
                    SalesBusinessRules.AppendNote(order, $"En attente (crédit) : {creditError}");
                }
            }

            // RG-CC3 : n° définitif seulement pour une commande déjà engagée (Confirmed) ;
            // sinon n° provisoire (Draft/Pending) alloué au numéro définitif à la confirmation.
            if (string.IsNullOrWhiteSpace(order.OrderNumber) || ProvisionalDocumentNumber.IsProvisional(order.OrderNumber))
            {
                order.OrderNumber = SalesBusinessRules.IsOrderCommitted(order.Status)
                    ? await this.numberingService.GetNextNumberAsync("Order", order.CompanyId)
                    : ProvisionalDocumentNumber.Create();
            }

            var created = await this.storage.InsertSalesOrderAsync(order);
            await this.AuditOrder(created.Id, "Created", $"Création {created.OrderNumber} ({created.Status})");
            return Created(created);
        }

        [HttpPut("{id:int}")]
        [RequirePermission(Permissions.OrderUpdate)]
        public async Task<IActionResult> Put(int id, [FromBody] SalesOrder order)
        {
            var existing = await this.storage.SelectSalesOrderByIdAsync(id);
            if (existing == null || !existing.BelongsToCompany(this.companyContext.GetCurrentCompanyId()))
                return NotFound();

            var status = existing.Status ?? "";
            if (string.Equals(status, "Cancelled", StringComparison.OrdinalIgnoreCase)
                || string.Equals(status, "Closed", StringComparison.OrdinalIgnoreCase)
                || string.Equals(status, "Invoiced", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest($"Une commande au statut {status} ne peut pas être modifiée.");
            }

            // RG-CP1 : devise figée hors Draft.
            var currencyErr = SalesBusinessRules.RejectCurrencyChangeIfFrozen(status, existing.CurrencyCode, order.CurrencyCode);
            if (currencyErr != null) return BadRequest(currencyErr);

            var committed = SalesBusinessRules.IsOrderCommitted(status);
            var incoming = order.Lines ?? new System.Collections.Generic.List<SalesOrderLine>();
            if (incoming.Count == 0)
                return BadRequest("La commande doit contenir au moins une ligne.");

            foreach (var line in incoming)
            {
                var discountErr = SalesBusinessRules.ValidateDiscountPercent(line.DiscountPercent, $"ligne {line.ProductKey}");
                if (discountErr != null) return BadRequest(discountErr);
            }
            var headerDiscountErr = SalesBusinessRules.ValidateDiscountPercent(order.HeaderDiscountPercent, "remise pied de page");
            if (headerDiscountErr != null) return BadRequest(headerDiscountErr);

            // RG-CC9 : client figé après confirmation.
            var customerFreezeErr = SalesBusinessRules.RejectIfCustomerChangedAfterCommit(status, existing.CustomerId, order.CustomerId);
            if (customerFreezeErr != null) return BadRequest(customerFreezeErr);

            var locked = existing.Lines
                .Where(l => l.DeliveredQuantity > 0 || l.InvoicedQuantity > 0)
                .ToList();

            foreach (var lockedLine in locked)
            {
                var match = incoming.FirstOrDefault(l =>
                    (l.Id > 0 && l.Id == lockedLine.Id)
                    || string.Equals(l.ProductKey?.Trim(), lockedLine.ProductKey?.Trim(), StringComparison.OrdinalIgnoreCase));
                var lockedErr = SalesBusinessRules.RejectIfLockedOrderLineViolation(lockedLine, match);
                if (lockedErr != null) return BadRequest(lockedErr);
            }

            // RG-CC4 : lignes déjà engagées sur un BL (même Draft) non extensibles.
            foreach (var existingLine in existing.Lines)
            {
                var allocated = SalesBusinessRules.AllocatedOnDeliveryNotes(this.storage, existing.Id, existingLine.ProductKey);
                if (allocated <= 0.0001m) continue;
                var match = incoming.FirstOrDefault(l =>
                    (l.Id > 0 && l.Id == existingLine.Id)
                    || string.Equals(l.ProductKey?.Trim(), existingLine.ProductKey?.Trim(), StringComparison.OrdinalIgnoreCase));
                if (match == null)
                    return BadRequest($"Impossible de supprimer la ligne '{existingLine.ProductKey}' déjà engagée sur un BL.");
                if (match.Quantity > existingLine.Quantity + 0.0001m)
                    return BadRequest($"Impossible d'augmenter '{existingLine.ProductKey}' : un BL existe déjà sur cette ligne.");
            }

            var merged = new System.Collections.Generic.List<SalesOrderLine>();
            var lineNumber = 0;
            foreach (var incomingLine in incoming)
            {
                lineNumber++;
                var prev = existing.Lines.FirstOrDefault(l =>
                    (incomingLine.Id > 0 && l.Id == incomingLine.Id)
                    || string.Equals(l.ProductKey?.Trim(), incomingLine.ProductKey?.Trim(), StringComparison.OrdinalIgnoreCase));

                // RG-CC3 : prix figés après confirmation.
                var unitPrice = committed && prev != null ? prev.UnitPrice : incomingLine.UnitPrice;
                var vatRate = committed && prev != null ? prev.VatRate : incomingLine.VatRate;
                if (prev != null && (prev.DeliveredQuantity > 0 || prev.InvoicedQuantity > 0))
                {
                    unitPrice = prev.UnitPrice;
                    vatRate = prev.VatRate;
                }

                var discountPercent = SalesBusinessRules.CapDiscountPercent(incomingLine.DiscountPercent);
                var lineHt = incomingLine.Quantity * unitPrice * (1 - (discountPercent / 100m));
                merged.Add(new SalesOrderLine
                {
                    ProductKey = incomingLine.ProductKey,
                    Description = committed && prev != null ? prev.Description : incomingLine.Description,
                    Quantity = incomingLine.Quantity,
                    DeliveredQuantity = prev?.DeliveredQuantity ?? 0m,
                    InvoicedQuantity = prev?.InvoicedQuantity ?? 0m,
                    ReservedQuantity = prev?.ReservedQuantity ?? 0m,
                    UnitPrice = unitPrice,
                    DiscountPercent = discountPercent,
                    VatRate = vatRate,
                    TotalHT = lineHt,
                    TotalTTC = lineHt * (1 + vatRate / 100m),
                    LineNumber = lineNumber
                });
            }

            // Ajuster réservations si quantités changent
            foreach (var line in merged)
            {
                var openNeed = Math.Max(0m, line.Quantity - line.DeliveredQuantity);
                if (line.ReservedQuantity > openNeed + 0.0001m)
                {
                    var excess = line.ReservedQuantity - openNeed;
                    await StockLedger.ReleaseAsync(
                        this.storage,
                        existing.CompanyId,
                        line.ProductKey,
                        excess,
                        $"Ajustement réservation {existing.OrderNumber}");
                    line.ReservedQuantity = openNeed;
                }
            }

            if (!committed)
            {
                existing.CustomerId = order.CustomerId;
                existing.BillingAddress = order.BillingAddress;
                existing.ShippingAddress = order.ShippingAddress;
            }
            // RG-CT3 : adresses figées après Confirm — ignore payload.
            existing.Notes = order.Notes;
            existing.Lines = merged;
            existing.HeaderDiscountPercent = SalesBusinessRules.CapDiscountPercent(order.HeaderDiscountPercent);
            RecalcOrderTotals(existing);
            SalesBusinessRules.RefreshOrderFulfillmentStatus(existing);

            var updated = await this.storage.UpdateSalesOrderAsync(existing);
            await this.AuditOrder(updated.Id, "Updated", $"Modification {updated.OrderNumber} — {updated.Lines.Count} ligne(s)");
            return Ok(updated);
        }

        [HttpPost("{id:int}/confirm")]
        [RequirePermission(Permissions.OrderUpdate)]
        public async Task<IActionResult> Confirm(int id)
        {
            var order = await this.storage.SelectSalesOrderByIdAsync(id);
            if (order == null || !order.BelongsToCompany(this.companyContext.GetCurrentCompanyId())) return NotFound();
            if (string.Equals(order.Status, "Cancelled", StringComparison.OrdinalIgnoreCase))
                return BadRequest("Une commande annulée ne peut pas être confirmée.");
            if (!string.Equals(order.Status, "Draft", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(order.Status))
                return Conflict(new { error = $"Commande déjà au statut {order.Status}." });

            var customer = await this.storage.SelectCustomerByIdAsync(order.CustomerId);
            if (customer == null) return BadRequest("Client introuvable.");
            var partyErr = SalesBusinessRules.RejectIfPartyNotActive(customer.Status, customer.Name);
            if (partyErr != null) return BadRequest(partyErr);
            var creditError = SalesBusinessRules.ValidateCreditLimit(this.storage, customer, order.TotalTTC, order.Id);
            if (creditError != null)
            {
                order.Status = "Pending";
                SalesBusinessRules.AppendNote(order, $"En attente (crédit) : {creditError}");
                var pending = await this.storage.UpdateSalesOrderAsync(order);
                await this.AuditOrder(pending.Id, "Held", $"En attente crédit — {pending.OrderNumber}", creditError);
                return Ok(pending);
            }

            // RG-CT3 : adresses figées à la confirmation.
            var snapshot = SalesBusinessRules.FormatPartyAddress(customer);
            if (string.IsNullOrWhiteSpace(order.BillingAddress)) order.BillingAddress = snapshot;
            if (string.IsNullOrWhiteSpace(order.ShippingAddress)) order.ShippingAddress = snapshot;

            var hardAllocationErr = await this.ValidateHardAllocationAsync(order);
            if (hardAllocationErr != null) return BadRequest(hardAllocationErr);

            order.Status = "Confirmed";
            // RG-CC3 : allocation du n° définitif à la confirmation si la commande portait un n° provisoire.
            if (ProvisionalDocumentNumber.IsProvisional(order.OrderNumber))
            {
                order.OrderNumber = await this.numberingService.GetNextNumberAsync("Order", order.CompanyId);
            }
            await StockLedger.ReserveOrderAsync(this.storage, order);
            var updated = await this.storage.UpdateSalesOrderAsync(order);
            await this.AuditOrder(updated.Id, "Confirmed", $"Confirmation {updated.OrderNumber}");
            return Ok(updated);
        }

        /// <summary>Valide une commande en attente (crédit / validation manuelle) → Confirmed.</summary>
        [HttpPost("{id:int}/approve")]
        [RequirePermission(Permissions.OrderUpdate)]
        public async Task<IActionResult> Approve(int id)
        {
            var order = await this.storage.SelectSalesOrderByIdAsync(id);
            if (order == null || !order.BelongsToCompany(this.companyContext.GetCurrentCompanyId())) return NotFound();
            if (!SalesBusinessRules.IsPendingStatus(order.Status))
                return BadRequest($"Seules les commandes en attente peuvent être validées (statut actuel : {order.Status}).");

            var customer = await this.storage.SelectCustomerByIdAsync(order.CustomerId);
            if (customer == null) return BadRequest("Client introuvable.");
            var partyErr = SalesBusinessRules.RejectIfPartyNotActive(customer.Status, customer.Name);
            if (partyErr != null) return BadRequest(partyErr);

            var snapshot = SalesBusinessRules.FormatPartyAddress(customer);
            if (string.IsNullOrWhiteSpace(order.BillingAddress)) order.BillingAddress = snapshot;
            if (string.IsNullOrWhiteSpace(order.ShippingAddress)) order.ShippingAddress = snapshot;

            var hardAllocationErr = await this.ValidateHardAllocationAsync(order);
            if (hardAllocationErr != null) return BadRequest(hardAllocationErr);

            order.Status = "Confirmed";
            // RG-CC3 : allocation du n° définitif à la validation si la commande portait un n° provisoire.
            if (ProvisionalDocumentNumber.IsProvisional(order.OrderNumber))
            {
                order.OrderNumber = await this.numberingService.GetNextNumberAsync("Order", order.CompanyId);
            }
            SalesBusinessRules.AppendNote(order, $"Validée le {DateTime.UtcNow:dd/MM/yyyy HH:mm} UTC.");
            await StockLedger.ReserveOrderAsync(this.storage, order);
            SalesBusinessRules.RefreshOrderFulfillmentStatus(order);
            var updated = await this.storage.UpdateSalesOrderAsync(order);
            await this.AuditOrder(updated.Id, "Approved", $"Validation {updated.OrderNumber}");
            return Ok(updated);
        }

        /// <summary>Met une commande Draft/Confirmed en attente de validation.</summary>
        [HttpPost("{id:int}/hold")]
        [RequirePermission(Permissions.OrderUpdate)]
        public async Task<IActionResult> Hold(int id, [FromBody] HoldRequest? request)
        {
            var order = await this.storage.SelectSalesOrderByIdAsync(id);
            if (order == null || !order.BelongsToCompany(this.companyContext.GetCurrentCompanyId())) return NotFound();
            if (SalesBusinessRules.IsPendingStatus(order.Status))
                return Conflict(new { error = "Commande déjà en attente." });
            if (string.Equals(order.Status, "Cancelled", StringComparison.OrdinalIgnoreCase)
                || string.Equals(order.Status, "Closed", StringComparison.OrdinalIgnoreCase)
                || string.Equals(order.Status, "Invoiced", StringComparison.OrdinalIgnoreCase)
                || string.Equals(order.Status, "Delivered", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest($"Une commande au statut {order.Status} ne peut pas être mise en attente.");
            }

            if (order.Lines.Any(l => l.DeliveredQuantity > 0 || l.InvoicedQuantity > 0))
                return BadRequest("Impossible : des quantités sont déjà livrées ou facturées.");

            var reason = string.IsNullOrWhiteSpace(request?.Reason)
                ? "Validation manuelle"
                : request!.Reason!.Trim();
            await StockLedger.ReleaseOrderAsync(this.storage, order, $"Mise en attente {order.OrderNumber}");
            order.Status = "Pending";
            SalesBusinessRules.AppendNote(order, $"En attente : {reason}");
            var updated = await this.storage.UpdateSalesOrderAsync(order);
            await this.AuditOrder(updated.Id, "Held", $"Mise en attente {updated.OrderNumber}", reason);
            return Ok(updated);
        }

        [HttpPost("{id:int}/convert-to-invoice")]
        [RequirePermission(Permissions.InvoiceCreate)]
        public Task<IActionResult> ConvertToInvoice(int id)
        {
            return Task.FromResult<IActionResult>(BadRequest(
                "Facturation directe depuis la commande interdite. Parcours : Commande → BL validé → Facturer le BL."));
        }

        [HttpPost("{id:int}/cancel")]
        [RequirePermission(Permissions.OrderUpdate)]
        public async Task<IActionResult> Cancel(int id, [FromBody] CancelRequest? request)
        {
            var order = await this.storage.SelectSalesOrderByIdAsync(id);
            if (order == null || !order.BelongsToCompany(this.companyContext.GetCurrentCompanyId())) return NotFound();
            if (string.Equals(order.Status, "Cancelled", StringComparison.OrdinalIgnoreCase))
                return Conflict(new { error = "Commande déjà annulée." });
            if (string.Equals(order.Status, "Closed", StringComparison.OrdinalIgnoreCase)
                || string.Equals(order.Status, "Invoiced", StringComparison.OrdinalIgnoreCase))
                return BadRequest("Une commande déjà facturée/clôturée ne peut pas être annulée.");

            if (order.Lines.Any(l => l.DeliveredQuantity > 0 || l.InvoicedQuantity > 0))
                return BadRequest("Impossible d'annuler : des quantités sont déjà livrées ou facturées. Annulez d'abord les BL ou utilisez un avoir.");

            var hasActiveDn = this.storage.SelectAllSalesDeliveryNotes()
                .Where(n => n.SalesOrderId == order.Id)
                .AsEnumerable()
                .Any(n => !string.Equals(n.Status, "Cancelled", StringComparison.OrdinalIgnoreCase));
            if (hasActiveDn)
                return BadRequest("Impossible d'annuler : des bons de livraison sont liés. Annulez d'abord les BL.");

            if (string.IsNullOrWhiteSpace(request?.Reason))
                return BadRequest("Motif d'annulation obligatoire.");

            order.Status = "Cancelled";
            var motif = request!.Reason!.Trim();
            SalesBusinessRules.AppendNote(order, $"Annulation : {motif}");
            await StockLedger.ReleaseOrderAsync(this.storage, order, $"Annulation {order.OrderNumber}");
            await this.storage.UpdateSalesOrderAsync(order);
            await this.AuditOrder(order.Id, "Cancelled", $"Annulation {order.OrderNumber}", motif);
            return Ok(order);
        }

        [HttpDelete("{id:int}")]
        [RequirePermission(Permissions.OrderUpdate)]
        public async Task<IActionResult> Delete(int id)
        {
            var order = await this.storage.SelectSalesOrderByIdAsync(id);
            if (order == null || !order.BelongsToCompany(this.companyContext.GetCurrentCompanyId())) return NotFound();
            if (!SalesBusinessRules.CanPhysicallyDelete(order.Status))
                return BadRequest("Seuls les brouillons peuvent être supprimés. Utilisez l'annulation pour les documents validés.");

            var actor = SalesDocumentAudit.ActorFrom(User);
            SalesBusinessRules.SoftDelete(order, actor);
            await this.storage.DeleteSalesOrderAsync(order);
            await this.AuditOrder(order.Id, "Deleted", $"Suppression soft {order.OrderNumber}");
            return NoContent();
        }

        [HttpPost("{id:int}/archive")]
        [RequirePermission(Permissions.OrderUpdate)]
        public async Task<IActionResult> Archive(int id)
        {
            var order = await this.storage.SelectSalesOrderByIdAsync(id);
            if (order == null || !order.BelongsToCompany(this.companyContext.GetCurrentCompanyId())) return NotFound();
            if (order.IsArchived) return Conflict(new { error = "Commande déjà archivée." });
            if (!SalesBusinessRules.CanArchive(order.Status))
                return BadRequest($"Une commande au statut {order.Status} ne peut pas être archivée.");

            SalesBusinessRules.Archive(order, SalesDocumentAudit.ActorFrom(User));
            await this.storage.UpdateSalesOrderAsync(order);
            await this.AuditOrder(order.Id, "Archived", $"Archivage {order.OrderNumber}");
            return Ok(order);
        }

        [HttpGet("{id:int}/audit")]
        [RequirePermission(Permissions.OrderRead)]
        public async Task<IActionResult> GetAudit(int id)
        {
            var order = await this.storage.SelectSalesOrderByIdAsync(id);
            if (order == null || !order.BelongsToCompany(this.companyContext.GetCurrentCompanyId())) return NotFound();

            var logs = this.storage.SelectAllDocumentAuditLogs()
                .Where(a => a.DocumentType == "Order" && a.DocumentId == id)
                .AsEnumerable()
                .OrderByDescending(a => a.CreatedAt)
                .ToList();
            return Ok(logs);
        }

        /// <summary>RG-CP3 : recalcule les totaux avec remise ligne + remise pied de page.</summary>
        private static void RecalcOrderTotals(SalesOrder order)
        {
            foreach (var line in order.Lines)
            {
                line.DiscountPercent = SalesBusinessRules.CapDiscountPercent(line.DiscountPercent);
                line.TotalHT = line.Quantity * line.UnitPrice * (1 - (line.DiscountPercent / 100m));
                line.TotalTTC = line.TotalHT * (1 + (line.VatRate / 100m));
            }

            var totalHT = order.Lines.Sum(l => l.TotalHT);
            var totalVat = order.Lines.Sum(l => l.TotalTTC - l.TotalHT);
            var totalTTC = order.Lines.Sum(l => l.TotalTTC);
            SalesBusinessRules.ApplyHeaderDiscount(order.HeaderDiscountPercent, ref totalHT, ref totalVat, ref totalTTC);
            order.TotalHT = totalHT;
            order.TotalVat = totalVat;
            order.TotalTTC = totalTTC;
        }

        /// <summary>RG-RS2 lite : si Company.RequireHardAllocation, la confirmation échoue si le stock disponible ne couvre pas intégralement le besoin (pas de réservation partielle silencieuse).</summary>
        private async Task<string?> ValidateHardAllocationAsync(SalesOrder order)
        {
            var company = await this.storage.SelectCompanyByIdAsync(order.CompanyId);
            if (company?.RequireHardAllocation != true) return null;

            foreach (var line in order.Lines ?? new List<SalesOrderLine>())
            {
                var need = Math.Max(0m, line.Quantity - line.DeliveredQuantity - line.ReservedQuantity);
                if (need <= 0.0001m || string.IsNullOrWhiteSpace(line.ProductKey)) continue;
                if (StockLedger.IsShippingFeeKey(line.ProductKey)) continue;

                var available = StockLedger.GetAvailable(this.storage, order.CompanyId, line.ProductKey);
                if (available + 0.0001m < need)
                {
                    return $"Allocation stricte requise (RG-RS2) : stock disponible insuffisant pour '{line.ProductKey}' (disponible {available:0.####}, requis {need:0.####}).";
                }
            }

            return null;
        }

        private async Task AuditOrder(int orderId, string action, string summary, string? details = null)
        {
            await SalesDocumentAudit.LogAsync(
                this.storage,
                this.companyContext.GetCurrentCompanyId(),
                "Order",
                orderId,
                action,
                SalesDocumentAudit.ActorFrom(User),
                summary,
                details);
        }

        public class CancelRequest
        {
            public string? Reason { get; set; }
        }

        public class HoldRequest
        {
            public string? Reason { get; set; }
        }
    }
}
