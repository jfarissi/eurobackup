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
using Backup.Web.Api.Server.Services.Stock;
using Backup.Web.Api.Server.Services.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RESTFulSense.Controllers;

namespace Backup.Web.Api.Server.Controllers
{
    /// <summary>
    /// Bon de retour fournisseur (BRF) — RG-BRF1–5.
    /// Cycle : Draft → Shipped (stock Out) / Cancelled (reverse stock si déjà expédié) ; peut générer un avoir fournisseur Draft.
    /// </summary>
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class SupplierReturnsController : RESTFulController
    {
        private readonly IStorageBroker storage;
        private readonly INumberingSequenceService numberingService;
        private readonly ICompanyContextService companyContext;

        public SupplierReturnsController(IStorageBroker storage, INumberingSequenceService numberingService, ICompanyContextService companyContext)
        {
            this.storage = storage;
            this.numberingService = numberingService;
            this.companyContext = companyContext;
        }

        public class CancelRequest
        {
            public string? Reason { get; set; }
        }

        public class CreateCreditNoteRequest
        {
            public int? SupplierInvoiceId { get; set; }
        }

        [HttpGet]
        [RequirePermission(Permissions.PurchaseOrderRead)]
        public IActionResult GetAll(
            [FromQuery] string? search = null,
            [FromQuery] int? supplierId = null,
            [FromQuery] int? purchaseOrderId = null)
        {
            var query = this.storage.SelectAllSupplierReturns().ForCompany(this.companyContext.GetCurrentCompanyId());

            if (supplierId.HasValue)
                query = query.Where(r => r.SupplierId == supplierId.Value);
            if (purchaseOrderId.HasValue)
                query = query.Where(r => r.PurchaseOrderId == purchaseOrderId.Value);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.ToLowerInvariant();
                query = query.Where(r => r.ReturnNumber.ToLower().Contains(s) || (r.Supplier != null && r.Supplier.Name.ToLower().Contains(s)));
            }

            return Ok(query.OrderByDescending(r => r.CreatedAt).ToList());
        }

        [HttpGet("{id:int}")]
        [RequirePermission(Permissions.PurchaseOrderRead)]
        public async Task<IActionResult> GetById(int id)
        {
            var supplierReturn = await this.storage.SelectSupplierReturnByIdAsync(id);
            if (supplierReturn == null || !supplierReturn.BelongsToCompany(this.companyContext.GetCurrentCompanyId())) return NotFound();
            return Ok(supplierReturn);
        }

        [HttpPost]
        [RequirePermission(Permissions.PurchaseOrderCreate)]
        public async Task<IActionResult> Post([FromBody] SupplierReturn supplierReturn)
        {
            supplierReturn.EnsureCompanyId(this.companyContext.GetCurrentCompanyId());

            var supplier = await this.storage.SelectSupplierByIdAsync(supplierReturn.SupplierId);
            if (supplier == null) return BadRequest("Fournisseur introuvable.");

            if (supplierReturn.Lines == null || supplierReturn.Lines.Count == 0)
                return BadRequest("Le retour fournisseur doit contenir au moins une ligne.");

            RecalcTotals(supplierReturn);

            var linkError = await ValidateLinksAsync(supplierReturn);
            if (linkError != null) return BadRequest(linkError);

            var capError = await ValidateCapAsync(supplierReturn);
            if (capError != null) return BadRequest(capError);

            supplierReturn.ReturnNumber = await this.numberingService.GetNextNumberAsync("SupplierReturn", supplierReturn.CompanyId);
            supplierReturn.Date = supplierReturn.Date == default ? DateTime.UtcNow : supplierReturn.Date;
            supplierReturn.CreatedAt = DateTime.UtcNow;
            supplierReturn.Status = "Draft";
            supplierReturn.StockApplied = false;
            supplierReturn.CreditNoteId = null;
            // RG-CP1 : devise figée à la création depuis Company.DefaultCurrencyCode.
            supplierReturn.CurrencyCode = await SalesBusinessRules.ResolveCompanyCurrencyAsync(this.storage, supplierReturn.CompanyId);

            var created = await this.storage.InsertSupplierReturnAsync(supplierReturn);
            await AuditReturnAsync(created.Id, "Created", $"Création retour fournisseur {created.ReturnNumber}");
            return Created(created);
        }

