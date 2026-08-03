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
using Backup.Web.Api.Server.Services.Sales;
using Backup.Web.Api.Server.Services.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RESTFulSense.Controllers;

namespace Backup.Web.Api.Server.Controllers
{
    /// <summary>
    /// Demande de prix fournisseur (DPF) — RG-DPF1–4.
    /// Cycle : Draft → Sent → Awaiting → Processed (convertie en CDF Draft) / Cancelled.
    /// </summary>
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class SupplierRfqsController : RESTFulController
    {
        private readonly IStorageBroker storage;
        private readonly INumberingSequenceService numberingService;
        private readonly ICompanyContextService companyContext;

        public SupplierRfqsController(IStorageBroker storage, INumberingSequenceService numberingService, ICompanyContextService companyContext)
        {
            this.storage = storage;
            this.numberingService = numberingService;
            this.companyContext = companyContext;
        }

        public class CancelRequest
        {
            public string? Reason { get; set; }
        }

        public class ConvertToPurchaseOrderRequest
        {
            public int? SupplierId { get; set; }
        }

        [HttpGet]
        [RequirePermission(Permissions.PurchaseOrderRead)]
        public IActionResult GetAll([FromQuery] string? search = null, [FromQuery] int? supplierId = null)
        {
            var query = this.storage.SelectAllSupplierRfqs().ForCompany(this.companyContext.GetCurrentCompanyId());

            if (supplierId.HasValue)
                query = query.Where(r => r.SupplierId == supplierId.Value);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.ToLowerInvariant();
                query = query.Where(r => r.RfqNumber.ToLower().Contains(s) || (r.Supplier != null && r.Supplier.Name.ToLower().Contains(s)));
            }

            return Ok(query.OrderByDescending(r => r.Date).ToList());
        }

        [HttpGet("{id:int}")]
        [RequirePermission(Permissions.PurchaseOrderRead)]
        public async Task<IActionResult> GetById(int id)
        {
            var rfq = await this.storage.SelectSupplierRfqByIdAsync(id);
            if (rfq == null || !rfq.BelongsToCompany(this.companyContext.GetCurrentCompanyId())) return NotFound();
            return Ok(rfq);
        }

        [HttpPost]
        [RequirePermission(Permissions.PurchaseOrderCreate)]
        public async Task<IActionResult> Post([FromBody] SupplierRfq rfq)
        {
            rfq.EnsureCompanyId(this.companyContext.GetCurrentCompanyId());

            if (rfq.SupplierId.HasValue && rfq.SupplierId.Value > 0)
            {
                var supplier = await this.storage.SelectSupplierByIdAsync(rfq.SupplierId.Value);
                if (supplier == null) return BadRequest("Fournisseur introuvable.");
            }

            if (rfq.Lines == null || rfq.Lines.Count == 0)
                return BadRequest("La demande de prix doit contenir au moins une ligne.");

            rfq.RfqNumber = await this.numberingService.GetNextNumberAsync("SupplierRfq", rfq.CompanyId);
            rfq.Date = rfq.Date == default ? DateTime.UtcNow : rfq.Date;
            rfq.CreatedAt = DateTime.UtcNow;
            rfq.Status = "Draft";
            rfq.PurchaseOrderId = null;
            NormalizeLines(rfq);

            var created = await this.storage.InsertSupplierRfqAsync(rfq);
            await AuditRfqAsync(created.Id, "Created", $"Création DPF {created.RfqNumber}");
            return Created(created);
        }

        [HttpPut("{id:int}")]
        [RequirePermission(Permissions.PurchaseOrderUpdate)]
        public async Task<IActionResult> Put(int id, [FromBody] SupplierRfq rfq)
        {
            var existing = await this.storage.SelectSupplierRfqByIdAsync(id);
            if (existing == null || !existing.BelongsToCompany(this.companyContext.GetCurrentCompanyId())) return NotFound();

            var draftErr = DocumentLifecycleRules.RejectIfNotDraft(existing.Status, "DPF");
            if (draftErr != null) return BadRequest(draftErr);

            existing.SupplierId = rfq.SupplierId;
            existing.Notes = rfq.Notes;
            if (rfq.Lines != null && rfq.Lines.Count > 0)
                existing.Lines = rfq.Lines;
            NormalizeLines(existing);

            var updated = await this.storage.UpdateSupplierRfqAsync(existing);
            await AuditRfqAsync(updated.Id, "Updated", $"Modification DPF {updated.RfqNumber}");
            return Ok(updated);
        }

