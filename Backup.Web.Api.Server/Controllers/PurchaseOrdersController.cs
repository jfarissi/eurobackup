using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Authorization;
using Authorize = Microsoft.AspNetCore.Authorization.AuthorizeAttribute;
using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Models.Entities;
using Backup.Web.Api.Server.Models.Security;
using Backup.Web.Api.Server.Services.Documents.Parsing;
using Backup.Web.Api.Server.Services.Numbering;
using Backup.Web.Api.Server.Models.Entities.Email;
using Backup.Web.Api.Server.Services.Email;
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
    public class PurchaseOrdersController : RESTFulController
    {
        private readonly IStorageBroker storage;
        private readonly INumberingSequenceService numberingService;
        private readonly ICompanyContextService companyContext;
        private readonly IEmailDispatchService emailDispatch;

        public PurchaseOrdersController(
            IStorageBroker storage,
            INumberingSequenceService numberingService,
            ICompanyContextService companyContext,
            IEmailDispatchService emailDispatch)
        {
            this.storage = storage;
            this.numberingService = numberingService;
            this.companyContext = companyContext;
            this.emailDispatch = emailDispatch;
        }

        public class SendPurchaseOrderRequest
        {
            public bool SendEmail { get; set; } = true;
        }

        public class ReceiveDeliveryRequest
        {
            public int DeliveryDocumentId { get; set; }
            public bool UpdateStock { get; set; } = true;
        }

        public class ReceiveDeliveryResult
        {
            public PurchaseOrder PurchaseOrder { get; set; } = new();
            public bool StockUpdated { get; set; }
            public bool StockAlreadyApplied { get; set; }
            public int StockMovementCount { get; set; }
            public decimal StockQuantityIn { get; set; }
            public List<string> Warnings { get; set; } = new();
        }

        [HttpGet]
        [RequirePermission(Permissions.PurchaseOrderRead)]
        public IActionResult GetAll([FromQuery] string? search = null, [FromQuery] int? salesOrderId = null)
        {
            var query = this.storage.SelectAllPurchaseOrders().ForCompany(this.companyContext.GetCurrentCompanyId());
            if (salesOrderId is > 0)
                query = query.Where(p => p.SalesOrderId == salesOrderId);
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.ToLowerInvariant();
                query = query.Where(p => p.OrderNumber.ToLower().Contains(s) || (p.Supplier != null && p.Supplier.Name.ToLower().Contains(s)));
            }
            return Ok(query.OrderByDescending(p => p.Date).ToList());
        }

        [HttpGet("{id:int}")]
        [RequirePermission(Permissions.PurchaseOrderRead)]
        public async Task<IActionResult> GetById(int id)
        {
            var po = await this.storage.SelectPurchaseOrderByIdAsync(id);
            if (po == null || !po.BelongsToCompany(this.companyContext.GetCurrentCompanyId())) return NotFound();
            return Ok(po);
        }

        [HttpPost]
        [RequirePermission(Permissions.PurchaseOrderCreate)]
        public async Task<IActionResult> Post([FromBody] PurchaseOrder order)
        {
            order.EnsureCompanyId(this.companyContext.GetCurrentCompanyId());

            var supplier = await this.storage.SelectSupplierByIdAsync(order.SupplierId);
            if (supplier == null) return BadRequest("Fournisseur introuvable.");
            var partyErr = SalesBusinessRules.RejectIfPartyNotActive(supplier.Status, supplier.Name);
            if (partyErr != null) return BadRequest(partyErr);
            if (!supplier.IsActive)
                return BadRequest($"Le fournisseur ({supplier.Name}) est inactif.");

            if (string.IsNullOrWhiteSpace(order.OrderNumber))
            {
                order.OrderNumber = await this.numberingService.GetNextNumberAsync("PurchaseOrder", order.CompanyId);
            }
            order.Date = order.Date == default ? DateTime.UtcNow : order.Date;
            order.CreatedAt = DateTime.UtcNow;
            if (string.IsNullOrWhiteSpace(order.Status)) order.Status = "Draft";

            // RG-CP1 : devise figée à la création depuis Company.DefaultCurrencyCode.
            order.CurrencyCode = await SalesBusinessRules.ResolveCompanyCurrencyAsync(this.storage, order.CompanyId);

            foreach (var line in order.Lines ?? new List<PurchaseOrderLine>())
            {
                var discountErr = SalesBusinessRules.ValidateDiscountPercent(line.DiscountPercent, $"ligne {line.ProductKey}");
                if (discountErr != null) return BadRequest(discountErr);
            }
            var headerDiscountErr = SalesBusinessRules.ValidateDiscountPercent(order.HeaderDiscountPercent, "remise pied de page");
            if (headerDiscountErr != null) return BadRequest(headerDiscountErr);
            var shippingErr = SalesBusinessRules.ValidateShippingAmount(order.ShippingAmountHt);
            if (shippingErr != null) return BadRequest(shippingErr);

            SalesBusinessRules.RecalculatePurchaseOrderTotals(order);

            var created = await this.storage.InsertPurchaseOrderAsync(order);
            await this.AuditPurchaseOrder(created.Id, "Created", $"Création {created.OrderNumber} ({created.Status})");
            return Created(created);
        }

        [HttpPut("{id:int}")]
        [RequirePermission(Permissions.PurchaseOrderUpdate)]
        public async Task<IActionResult> Put(int id, [FromBody] PurchaseOrder order)
        {
            var existing = await this.storage.SelectPurchaseOrderByIdAsync(id);
            if (existing == null || !existing.BelongsToCompany(this.companyContext.GetCurrentCompanyId())) return NotFound();

            // RG-CF2 : modification libre réservée aux commandes Draft (Confirmed/Sent figées).
            if (!string.Equals(existing.Status, "Draft", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest($"Une commande fournisseur au statut {existing.Status} ne peut plus être modifiée (Draft uniquement).");
            }

            existing.SupplierId = order.SupplierId;
            existing.Date = order.Date;
            existing.ExpectedDeliveryDate = order.ExpectedDeliveryDate;
            existing.Status = order.Status;
            existing.Notes = order.Notes;
            existing.Lines = order.Lines;
            existing.HeaderDiscountPercent = order.HeaderDiscountPercent;
            existing.ShippingAmountHt = order.ShippingAmountHt;
            existing.ShippingVatRate = order.ShippingVatRate;

            foreach (var line in existing.Lines ?? new List<PurchaseOrderLine>())
            {
                var discountErr = SalesBusinessRules.ValidateDiscountPercent(line.DiscountPercent, $"ligne {line.ProductKey}");
                if (discountErr != null) return BadRequest(discountErr);
            }
            var headerDiscountErr = SalesBusinessRules.ValidateDiscountPercent(existing.HeaderDiscountPercent, "remise pied de page");
            if (headerDiscountErr != null) return BadRequest(headerDiscountErr);
            var shippingErr = SalesBusinessRules.ValidateShippingAmount(existing.ShippingAmountHt);
            if (shippingErr != null) return BadRequest(shippingErr);

            SalesBusinessRules.RecalculatePurchaseOrderTotals(existing);

            var updated = await this.storage.UpdatePurchaseOrderAsync(existing);
            return Ok(updated);
        }

        /// <summary>RG-CF2 : Draft → Confirmed.</summary>
        [HttpPost("{id:int}/confirm")]
        [RequirePermission(Permissions.PurchaseOrderUpdate)]
        public async Task<IActionResult> Confirm(int id)
        {
            var existing = await this.storage.SelectPurchaseOrderByIdAsync(id);
            if (existing == null || !existing.BelongsToCompany(this.companyContext.GetCurrentCompanyId())) return NotFound();

            if (!string.Equals(existing.Status, "Draft", StringComparison.OrdinalIgnoreCase))
                return Conflict(new { error = $"Commande fournisseur déjà au statut {existing.Status}." });

            existing.Status = "Confirmed";
            var updated = await this.storage.UpdatePurchaseOrderAsync(existing);
            await this.AuditPurchaseOrder(updated.Id, "Confirmed", $"Confirmation {updated.OrderNumber}");
            return Ok(updated);
        }

        /// <summary>RG-CF2 : Confirmed → Sent (envoi au fournisseur).</summary>
        [HttpPost("{id:int}/send")]
        [RequirePermission(Permissions.PurchaseOrderUpdate)]
        public async Task<IActionResult> Send(int id, [FromBody] SendPurchaseOrderRequest? request = null)
        {
            request ??= new SendPurchaseOrderRequest();
            var existing = await this.storage.SelectPurchaseOrderByIdAsync(id);
            if (existing == null || !existing.BelongsToCompany(this.companyContext.GetCurrentCompanyId())) return NotFound();

            if (!string.Equals(existing.Status, "Confirmed", StringComparison.OrdinalIgnoreCase))
                return BadRequest($"Seule une commande Confirmée peut être envoyée (statut actuel : {existing.Status}).");

            existing.Status = "Sent";
            var updated = await this.storage.UpdatePurchaseOrderAsync(existing);
            await this.AuditPurchaseOrder(updated.Id, "Sent", $"Envoi {updated.OrderNumber}");

            EmailMessage? emailMessage = null;
            var companyId = updated.CompanyId ?? this.companyContext.GetCurrentCompanyId();
            var settings = await this.storage.SelectCompanyEmailSettingsByCompanyIdAsync(companyId);
            var shouldEmail = request.SendEmail && (settings?.AutoEmailOnPurchaseOrderSend ?? true);
            if (shouldEmail)
            {
                try
                {
                    emailMessage = await this.emailDispatch.QueueAsync(companyId, new SendEmailRequest
                    {
                        DocumentType = "PurchaseOrder",
                        DocumentId = updated.Id,
                        TemplateCode = EmailTemplateCodes.PurchaseOrder,
                        SendNow = true
                    }, User.Identity?.Name ?? "System");
                }
                catch (InvalidOperationException ex)
                {
                    return Ok(new
                    {
                        purchaseOrder = updated,
                        emailWarning = ex.Message
                    });
                }
            }

            return Ok(new
            {
                purchaseOrder = updated,
                email = emailMessage == null ? null : new
                {
                    emailMessage.Id,
                    emailMessage.Status,
                    emailMessage.ToEmail,
                    emailMessage.LastError
                }
            });
        }

        [HttpGet("{id:int}/audit")]
        [RequirePermission(Permissions.PurchaseOrderRead)]
        public async Task<IActionResult> GetAudit(int id)
        {
            var existing = await this.storage.SelectPurchaseOrderByIdAsync(id);
            if (existing == null || !existing.BelongsToCompany(this.companyContext.GetCurrentCompanyId())) return NotFound();

            var logs = this.storage.SelectAllDocumentAuditLogs()
                .Where(a => a.DocumentType == "PurchaseOrder" && a.DocumentId == id)
                .AsEnumerable()
                .OrderByDescending(a => a.CreatedAt)
                .ToList();
            return Ok(logs);
        }

        [HttpPost("{id:int}/receive-delivery")]
        [RequirePermission(Permissions.PurchaseOrderUpdate)]
        public async Task<IActionResult> ReceiveDelivery(int id, [FromBody] ReceiveDeliveryRequest request)
        {
            if (request.DeliveryDocumentId <= 0) return BadRequest("DeliveryDocumentId required");

            var existing = await this.storage.SelectPurchaseOrderByIdAsync(id);
            if (existing == null || !existing.BelongsToCompany(this.companyContext.GetCurrentCompanyId())) return NotFound("Purchase order not found");

            var delivery = await this.storage.SelectDocumentByIdAsync(request.DeliveryDocumentId);
            if (delivery == null) return NotFound("Delivery document not found");
            if (string.IsNullOrWhiteSpace(delivery.TypeDocument) || !delivery.TypeDocument.Contains("bonlivraison", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest("The provided document is not a delivery note.");
            }

            if (!string.IsNullOrWhiteSpace(delivery.Supplier) && existing.Supplier != null &&
                !string.Equals(delivery.Supplier.Trim(), existing.Supplier.Name.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest("Delivery note supplier does not match the purchase order supplier.");
            }

            var receiptNote = $"Received from delivery #{request.DeliveryDocumentId}";
            if (!string.IsNullOrWhiteSpace(existing.Notes) &&
                existing.Notes.Contains(receiptNote, StringComparison.OrdinalIgnoreCase))
            {
                return Conflict(new
                {
                    error = $"Delivery note #{request.DeliveryDocumentId} has already been applied to this purchase order."
                });
            }

            var deliveryLines = this.storage.SelectLinesByDocumentId(request.DeliveryDocumentId).ToList();
            if (deliveryLines.Count == 0) return BadRequest("No parsed lines found on the delivery note.");

            var receivedByKey = deliveryLines
                .Where(l => l.Quantity > 0)
                .GroupBy(l => ProductKeyHelper.Normalize(ProductKeyHelper.GetProductKey(l)), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    g => g.Key,
                    g => (
                        Quantity: g.Sum(x => x.Quantity),
                        Description: g.Select(x => x.Product).FirstOrDefault(p => !string.IsNullOrWhiteSpace(p)),
                        Unit: g.Select(x => x.Unit).FirstOrDefault(u => !string.IsNullOrWhiteSpace(u))
                    ),
                    StringComparer.OrdinalIgnoreCase);

            var appliedStockByKey = new Dictionary<string, (decimal Qty, decimal CostSum)>(StringComparer.OrdinalIgnoreCase);

            foreach (var line in existing.Lines)
            {
                var orderKey = ProductKeyHelper.Normalize(ProductKeyHelper.GetProductKey(line.ProductKey, null, line.Description));
                if (!receivedByKey.TryGetValue(orderKey, out var received))
                {
                    continue;
                }

                var remaining = Math.Max(0m, line.Quantity - line.ReceivedQuantity);
                var appliedQty = Math.Min(remaining, received.Quantity);
                if (appliedQty <= 0)
                {
                    continue;
                }

                line.ReceivedQuantity += appliedQty;
                if (appliedStockByKey.TryGetValue(orderKey, out var already))
                {
                    appliedStockByKey[orderKey] = (
                        already.Qty + appliedQty,
                        already.CostSum + appliedQty * line.UnitPrice);
                }
                else
                {
                    appliedStockByKey[orderKey] = (appliedQty, appliedQty * line.UnitPrice);
                }
            }

            var totalOrdered = existing.Lines.Sum(l => l.Quantity);
            var totalReceived = existing.Lines.Sum(l => l.ReceivedQuantity);

            existing.Status = totalReceived switch
            {
                <= 0 => existing.Status,
                _ when totalReceived >= totalOrdered => "Received",
                _ => "PartiallyReceived"
            };

            existing.Notes = string.IsNullOrWhiteSpace(existing.Notes)
                ? receiptNote
                : $"{existing.Notes}{Environment.NewLine}{receiptNote}";

            var result = new ReceiveDeliveryResult
            {
                PurchaseOrder = existing,
                Warnings = new List<string>()
            };

            if (request.UpdateStock)
            {
                var alreadyStocked = this.storage.SelectStockUpdatesByDeliveryId(request.DeliveryDocumentId).Any();
                if (alreadyStocked)
                {
                    result.StockAlreadyApplied = true;
                    result.Warnings.Add($"Stock was already updated from delivery #{request.DeliveryDocumentId}; skipped duplicate stock entry.");
                }
                else if (appliedStockByKey.Count == 0)
                {
                    result.Warnings.Add("No matching purchase-order lines received; stock was not updated.");
                }
                else
                {
                    var supplierName = existing.Supplier?.Name ?? delivery.Supplier;
                    var stockChanges = appliedStockByKey.Select(entry =>
                    {
                        receivedByKey.TryGetValue(entry.Key, out var meta);
                        decimal? unitCost = entry.Value.Qty > 0.0001m
                            ? Math.Round(entry.Value.CostSum / entry.Value.Qty, 4, MidpointRounding.AwayFromZero)
                            : null;
                        return (
                            productKey: entry.Key,
                            quantityDelta: entry.Value.Qty,
                            supplier: supplierName,
                            description: meta.Description,
                            unit: meta.Unit,
                            unitCost: unitCost
                        );
                    }).ToList();

                    await this.storage.UpsertStockBatchAsync(stockChanges, request.DeliveryDocumentId, invoiceId: null);

                    var createdBy = User.Identity?.Name ?? "System";
                    var reference = $"PO:{existing.OrderNumber}|BL:{request.DeliveryDocumentId}";
                    foreach (var change in stockChanges)
                    {
                        await this.storage.InsertStockMovementAsync(new StockMovement
                        {
                            ProductKey = change.productKey,
                            MovementType = "In",
                            Quantity = change.quantityDelta,
                            UnitCost = change.unitCost,
                            StockValue = change.unitCost.HasValue
                                ? Math.Round(change.quantityDelta * change.unitCost.Value, 4, MidpointRounding.AwayFromZero)
                                : null,
                            Reason = "Purchase order delivery receipt",
                            ReferenceDocument = reference,
                            CompanyId = existing.CompanyId,
                            CreatedBy = createdBy,
                            CreatedAt = DateTime.UtcNow
                        });
                    }

                    result.StockUpdated = true;
                    result.StockMovementCount = appliedStockByKey.Count;
                    result.StockQuantityIn = appliedStockByKey.Values.Sum(v => v.Qty);
                }
            }

            var updated = await this.storage.UpdatePurchaseOrderAsync(existing);
            result.PurchaseOrder = updated;
            return Ok(result);
        }

        public class CancelRequest
        {
            public string? Reason { get; set; }
        }

        /// <summary>RG-CF5 : annulation possible tant qu'aucun BR / quantité reçue.</summary>
        [HttpPost("{id:int}/cancel")]
        [RequirePermission(Permissions.PurchaseOrderUpdate)]
        public async Task<IActionResult> Cancel(int id, [FromBody] CancelRequest? request)
        {
            var existing = await this.storage.SelectPurchaseOrderByIdAsync(id);
            if (existing == null || !existing.BelongsToCompany(this.companyContext.GetCurrentCompanyId()))
                return NotFound();

            if (string.Equals(existing.Status, "Cancelled", StringComparison.OrdinalIgnoreCase))
                return Conflict(new { error = "Commande fournisseur déjà annulée." });

            var anyReceived = (existing.Lines ?? new List<PurchaseOrderLine>())
                .Any(l => l.ReceivedQuantity > 0.0001m);
            if (anyReceived
                || string.Equals(existing.Status, "PartiallyReceived", StringComparison.OrdinalIgnoreCase)
                || string.Equals(existing.Status, "Received", StringComparison.OrdinalIgnoreCase)
                || string.Equals(existing.Status, "Closed", StringComparison.OrdinalIgnoreCase)
                || string.Equals(existing.Status, "Invoiced", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest("Annulation impossible : des réceptions existent déjà. Annulez d'abord le(s) bon(s) de réception.");
            }

            existing.Status = "Cancelled";
            var motif = string.IsNullOrWhiteSpace(request?.Reason) ? "Annulation" : request!.Reason!.Trim();
            existing.Notes = string.IsNullOrWhiteSpace(existing.Notes)
                ? motif
                : $"{existing.Notes}{Environment.NewLine}{motif}";

            var updated = await this.storage.UpdatePurchaseOrderAsync(existing);
            await this.AuditPurchaseOrder(updated.Id, "Cancelled", $"Annulation {updated.OrderNumber}", motif);
            return Ok(updated);
        }

        private async Task AuditPurchaseOrder(int orderId, string action, string summary, string? details = null)
        {
            await SalesDocumentAudit.LogAsync(
                this.storage,
                this.companyContext.GetCurrentCompanyId(),
                "PurchaseOrder",
                orderId,
                action,
                SalesDocumentAudit.ActorFrom(User),
                summary,
                details);
        }
    }
}