        [HttpPut("{id:int}")]
        [RequirePermission(Permissions.PurchaseOrderUpdate)]
        public async Task<IActionResult> Put(int id, [FromBody] SupplierReturn supplierReturn)
        {
            var existing = await this.storage.SelectSupplierReturnByIdAsync(id);
            if (existing == null || !existing.BelongsToCompany(this.companyContext.GetCurrentCompanyId())) return NotFound();

            if (!string.Equals(existing.Status, "Draft", StringComparison.OrdinalIgnoreCase))
                return BadRequest($"Un retour fournisseur au statut {existing.Status} ne peut plus être modifié (Draft uniquement).");

            existing.PurchaseOrderId = supplierReturn.PurchaseOrderId;
            existing.ReceiptId = supplierReturn.ReceiptId;
            existing.SupplierInvoiceId = supplierReturn.SupplierInvoiceId;
            existing.Notes = supplierReturn.Notes;
            if (supplierReturn.Lines != null && supplierReturn.Lines.Count > 0)
                existing.Lines = supplierReturn.Lines;

            RecalcTotals(existing);

            var linkError = await ValidateLinksAsync(existing);
            if (linkError != null) return BadRequest(linkError);

            var capError = await ValidateCapAsync(existing, id);
            if (capError != null) return BadRequest(capError);

            var updated = await this.storage.UpdateSupplierReturnAsync(existing);
            await AuditReturnAsync(updated.Id, "Updated", $"Modification retour fournisseur {updated.ReturnNumber}");
            return Ok(updated);
        }

        /// <summary>RG-BRF3 : Draft → Shipped, sortie de stock.</summary>
        [HttpPost("{id:int}/ship")]
        [RequirePermission(Permissions.PurchaseOrderUpdate)]
        public async Task<IActionResult> Ship(int id)
        {
            var supplierReturn = await this.storage.SelectSupplierReturnByIdAsync(id);
            if (supplierReturn == null || !supplierReturn.BelongsToCompany(this.companyContext.GetCurrentCompanyId())) return NotFound();

            var shipErr = DocumentLifecycleRules.RejectIfSupplierReturnCannotShip(supplierReturn.Status);
            if (shipErr != null) return Conflict(new { error = shipErr });

            var capError = await ValidateCapAsync(supplierReturn, id);
            if (capError != null) return BadRequest(capError);

            var companyId = supplierReturn.CompanyId ?? this.companyContext.GetCurrentCompanyId();
            await ApplyStockOutAsync(supplierReturn, companyId);

            supplierReturn.Status = "Shipped";
            var updated = await this.storage.UpdateSupplierReturnAsync(supplierReturn);
            await AuditReturnAsync(updated.Id, "Shipped", $"Expédition retour fournisseur {updated.ReturnNumber}");
            return Ok(updated);
        }

        /// <summary>RG-BRF5 : annulation possible sauf après création d'avoir ; réversion stock si déjà expédié.</summary>
        [HttpPost("{id:int}/cancel")]
        [RequirePermission(Permissions.PurchaseOrderUpdate)]
        public async Task<IActionResult> Cancel(int id, [FromBody] CancelRequest? request)
        {
            var supplierReturn = await this.storage.SelectSupplierReturnByIdAsync(id);
            if (supplierReturn == null || !supplierReturn.BelongsToCompany(this.companyContext.GetCurrentCompanyId())) return NotFound();

            var cancelErr = DocumentLifecycleRules.RejectIfSupplierReturnCannotCancel(
                supplierReturn.Status, supplierReturn.CreditNoteId.HasValue);
            if (cancelErr != null)
            {
                if (string.Equals(supplierReturn.Status, "Cancelled", StringComparison.OrdinalIgnoreCase))
                    return Conflict(new { error = cancelErr });
                return BadRequest(cancelErr);
            }

            var companyId = supplierReturn.CompanyId ?? this.companyContext.GetCurrentCompanyId();
            if (supplierReturn.StockApplied)
            {
                foreach (var line in supplierReturn.Lines)
                {
                    await StockLedger.ApplyAsync(
                        this.storage,
                        companyId,
                        line.ProductKey,
                        "In",
                        line.Quantity,
                        $"BRF-CANCEL:{supplierReturn.ReturnNumber}",
                        $"Annulation retour fournisseur {supplierReturn.ReturnNumber}",
                        User.Identity?.Name);
                }
                supplierReturn.StockApplied = false;
            }

            supplierReturn.Status = "Cancelled";
            var motif = string.IsNullOrWhiteSpace(request?.Reason) ? "Annulation" : request!.Reason!.Trim();
            supplierReturn.Notes = string.IsNullOrWhiteSpace(supplierReturn.Notes) ? motif : $"{supplierReturn.Notes}\n{motif}";

            var updated = await this.storage.UpdateSupplierReturnAsync(supplierReturn);
            await AuditReturnAsync(updated.Id, "Cancelled", $"Annulation retour fournisseur {updated.ReturnNumber}", motif);
            return Ok(updated);
        }

