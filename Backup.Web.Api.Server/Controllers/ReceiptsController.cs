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
using Backup.Web.Api.Server.Services.Sales;
using Backup.Web.Api.Server.Services.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RESTFulSense.Controllers;

namespace Backup.Web.Api.Server.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class ReceiptsController : RESTFulController
    {
        private readonly IStorageBroker storage;
        private readonly INumberingSequenceService numberingService;
        private readonly ICompanyContextService companyContext;

        public ReceiptsController(IStorageBroker storage, INumberingSequenceService numberingService, ICompanyContextService companyContext)
        {
            this.storage = storage;
            this.numberingService = numberingService;
            this.companyContext = companyContext;
        }

        [HttpGet]
        [RequirePermission(Permissions.ReceiptRead)]
        public async Task<IActionResult> GetAll([FromQuery] string? search = null, [FromQuery] int? supplierId = null)
        {
            var companyId = this.companyContext.GetCurrentCompanyId();
            var query = this.storage.SelectAllReceipts().ForCompany(companyId);
            if (supplierId.HasValue)
            {
                query = query.Where(r => r.SupplierId == supplierId.Value);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.ToLowerInvariant();
                query = query.Where(r =>
                    r.ReceiptNumber.ToLower().Contains(s) ||
                    (r.Supplier != null && r.Supplier.Name.ToLower().Contains(s)) ||
                    (r.Notes != null && r.Notes.ToLower().Contains(s)));
            }

            var receipts = await query.OrderByDescending(r => r.ReceivedAt).Take(200).ToListAsync();
            return Ok(receipts);
        }

        [HttpGet("{id:int}")]
        [RequirePermission(Permissions.ReceiptRead)]
        public async Task<IActionResult> GetById(int id)
        {
            var receipt = await this.storage.SelectReceiptByIdAsync(id);
            if (receipt == null || !receipt.BelongsToCompany(this.companyContext.GetCurrentCompanyId())) return NotFound();
            return Ok(receipt);
        }

        [HttpGet("by-document/{documentId:int}")]
        [RequirePermission(Permissions.ReceiptRead)]
        public async Task<IActionResult> GetByDocument(int documentId)
        {
            var receipt = await this.storage.SelectReceiptByDocumentIdAsync(documentId);
            if (receipt == null || !receipt.BelongsToCompany(this.companyContext.GetCurrentCompanyId())) return NotFound();
            return Ok(receipt);
        }

        public class ComptabiliserRequest
        {
            public int DocumentId { get; set; }
            public int? SupplierId { get; set; }
            public int? PurchaseOrderId { get; set; }
            public bool UpdateStock { get; set; } = true;
            /// <summary>RG-BR : créer en QualityHold (stock différé jusqu'à POST /receipts/{id}/post).</summary>
            public bool HoldForQuality { get; set; }
            public decimal DefaultVatRate { get; set; } = 21m;
        }

        public class ComptabiliserResult
        {
            public Receipt Receipt { get; set; } = new();
            public bool StockUpdated { get; set; }
            public bool StockAlreadyApplied { get; set; }
            public int StockMovementCount { get; set; }
            public decimal StockQuantityIn { get; set; }
            public List<string> Warnings { get; set; } = new();
        }

        /// <summary>
        /// Comptabiliser un BL parsé (Documents) → ErpReceipts / ErpReceiptLines (+ stock).
        /// Aligné sur le flux Pulse « Valider & comptabiliser ».
        /// </summary>
        [HttpPost("comptabiliser")]
        [RequirePermission(Permissions.ReceiptCreate)]
        public async Task<IActionResult> Comptabiliser([FromBody] ComptabiliserRequest request)
        {
            if (request.DocumentId <= 0) return BadRequest("DocumentId required");

            var document = await this.storage.SelectDocumentByIdAsync(request.DocumentId);
            if (document == null) return NotFound("Document not found");

            var type = (document.TypeDocument ?? string.Empty).Trim().ToLowerInvariant();
            var isBl =
                type is "bonlivraison" or "bl" ||
                type.Contains("bonlivraison") ||
                type.Contains("bon de livraison") ||
                type.Contains("delivery note") ||
                (type.Contains("bon") && type.Contains("livraison"));
            if (!isBl)
            {
                return BadRequest("Le document doit être un bon de livraison (BonLivraison).");
            }

            var existing = await this.storage.SelectReceiptByDocumentIdAsync(request.DocumentId);
            if (existing != null)
            {
                return Conflict(new
                {
                    error = $"Ce BL est déjà comptabilisé (réception {existing.ReceiptNumber}).",
                    receipt = existing
                });
            }

            var supplierId = request.SupplierId;
            if (!supplierId.HasValue || supplierId.Value <= 0)
            {
                if (!string.IsNullOrWhiteSpace(document.Supplier))
                {
                    var name = document.Supplier.Trim();
                    var matchedSupplier = this.storage.SelectAllSuppliers()
                        .ForCompany(this.companyContext.GetCurrentCompanyId())
                        .FirstOrDefault(s => s.Name.ToLower() == name.ToLower());
                    if (matchedSupplier != null) supplierId = matchedSupplier.Id;
                }
            }

            if (!supplierId.HasValue || supplierId.Value <= 0)
            {
                return BadRequest("Fournisseur requis. Sélectionnez un fournisseur ou associez-le au document.");
            }

            var supplier = await this.storage.SelectSupplierByIdAsync(supplierId.Value);
            if (supplier == null) return BadRequest("Fournisseur introuvable.");

            if (request.PurchaseOrderId.HasValue)
            {
                var po = await this.storage.SelectPurchaseOrderByIdAsync(request.PurchaseOrderId.Value);
                if (po == null) return BadRequest("Commande fournisseur introuvable.");
                if (po.SupplierId != supplierId.Value)
                {
                    return BadRequest("La commande ne correspond pas au fournisseur sélectionné.");
                }
            }

            var documentLines = this.storage.SelectLinesByDocumentId(request.DocumentId)
                .OrderBy(l => l.LineNumber)
                .ToList();
            if (documentLines.Count == 0)
            {
                return BadRequest("Aucune ligne parsée sur ce bon de livraison.");
            }

            var vat = request.DefaultVatRate > 0 ? request.DefaultVatRate : 21m;
            var receiptNumber = !string.IsNullOrWhiteSpace(document.Numero)
                ? document.Numero!.Trim()
                : await this.numberingService.GetNextNumberAsync("Receipt", this.companyContext.GetCurrentCompanyId());

            // Quantités réelles validées sur Comparer (DeliveryLineAdjustments) → réception + stock
            var validatedAdjustments = this.storage.SelectAdjustmentsByDeliveryId(request.DocumentId)
                .Where(a => a.IsValidated && a.ActualQuantity.HasValue)
                .ToList();
            var adjustmentQtyByKey = validatedAdjustments
                .GroupBy(a => ProductKeyHelper.Normalize(a.ProductKey), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Last().ActualQuantity!.Value, StringComparer.OrdinalIgnoreCase);

            var adjustedLineCount = 0;
            // RG-BR : QualityHold si hold qualité ou stock différé ; sinon Posted immédiat.
            var hold = request.HoldForQuality || !request.UpdateStock;
            var receipt = new Receipt
            {
                ReceiptNumber = receiptNumber,
                SupplierId = supplierId.Value,
                PurchaseOrderId = request.PurchaseOrderId,
                DocumentId = request.DocumentId,
                ReceivedAt = document.DateDocument ?? DateTime.UtcNow,
                Status = hold ? "QualityHold" : "Posted",
                Notes = $"Réception créée depuis le document #{request.DocumentId} ({document.OriginalFileName})",
                CreatedBy = User.Identity?.Name ?? "System",
                CreatedAt = DateTime.UtcNow,
                CompanyId = this.companyContext.GetCurrentCompanyId(),
                Lines = documentLines.Select((line, index) =>
                {
                    var productKey = ProductKeyHelper.Normalize(ProductKeyHelper.GetProductKey(line));
                    var blQty = line.Quantity;
                    var qty = blQty;
                    if (adjustmentQtyByKey.TryGetValue(productKey, out var actualQty))
                    {
                        qty = actualQty;
                        if (actualQty != blQty) adjustedLineCount++;
                    }

                    var unitPrice = line.UnitPrice;
                    var ht = qty * unitPrice;
                    return new ReceiptLine
                    {
                        ProductKey = productKey,
                        Description = line.Product ?? string.Empty,
                        QuantityReceived = qty,
                        UnitPriceExclTax = unitPrice,
                        TaxRatePercent = vat,
                        LineAmountExclTax = ht,
                        LineTaxAmount = ht * (vat / 100m),
                        LineNumber = index + 1
                    };
                }).ToList()
            };

            var created = await this.storage.InsertReceiptAsync(receipt);
            created = await this.storage.SelectReceiptByIdAsync(created.Id) ?? created;

            await SalesDocumentAudit.LogAsync(
                this.storage,
                this.companyContext.GetCurrentCompanyId(),
                "Receipt",
                created.Id,
                hold ? "QualityHold" : "Posted",
                SalesDocumentAudit.ActorFrom(User),
                $"Réception {created.ReceiptNumber} ({created.Status})");

            var result = new ComptabiliserResult
            {
                Receipt = created,
                Warnings = new List<string>()
            };

            if (hold)
            {
                result.Warnings.Add("Réception en contrôle qualité (QualityHold) — stock non mis à jour. POST /receipts/{id}/post pour valider.");
            }

            if (adjustedLineCount > 0)
            {
                result.Warnings.Add(
                    $"{adjustedLineCount} ligne(s) avec quantité réelle validée (Comparer) appliquée(s) à la réception/stock.");
            }
            else if (validatedAdjustments.Count > 0)
            {
                result.Warnings.Add(
                    $"{validatedAdjustments.Count} quantité(s) validée(s) sur Comparer (égales au BL) prises en compte.");
            }

            // Écarts vs commande liée
            if (request.PurchaseOrderId.HasValue)
            {
                var po = await this.storage.SelectPurchaseOrderByIdAsync(request.PurchaseOrderId.Value);
                if (po != null)
                {
                    foreach (var rLine in created.Lines)
                    {
                        var key = ProductKeyHelper.Normalize(rLine.ProductKey);
                        var poLine = po.Lines.FirstOrDefault(l =>
                            string.Equals(
                                ProductKeyHelper.Normalize(ProductKeyHelper.GetProductKey(l.ProductKey, null, l.Description)),
                                key,
                                StringComparison.OrdinalIgnoreCase));
                        if (poLine == null)
                        {
                            result.Warnings.Add($"Ligne réception '{rLine.ProductKey}' absente de la commande {po.OrderNumber}.");
                            continue;
                        }

                        var remaining = Math.Max(0m, poLine.Quantity - poLine.ReceivedQuantity);
                        if (rLine.QuantityReceived > remaining + 0.0001m)
                        {
                            result.Warnings.Add(
                                $"Écart qté '{rLine.ProductKey}' : reçu {rLine.QuantityReceived:0.####} > reste commande {remaining:0.####}.");
                        }
                    }
                }
            }

            if (!hold && request.UpdateStock)
            {
                await this.ApplyReceiptStockAsync(created, supplier.Name, result);
            }

            // CFA : quantités reçues seulement quand Posted (pas en QualityHold).
            if (!hold && request.PurchaseOrderId.HasValue)
            {
                await this.ApplyReceiptToPurchaseOrderAsync(created, request.PurchaseOrderId.Value);
                created = await this.storage.SelectReceiptByIdAsync(created.Id) ?? created;
                result.Receipt = created;
            }

            return Ok(result);
        }

        /// <summary>RG-BR : QualityHold → Posted + entrée stock.</summary>
        [HttpPost("{id:int}/post")]
        [RequirePermission(Permissions.ReceiptCreate)]
        public async Task<IActionResult> PostFromHold(int id)
        {
            var receipt = await this.storage.SelectReceiptByIdAsync(id);
            if (receipt == null || !receipt.BelongsToCompany(this.companyContext.GetCurrentCompanyId()))
                return NotFound();

            if (string.Equals(receipt.Status, "Posted", StringComparison.OrdinalIgnoreCase))
                return Conflict(new { error = "Réception déjà validée (Posted)." });
            if (!string.Equals(receipt.Status, "QualityHold", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(receipt.Status, "Draft", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest($"Seules les réceptions QualityHold/Draft peuvent être validées (statut : {receipt.Status}).");
            }

            var supplier = await this.storage.SelectSupplierByIdAsync(receipt.SupplierId);
            var result = new ComptabiliserResult { Receipt = receipt, Warnings = new List<string>() };
            await this.ApplyReceiptStockAsync(receipt, supplier?.Name, result);

            receipt.Status = "Posted";
            var updated = await this.storage.UpdateReceiptAsync(receipt);
            result.Receipt = updated;

            if (updated.PurchaseOrderId.HasValue)
            {
                await this.ApplyReceiptToPurchaseOrderAsync(updated, updated.PurchaseOrderId.Value);
            }

            await SalesDocumentAudit.LogAsync(
                this.storage,
                this.companyContext.GetCurrentCompanyId(),
                "Receipt",
                updated.Id,
                "Posted",
                SalesDocumentAudit.ActorFrom(User),
                $"Validation qualité réception {updated.ReceiptNumber}");

            return Ok(result);
        }

        /// <summary>RG-BR : basculer une réception Draft/Posted sans stock? vers QualityHold (avant post).</summary>
        [HttpPost("{id:int}/hold")]
        [RequirePermission(Permissions.ReceiptCreate)]
        public async Task<IActionResult> Hold(int id, [FromBody] HoldRequest? request)
        {
            var receipt = await this.storage.SelectReceiptByIdAsync(id);
            if (receipt == null || !receipt.BelongsToCompany(this.companyContext.GetCurrentCompanyId()))
                return NotFound();
            if (string.Equals(receipt.Status, "Posted", StringComparison.OrdinalIgnoreCase))
                return BadRequest("Une réception déjà Posted ne peut plus passer en QualityHold.");

            receipt.Status = "QualityHold";
            if (!string.IsNullOrWhiteSpace(request?.Reason))
            {
                var note = $"Hold qualité : {request.Reason.Trim()}";
                receipt.Notes = string.IsNullOrWhiteSpace(receipt.Notes) ? note : $"{receipt.Notes}\n{note}";
            }

            var updated = await this.storage.UpdateReceiptAsync(receipt);
            await SalesDocumentAudit.LogAsync(
                this.storage,
                this.companyContext.GetCurrentCompanyId(),
                "Receipt",
                updated.Id,
                "QualityHold",
                SalesDocumentAudit.ActorFrom(User),
                $"Hold qualité {updated.ReceiptNumber}");
            return Ok(updated);
        }

        public class HoldRequest
        {
            public string? Reason { get; set; }
        }

        private async Task ApplyReceiptStockAsync(Receipt receipt, string? supplierName, ComptabiliserResult result)
        {
            if (!receipt.DocumentId.HasValue)
            {
                result.Warnings.Add("Pas de DocumentId : stock non alimenté automatiquement.");
                return;
            }

            var documentId = receipt.DocumentId.Value;
            var alreadyStocked = this.storage.SelectStockUpdatesByDeliveryId(documentId).Any();
            if (alreadyStocked)
            {
                result.StockAlreadyApplied = true;
                result.Warnings.Add($"Stock déjà alimenté pour le BL #{documentId} (pas de double entrée).");
                return;
            }

            var stockChanges = receipt.Lines
                .Where(l => l.QuantityReceived > 0)
                .GroupBy(l => ProductKeyHelper.Normalize(l.ProductKey), StringComparer.OrdinalIgnoreCase)
                .Select(g => (
                    productKey: g.Key,
                    quantityDelta: g.Sum(x => x.QuantityReceived),
                    supplier: supplierName,
                    description: g.Select(x => x.Description).FirstOrDefault(d => !string.IsNullOrWhiteSpace(d)),
                    unit: (string?)null
                ))
                .ToList();

            if (stockChanges.Count == 0)
            {
                result.Warnings.Add("Aucune quantité positive : stock non mis à jour.");
                return;
            }

            await this.storage.UpsertStockBatchAsync(stockChanges, documentId, invoiceId: null);
            var createdBy = User.Identity?.Name ?? "System";
            foreach (var change in stockChanges)
            {
                await this.storage.InsertStockMovementAsync(new StockMovement
                {
                    ProductKey = change.productKey,
                    MovementType = "In",
                    Quantity = change.quantityDelta,
                    Reason = "Comptabilisation réception fournisseur",
                    ReferenceDocument = $"REC:{receipt.ReceiptNumber}|BL:{documentId}",
                    CompanyId = this.companyContext.GetCurrentCompanyId(),
                    CreatedBy = createdBy,
                    CreatedAt = DateTime.UtcNow
                });
            }

            result.StockUpdated = true;
            result.StockMovementCount = stockChanges.Count;
            result.StockQuantityIn = stockChanges.Sum(c => c.quantityDelta);
        }

        private async Task ApplyReceiptToPurchaseOrderAsync(Receipt receipt, int purchaseOrderId)
        {
            var po = await this.storage.SelectPurchaseOrderByIdAsync(purchaseOrderId);
            if (po == null) return;

            var receivedByKey = receipt.Lines
                .GroupBy(l => ProductKeyHelper.Normalize(l.ProductKey), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.QuantityReceived), StringComparer.OrdinalIgnoreCase);

            foreach (var line in po.Lines)
            {
                var key = ProductKeyHelper.Normalize(ProductKeyHelper.GetProductKey(line.ProductKey, null, line.Description));
                if (!receivedByKey.TryGetValue(key, out var qty)) continue;
                var remaining = Math.Max(0m, line.Quantity - line.ReceivedQuantity);
                line.ReceivedQuantity += Math.Min(remaining, qty);
            }

            var totalOrdered = po.Lines.Sum(l => l.Quantity);
            var totalReceived = po.Lines.Sum(l => l.ReceivedQuantity);
            po.Status = totalReceived switch
            {
                <= 0 => po.Status,
                _ when totalReceived >= totalOrdered => "Received",
                _ => "PartiallyReceived"
            };
            await this.storage.UpdatePurchaseOrderAsync(po);
        }
    }
}