        /// <summary>RG-DPF2 : Draft → Sent.</summary>
        [HttpPost("{id:int}/send")]
        [RequirePermission(Permissions.PurchaseOrderUpdate)]
        public async Task<IActionResult> Send(int id)
        {
            var rfq = await this.storage.SelectSupplierRfqByIdAsync(id);
            if (rfq == null || !rfq.BelongsToCompany(this.companyContext.GetCurrentCompanyId())) return NotFound();

            var sendErr = DocumentLifecycleRules.RejectIfRfqCannotSend(rfq.Status);
            if (sendErr != null) return Conflict(new { error = sendErr });

            rfq.Status = "Sent";
            var updated = await this.storage.UpdateSupplierRfqAsync(rfq);
            await AuditRfqAsync(updated.Id, "Sent", $"Envoi DPF {updated.RfqNumber}");
            return Ok(updated);
        }

        /// <summary>RG-DPF3 : Sent → Awaiting (réponse fournisseur en attente).</summary>
        [HttpPost("{id:int}/await-response")]
        [RequirePermission(Permissions.PurchaseOrderUpdate)]
        public async Task<IActionResult> AwaitResponse(int id)
        {
            var rfq = await this.storage.SelectSupplierRfqByIdAsync(id);
            if (rfq == null || !rfq.BelongsToCompany(this.companyContext.GetCurrentCompanyId())) return NotFound();

            var awaitErr = DocumentLifecycleRules.RejectIfRfqCannotAwait(rfq.Status);
            if (awaitErr != null) return BadRequest(awaitErr);

            rfq.Status = "Awaiting";
            var updated = await this.storage.UpdateSupplierRfqAsync(rfq);
            await AuditRfqAsync(updated.Id, "Awaiting", $"DPF {updated.RfqNumber} en attente de réponse");
            return Ok(updated);
        }

        [HttpPost("{id:int}/cancel")]
        [RequirePermission(Permissions.PurchaseOrderUpdate)]
        public async Task<IActionResult> Cancel(int id, [FromBody] CancelRequest? request)
        {
            var rfq = await this.storage.SelectSupplierRfqByIdAsync(id);
            if (rfq == null || !rfq.BelongsToCompany(this.companyContext.GetCurrentCompanyId())) return NotFound();

            var cancelErr = DocumentLifecycleRules.RejectIfRfqCannotCancel(rfq.Status);
            if (cancelErr != null)
            {
                if (string.Equals(rfq.Status, "Cancelled", StringComparison.OrdinalIgnoreCase))
                    return Conflict(new { error = cancelErr });
                return BadRequest(cancelErr);
            }

            rfq.Status = "Cancelled";
            var motif = string.IsNullOrWhiteSpace(request?.Reason) ? "Annulation" : request!.Reason!.Trim();
            rfq.Notes = string.IsNullOrWhiteSpace(rfq.Notes) ? motif : $"{rfq.Notes}\n{motif}";

            var updated = await this.storage.UpdateSupplierRfqAsync(rfq);
            await AuditRfqAsync(updated.Id, "Cancelled", $"Annulation DPF {updated.RfqNumber}", motif);
            return Ok(updated);
        }