        /// <summary>RG-BRF4 : génère un avoir fournisseur Draft (SupplierInvoiceId obligatoire, explicite ou déjà lié).</summary>
        [HttpPost("{id:int}/create-credit-note")]
        [RequirePermission(Permissions.SupplierCreditNoteCreate)]
        public async Task<IActionResult> CreateCreditNote(int id, [FromBody] CreateCreditNoteRequest? request)
        {
            var supplierReturn = await this.storage.SelectSupplierReturnByIdAsync(id);
            if (supplierReturn == null || !supplierReturn.BelongsToCompany(this.companyContext.GetCurrentCompanyId())) return NotFound();

            var cnErr = DocumentLifecycleRules.RejectIfSupplierReturnCannotCreateCreditNote(
                supplierReturn.Status, supplierReturn.CreditNoteId.HasValue);
            if (cnErr != null)
            {
                if (supplierReturn.CreditNoteId.HasValue)
                {
                    return Conflict(new
                    {
                        error = $"Un avoir existe déjà pour ce retour (#{supplierReturn.CreditNoteId}).",
                        creditNoteId = supplierReturn.CreditNoteId
                    });
                }

                return BadRequest(cnErr);
            }

            var supplierInvoiceId = request?.SupplierInvoiceId ?? supplierReturn.SupplierInvoiceId;
            if (!supplierInvoiceId.HasValue || supplierInvoiceId.Value <= 0)
                return BadRequest("Un avoir fournisseur doit être lié à une facture fournisseur : précisez SupplierInvoiceId.");

            var invoice = await this.storage.SelectSupplierInvoiceByIdAsync(supplierInvoiceId.Value);
            if (invoice == null) return BadRequest("Facture fournisseur introuvable.");
            if (invoice.SupplierId != supplierReturn.SupplierId)
                return BadRequest("La facture fournisseur doit appartenir au même fournisseur que le retour.");

            var companyId = supplierReturn.CompanyId ?? this.companyContext.GetCurrentCompanyId();
            var creditLines = supplierReturn.Lines.Select((l, i) => new SupplierCreditNoteLineEntity
            {
                ProductKey = l.ProductKey,
                Description = l.Description,
                Quantity = l.Quantity,
                UnitPrice = l.UnitPrice,
                VatRate = l.VatRate,
                TotalHT = l.Quantity * l.UnitPrice,
                TotalTTC = l.Quantity * l.UnitPrice * (1 + l.VatRate / 100m),
                LineNumber = i + 1
            }).ToList();

            var creditNote = new SupplierCreditNoteEntity
            {
                SupplierId = supplierReturn.SupplierId,
                SupplierInvoiceId = invoice.Id,
                Date = DateTime.UtcNow,
                Status = "Draft",
                Notes = $"Avoir généré depuis retour fournisseur {supplierReturn.ReturnNumber}",
                CompanyId = companyId,
                CreatedAt = DateTime.UtcNow,
                Lines = creditLines
            };
            creditNote.TotalHT = creditNote.Lines.Sum(l => l.TotalHT);
            creditNote.TotalVat = creditNote.Lines.Sum(l => l.TotalTTC - l.TotalHT);
            creditNote.TotalTTC = creditNote.Lines.Sum(l => l.TotalTTC);
            creditNote.CreditNoteNumber = await this.numberingService.GetNextNumberAsync("SupplierCreditNote", companyId);

            if (creditNote.TotalTTC > invoice.TotalTTC + 0.01m)
                return BadRequest($"L'avoir ({creditNote.TotalTTC:0.##} €) dépasserait le TTC de la facture {invoice.InvoiceNumber} ({invoice.TotalTTC:0.##} €).");

            var created = await this.storage.InsertSupplierCreditNoteAsync(creditNote);

            supplierReturn.CreditNoteId = created.Id;
            supplierReturn.SupplierInvoiceId = invoice.Id;
            await this.storage.UpdateSupplierReturnAsync(supplierReturn);

            await SalesDocumentAudit.LogAsync(
                this.storage, companyId, "SupplierCreditNote", created.Id, "Created", SalesDocumentAudit.ActorFrom(User),
                $"Avoir {created.CreditNoteNumber} généré depuis retour fournisseur {supplierReturn.ReturnNumber}");

            return Created(created);
        }