        /// <summary>RG-DPF4 : conversion en commande fournisseur Draft (fournisseur requis) ; marque la DPF Processed.</summary>
        [HttpPost("{id:int}/convert-to-purchase-order")]
        [RequirePermission(Permissions.PurchaseOrderCreate)]
        public async Task<IActionResult> ConvertToPurchaseOrder(int id, [FromBody] ConvertToPurchaseOrderRequest? request)
        {
            var rfq = await this.storage.SelectSupplierRfqByIdAsync(id);
            if (rfq == null || !rfq.BelongsToCompany(this.companyContext.GetCurrentCompanyId())) return NotFound();

            var convertErr = DocumentLifecycleRules.RejectIfRfqCannotConvert(rfq.Status);
            if (convertErr != null)
            {
                if (string.Equals(rfq.Status, "Processed", StringComparison.OrdinalIgnoreCase))
                    return Conflict(new { error = $"DPF déjà convertie (commande fournisseur #{rfq.PurchaseOrderId}).", purchaseOrderId = rfq.PurchaseOrderId });
                return BadRequest(convertErr);
            }

            var supplierId = request?.SupplierId ?? rfq.SupplierId;
            if (!supplierId.HasValue || supplierId.Value <= 0)
                return BadRequest("SupplierId requis pour convertir la DPF en commande fournisseur.");

            var supplier = await this.storage.SelectSupplierByIdAsync(supplierId.Value);
            if (supplier == null) return BadRequest("Fournisseur introuvable.");
            var partyErr = SalesBusinessRules.RejectIfPartyNotActive(supplier.Status, supplier.Name);
            if (partyErr != null) return BadRequest(partyErr);

            if (rfq.Lines == null || rfq.Lines.Count == 0)
                return BadRequest("La DPF ne contient aucune ligne à convertir.");

            var companyId = rfq.CompanyId ?? this.companyContext.GetCurrentCompanyId();
            var order = new PurchaseOrder
            {
                OrderNumber = await this.numberingService.GetNextNumberAsync("PurchaseOrder", companyId),
                SupplierId = supplier.Id,
                Date = DateTime.UtcNow,
                Status = "Draft",
                Notes = $"Créée depuis la DPF {rfq.RfqNumber}",
                CompanyId = companyId,
                CreatedAt = DateTime.UtcNow,
                Lines = rfq.Lines.Select((l, i) => new PurchaseOrderLine
                {
                    ProductKey = l.ProductKey,
                    Description = l.Description,
                    Quantity = l.Quantity,
                    UnitPrice = l.EstimatedUnitPrice,
                    VatRate = 21.0m,
                    TotalHT = l.Quantity * l.EstimatedUnitPrice,
                    TotalTTC = l.Quantity * l.EstimatedUnitPrice * 1.21m,
                    LineNumber = i + 1
                }).ToList()
            };
            order.TotalHT = order.Lines.Sum(l => l.TotalHT);
            order.TotalVat = order.Lines.Sum(l => l.TotalTTC - l.TotalHT);
            order.TotalTTC = order.Lines.Sum(l => l.TotalTTC);

            var createdOrder = await this.storage.InsertPurchaseOrderAsync(order);

            rfq.SupplierId = supplier.Id;
            rfq.Status = "Processed";
            rfq.PurchaseOrderId = createdOrder.Id;
            var updated = await this.storage.UpdateSupplierRfqAsync(rfq);
            await AuditRfqAsync(updated.Id, "Processed", $"Conversion DPF {updated.RfqNumber} → commande fournisseur {createdOrder.OrderNumber}");
            return Ok(createdOrder);
        }

        [HttpDelete("{id:int}")]
        [RequirePermission(Permissions.PurchaseOrderUpdate)]
        public async Task<IActionResult> Delete(int id)
        {
            var existing = await this.storage.SelectSupplierRfqByIdAsync(id);
            if (existing == null || !existing.BelongsToCompany(this.companyContext.GetCurrentCompanyId())) return NotFound();

            if (!string.Equals(existing.Status, "Draft", StringComparison.OrdinalIgnoreCase))
                return BadRequest("Seules les DPF Draft peuvent être supprimées. Sinon annulez.");

            var actor = SalesDocumentAudit.ActorFrom(User);
            SalesBusinessRules.SoftDelete(existing, actor);
            await this.storage.DeleteSupplierRfqAsync(existing);
            await AuditRfqAsync(existing.Id, "Deleted", $"Suppression soft DPF {existing.RfqNumber}");
            return NoContent();
        }

        private static void NormalizeLines(SupplierRfq rfq)
        {
            rfq.Lines ??= new List<SupplierRfqLine>();
            for (var i = 0; i < rfq.Lines.Count; i++)
            {
                var line = rfq.Lines[i];
                line.LineNumber = line.LineNumber <= 0 ? i + 1 : line.LineNumber;
                line.Description = line.Description?.Trim() ?? string.Empty;
                line.ProductKey = line.ProductKey?.Trim() ?? string.Empty;
            }
        }

        private async Task AuditRfqAsync(int rfqId, string action, string summary, string? details = null)
        {
            await SalesDocumentAudit.LogAsync(
                this.storage,
                this.companyContext.GetCurrentCompanyId(),
                "SupplierRfq",
                rfqId,
                action,
                SalesDocumentAudit.ActorFrom(User),
                summary,
                details);
        }
    }
}