        [HttpDelete("{id:int}")]
        [RequirePermission(Permissions.PurchaseOrderUpdate)]
        public async Task<IActionResult> Delete(int id)
        {
            var existing = await this.storage.SelectSupplierReturnByIdAsync(id);
            if (existing == null || !existing.BelongsToCompany(this.companyContext.GetCurrentCompanyId())) return NotFound();

            if (!string.Equals(existing.Status, "Draft", StringComparison.OrdinalIgnoreCase))
                return BadRequest("Seuls les retours fournisseur Draft peuvent être supprimés. Sinon annulez.");

            var actor = SalesDocumentAudit.ActorFrom(User);
            SalesBusinessRules.SoftDelete(existing, actor);
            await this.storage.DeleteSupplierReturnAsync(existing);
            await AuditReturnAsync(existing.Id, "Deleted", $"Suppression soft retour fournisseur {existing.ReturnNumber}");
            return NoContent();
        }

        private async Task<string?> ValidateLinksAsync(SupplierReturn supplierReturn)
        {
            if (supplierReturn.PurchaseOrderId.HasValue && supplierReturn.PurchaseOrderId.Value > 0)
            {
                var po = await this.storage.SelectPurchaseOrderByIdAsync(supplierReturn.PurchaseOrderId.Value);
                if (po == null) return "Commande fournisseur liée introuvable.";
                if (po.SupplierId != supplierReturn.SupplierId) return "La commande liée doit appartenir au même fournisseur que le retour.";
            }

            if (supplierReturn.ReceiptId.HasValue && supplierReturn.ReceiptId.Value > 0)
            {
                var receipt = await this.storage.SelectReceiptByIdAsync(supplierReturn.ReceiptId.Value);
                if (receipt == null) return "Réception liée introuvable.";
                if (receipt.SupplierId != supplierReturn.SupplierId) return "La réception liée doit appartenir au même fournisseur que le retour.";
            }

            if (supplierReturn.SupplierInvoiceId.HasValue && supplierReturn.SupplierInvoiceId.Value > 0)
            {
                var invoice = await this.storage.SelectSupplierInvoiceByIdAsync(supplierReturn.SupplierInvoiceId.Value);
                if (invoice == null) return "Facture fournisseur liée introuvable.";
                if (invoice.SupplierId != supplierReturn.SupplierId) return "La facture liée doit appartenir au même fournisseur que le retour.";
            }

            return null;
        }

        /// <summary>RG-BRF2 : quantité retournée (hors Cancelled) plafonnée à la quantité reçue sur la CDF/BR liée.</summary>
        private async Task<string?> ValidateCapAsync(SupplierReturn supplierReturn, int? currentId = null)
        {
            Dictionary<string, decimal>? receivedByKey = null;

            if (supplierReturn.PurchaseOrderId.HasValue && supplierReturn.PurchaseOrderId.Value > 0)
            {
                var po = await this.storage.SelectPurchaseOrderByIdAsync(supplierReturn.PurchaseOrderId.Value);
                if (po != null)
                {
                    receivedByKey = po.Lines
                        .GroupBy(l => (l.ProductKey ?? string.Empty).Trim(), StringComparer.OrdinalIgnoreCase)
                        .ToDictionary(g => g.Key, g => g.Sum(l => l.ReceivedQuantity), StringComparer.OrdinalIgnoreCase);
                }
            }
            else if (supplierReturn.ReceiptId.HasValue && supplierReturn.ReceiptId.Value > 0)
            {
                var receipt = await this.storage.SelectReceiptByIdAsync(supplierReturn.ReceiptId.Value);
                if (receipt != null)
                {
                    receivedByKey = receipt.Lines
                        .GroupBy(l => (l.ProductKey ?? string.Empty).Trim(), StringComparer.OrdinalIgnoreCase)
                        .ToDictionary(g => g.Key, g => g.Sum(l => l.QuantityReceived), StringComparer.OrdinalIgnoreCase);
                }
            }

            if (receivedByKey == null) return null;

            var alreadyReturnedByKey = this.storage.SelectAllSupplierReturns()
                .Where(r =>
                    (supplierReturn.PurchaseOrderId.HasValue && r.PurchaseOrderId == supplierReturn.PurchaseOrderId) ||
                    (supplierReturn.ReceiptId.HasValue && r.ReceiptId == supplierReturn.ReceiptId))
                .AsEnumerable()
                .Where(r => !string.Equals(r.Status, "Cancelled", StringComparison.OrdinalIgnoreCase)
                    && (!currentId.HasValue || r.Id != currentId.Value))
                .SelectMany(r => r.Lines ?? new List<SupplierReturnLine>())
                .GroupBy(l => (l.ProductKey ?? string.Empty).Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Sum(l => l.Quantity), StringComparer.OrdinalIgnoreCase);

            foreach (var line in supplierReturn.Lines)
            {
                var key = (line.ProductKey ?? string.Empty).Trim();
                var received = receivedByKey.TryGetValue(key, out var r) ? r : 0m;
                var alreadyReturned = alreadyReturnedByKey.TryGetValue(key, out var ar) ? ar : 0m;
                if (alreadyReturned + line.Quantity > received + 0.0001m)
                {
                    return $"Quantité retournée pour '{key}' ({alreadyReturned + line.Quantity:0.####}) dépasse la quantité reçue ({received:0.####}).";
                }
            }

            return null;
        }

        private async Task ApplyStockOutAsync(SupplierReturn supplierReturn, string? companyId)
        {
            if (supplierReturn.StockApplied) return;

            foreach (var line in supplierReturn.Lines)
            {
                await StockLedger.ApplyAsync(
                    this.storage,
                    companyId,
                    line.ProductKey,
                    "Out",
                    line.Quantity,
                    $"BRF:{supplierReturn.ReturnNumber}",
                    $"Retour fournisseur {supplierReturn.ReturnNumber}",
                    User.Identity?.Name);
            }

            supplierReturn.StockApplied = true;
        }

        private static void RecalcTotals(SupplierReturn supplierReturn)
        {
            supplierReturn.Lines ??= new List<SupplierReturnLine>();
            for (var i = 0; i < supplierReturn.Lines.Count; i++)
            {
                var line = supplierReturn.Lines[i];
                line.LineNumber = line.LineNumber <= 0 ? i + 1 : line.LineNumber;
                line.TotalHT = line.Quantity * line.UnitPrice;
                line.TotalTTC = line.TotalHT * (1 + (line.VatRate / 100m));
            }

            supplierReturn.TotalHT = supplierReturn.Lines.Sum(l => l.TotalHT);
            supplierReturn.TotalVat = supplierReturn.Lines.Sum(l => l.TotalTTC - l.TotalHT);
            supplierReturn.TotalTTC = supplierReturn.Lines.Sum(l => l.TotalTTC);
        }

        private async Task AuditReturnAsync(int returnId, string action, string summary, string? details = null)
        {
            await SalesDocumentAudit.LogAsync(
                this.storage,
                this.companyContext.GetCurrentCompanyId(),
                "SupplierReturn",
                returnId,
                action,
                SalesDocumentAudit.ActorFrom(User),
                summary,
                details);
        }
    }
}
