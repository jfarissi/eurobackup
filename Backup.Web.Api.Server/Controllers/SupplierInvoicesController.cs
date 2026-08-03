using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Authorization;
using Authorize = Microsoft.AspNetCore.Authorization.AuthorizeAttribute;
using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Models;
using Backup.Web.Api.Server.Models.Entities;
using Backup.Web.Api.Server.Models.Security;
using Backup.Web.Api.Server.Services.Accounting;
using Backup.Web.Api.Server.Services.Documents;
using Backup.Web.Api.Server.Services.Documents.Parsing;
using Backup.Web.Api.Server.Services.Numbering;
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
    public class SupplierInvoicesController : RESTFulController
    {
        private readonly IStorageBroker storage;
        private readonly INumberingSequenceService numberingService;
        private readonly ICompanyContextService companyContext;
        private readonly ISupplierDocumentProductEnsureService productEnsure;

        public SupplierInvoicesController(
            IStorageBroker storage,
            INumberingSequenceService numberingService,
            ICompanyContextService companyContext,
            ISupplierDocumentProductEnsureService productEnsure)
        {
            this.storage = storage;
            this.numberingService = numberingService;
            this.companyContext = companyContext;
            this.productEnsure = productEnsure;
        }

        public class CreateFromDocumentRequest
        {
            public int DocumentId { get; set; }
            public int SupplierId { get; set; }
            public string? CompanyId { get; set; }
            public decimal DefaultVatRate { get; set; } = 21.0m;
            public bool UseDocumentNumberWhenAvailable { get; set; } = true;
            public bool ForceCreateNew { get; set; }
        }

        public class LinkDocumentRequest
        {
            public int DocumentId { get; set; }
        }

        public class MatchPurchaseOrderRequest
        {
            public int PurchaseOrderId { get; set; }
        }

        public class PurchaseOrderMatchResult
        {
            public SupplierInvoiceEntity Invoice { get; set; } = new();
            public PurchaseOrder PurchaseOrder { get; set; } = new();
            public decimal InvoiceTotalHt { get; set; }
            public decimal PurchaseOrderTotalHt { get; set; }
            public decimal TotalHtDelta { get; set; }
            public int MatchedLineCount { get; set; }
            public int MissingInvoiceLineCount { get; set; }
            public int MissingPurchaseOrderLineCount { get; set; }
            public int QuantityMismatchCount { get; set; }
            public int ReceivedQuantityMismatchCount { get; set; }
            public int PriceMismatchCount { get; set; }
            public bool IsBalanced { get; set; }
            public bool RequiresApproval { get; set; }
            public List<string> Warnings { get; set; } = new();
        }

        [HttpGet]
        [RequirePermission(Permissions.SupplierInvoiceRead)]
        public IActionResult GetAll([FromQuery] string? search = null, [FromQuery] int? supplierId = null, [FromQuery] int? documentId = null)
        {
            var query = this.storage.SelectAllSupplierInvoices().ForCompany(this.companyContext.GetCurrentCompanyId());

            if (supplierId.HasValue)
            {
                query = query.Where(i => i.SupplierId == supplierId.Value);
            }

            if (documentId.HasValue)
            {
                query = query.Where(i => i.DocumentId == documentId.Value);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.ToLowerInvariant();
                query = query.Where(i =>
                    i.InvoiceNumber.ToLower().Contains(s) ||
                    (i.Supplier != null && i.Supplier.Name.ToLower().Contains(s)) ||
                    (i.Notes != null && i.Notes.ToLower().Contains(s)));
            }

            return Ok(query.OrderByDescending(i => i.Date).ToList());
        }

        [HttpGet("{id:int}")]
        [RequirePermission(Permissions.SupplierInvoiceRead)]
        public async Task<IActionResult> GetById(int id)
        {
            var invoice = await this.storage.SelectSupplierInvoiceByIdAsync(id);
            if (invoice == null || !invoice.BelongsToCompany(this.companyContext.GetCurrentCompanyId())) return NotFound();
            return Ok(invoice);
        }

        [HttpPost]
        [RequirePermission(Permissions.SupplierInvoiceCreate)]
        public async Task<IActionResult> Post([FromBody] SupplierInvoiceEntity invoice)
        {
            invoice.EnsureCompanyId(this.companyContext.GetCurrentCompanyId());
            var validation = await this.ValidateSupplierInvoiceAsync(invoice);
            if (validation != null) return validation;

            await this.EnsureInvoiceNumberAsync(invoice, preferDocumentNumber: false);
            await this.ApplySupplierDueDateAsync(invoice);
            NormalizeSupplierInvoice(invoice);

            // RG-CP1 : devise figée à la création depuis Company.DefaultCurrencyCode.
            invoice.CurrencyCode = await SalesBusinessRules.ResolveCompanyCurrencyAsync(this.storage, invoice.CompanyId);

            var created = await this.storage.InsertSupplierInvoiceAsync(invoice);
            await this.AuditSupplierInvoice(created.Id, "Created", $"Création facture fournisseur {created.InvoiceNumber}");
            return Created(created);
        }

        [HttpPut("{id:int}")]
        [RequirePermission(Permissions.SupplierInvoiceCreate)]
        public async Task<IActionResult> Put(int id, [FromBody] SupplierInvoiceEntity invoice)
        {
            var existing = await this.storage.SelectSupplierInvoiceByIdAsync(id);
            if (existing == null || !existing.BelongsToCompany(this.companyContext.GetCurrentCompanyId())) return NotFound();

            // RG-FF4 : immuable après validation / comptabilisation.
            if (string.Equals(existing.Status, "Validated", StringComparison.OrdinalIgnoreCase)
                || string.Equals(existing.Status, "Paid", StringComparison.OrdinalIgnoreCase)
                || string.Equals(existing.Status, "PartiallyPaid", StringComparison.OrdinalIgnoreCase)
                || string.Equals(existing.Status, "Posted", StringComparison.OrdinalIgnoreCase)
                || AccountingLedger.HasPostedEntry(this.storage, AccountingLedger.RefSupplierInvoice, existing.Id, existing.CompanyId))
            {
                return BadRequest($"Une facture fournisseur au statut {existing.Status} ne peut plus être modifiée. Utilisez un avoir fournisseur.");
            }

            var validation = await this.ValidateSupplierInvoiceAsync(invoice, id);
            if (validation != null) return validation;

            existing.InvoiceNumber = string.IsNullOrWhiteSpace(invoice.InvoiceNumber) ? existing.InvoiceNumber : invoice.InvoiceNumber.Trim();
            existing.SupplierId = invoice.SupplierId;
            existing.DocumentId = invoice.DocumentId;
            existing.PurchaseOrderId = invoice.PurchaseOrderId;
            existing.Date = invoice.Date == default ? existing.Date : invoice.Date;
            existing.DueDate = invoice.DueDate == default ? existing.DueDate : invoice.DueDate;
            existing.DueDate = PaymentTermsHelper.EnsureNotBeforeInvoiceDate(existing.Date, existing.DueDate);
            existing.Status = string.IsNullOrWhiteSpace(invoice.Status) ? existing.Status : invoice.Status.Trim();
            existing.Notes = invoice.Notes;
            existing.CompanyId = invoice.CompanyId;
            existing.Lines = invoice.Lines ?? new List<SupplierInvoiceLineEntity>();

            NormalizeSupplierInvoice(existing);

            var updated = await this.storage.UpdateSupplierInvoiceAsync(existing);
            return Ok(updated);
        }

        [HttpPost("from-document")]
        [RequirePermission(Permissions.SupplierInvoiceCreate)]
        public async Task<IActionResult> CreateFromDocument([FromBody] CreateFromDocumentRequest request)
        {
            if (request.DocumentId <= 0) return BadRequest("DocumentId required");
            if (request.SupplierId <= 0) return BadRequest("SupplierId required");

            var supplier = await this.storage.SelectSupplierByIdAsync(request.SupplierId);
            if (supplier == null) return BadRequest("Supplier not found");

            var document = await this.storage.SelectDocumentByIdAsync(request.DocumentId);
            if (document == null) return NotFound("Document not found");

            var existingForDocument = this.storage.SelectAllSupplierInvoices()
                .ForCompany(this.companyContext.GetCurrentCompanyId())
                .FirstOrDefault(i => i.DocumentId == request.DocumentId);
            if (existingForDocument != null && request.ForceCreateNew == false)
            {
                return Conflict(new
                {
                    error = "A supplier invoice is already linked to this document.",
                    supplierInvoiceId = existingForDocument.Id
                });
            }

            var lines = this.storage.SelectLinesByDocumentId(request.DocumentId)
                .OrderBy(l => l.LineNumber)
                .ToList();

            var invoice = new SupplierInvoiceEntity
            {
                SupplierId = request.SupplierId,
                DocumentId = request.DocumentId,
                Date = document.DateDocument ?? DateTime.UtcNow,
                DueDate = default,
                Status = "Draft",
                Notes = BuildDocumentOriginNote(document),
                CompanyId = request.CompanyId ?? this.companyContext.GetCurrentCompanyId(),
                CreatedAt = DateTime.UtcNow,
                Lines = MapDocumentLines(lines, request.DefaultVatRate)
            };

            await this.EnsureInvoiceNumberAsync(invoice, request.UseDocumentNumberWhenAvailable, document);
            await this.ApplySupplierDueDateAsync(invoice);
            NormalizeSupplierInvoice(invoice);

            var created = await this.storage.InsertSupplierInvoiceAsync(invoice);
            return Created(created);
        }

        public class ComptabiliserRequest
        {
            public int DocumentId { get; set; }
            public int? SupplierId { get; set; }
            public int? PurchaseOrderId { get; set; }
            public string? CompanyId { get; set; }
            public decimal DefaultVatRate { get; set; } = 21m;
        }

        public class ComptabiliserResult
        {
            public SupplierInvoiceEntity Invoice { get; set; } = new();
            public List<string> Warnings { get; set; } = new();
        }

        /// <summary>
        /// Comptabiliser une facture fournisseur parsée (Documents) → SupplierInvoices (+ lignes).
        /// </summary>
        [HttpPost("comptabiliser")]
        [RequirePermission(Permissions.SupplierInvoiceCreate)]
        public async Task<IActionResult> Comptabiliser([FromBody] ComptabiliserRequest request)
        {
            if (request.DocumentId <= 0) return BadRequest("DocumentId required");

            var document = await this.storage.SelectDocumentByIdAsync(request.DocumentId);
            if (document == null) return NotFound("Document not found");

            if (!IsSupplierInvoiceDocument(document))
            {
                return BadRequest("Le document doit être une facture fournisseur (Facture).");
            }

            var existing = this.storage.SelectAllSupplierInvoices()
                .ForCompany(this.companyContext.GetCurrentCompanyId())
                .FirstOrDefault(i => i.DocumentId == request.DocumentId);
            if (existing != null)
            {
                return Conflict(new
                {
                    error = $"Cette facture est déjà comptabilisée ({existing.InvoiceNumber}).",
                    invoice = existing
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
                return BadRequest("Aucune ligne parsée sur cette facture.");
            }

            var ensureResult = await this.productEnsure.EnsureProductsForLinesAsync(
                documentLines,
                supplier.Name);

            var vat = request.DefaultVatRate > 0 ? request.DefaultVatRate : 21m;
            var invoice = new SupplierInvoiceEntity
            {
                SupplierId = supplierId.Value,
                DocumentId = request.DocumentId,
                PurchaseOrderId = request.PurchaseOrderId,
                Date = document.DateDocument ?? DateTime.UtcNow,
                DueDate = default,
                Status = "Validated",
                Notes = $"Facture fournisseur créée depuis le document #{request.DocumentId} ({document.OriginalFileName})",
                CompanyId = this.companyContext.GetCurrentCompanyId(),
                CreatedAt = DateTime.UtcNow,
                Lines = MapDocumentLines(documentLines, vat)
            };

            await this.EnsureInvoiceNumberAsync(invoice, preferDocumentNumber: true, document);
            invoice.DueDate = PaymentTermsHelper.ComputeDueDate(invoice.Date, supplier.PaymentTerms);
            NormalizeSupplierInvoice(invoice);

            var created = await this.storage.InsertSupplierInvoiceAsync(invoice);
            created = await this.storage.SelectSupplierInvoiceByIdAsync(created.Id) ?? created;
            await this.AuditSupplierInvoice(created.Id, "Created", $"Comptabilisation facture fournisseur {created.InvoiceNumber} ({created.Status})");

            var result = new ComptabiliserResult { Invoice = created };
            result.Warnings.AddRange(ensureResult.Warnings);

            if (request.PurchaseOrderId.HasValue)
            {
                var po = await this.storage.SelectPurchaseOrderByIdAsync(request.PurchaseOrderId.Value);
                if (po != null)
                {
                    var matchResult = BuildPurchaseOrderMatchResult(created, po);
                    if (!matchResult.IsBalanced)
                    {
                        result.Warnings.AddRange(matchResult.Warnings);
                        created.Status = "ApprovalRequired";
                        created = await this.storage.UpdateSupplierInvoiceAsync(created);
                        result.Invoice = created;
                    }

                    if (po.Status is "Received" or "PartiallyReceived")
                    {
                        po.Status = matchResult.IsBalanced ? "Invoiced" : "PartiallyInvoiced";
                    }

                    // RG-CF6 : cohérence qté commande/réception/facture — bump InvoicedQuantity lignes PO.
                    ApplyInvoicedQuantities(po, created);
                    await this.storage.UpdatePurchaseOrderAsync(po);
                }
            }

            var postError = await TryPostSupplierAccountingAsync(created);
            if (postError != null) result.Warnings.Add(postError);

            return Ok(result);
        }

        [HttpPost("{id:int}/link-document")]
        [RequirePermission(Permissions.SupplierInvoiceCreate)]
        public async Task<IActionResult> LinkDocument(int id, [FromBody] LinkDocumentRequest request)
        {
            if (request.DocumentId <= 0) return BadRequest("DocumentId required");

            var invoice = await this.storage.SelectSupplierInvoiceByIdAsync(id);
            if (invoice == null || !invoice.BelongsToCompany(this.companyContext.GetCurrentCompanyId())) return NotFound("Supplier invoice not found");

            var document = await this.storage.SelectDocumentByIdAsync(request.DocumentId);
            if (document == null) return NotFound("Document not found");

            var conflict = this.storage.SelectAllSupplierInvoices()
                .ForCompany(this.companyContext.GetCurrentCompanyId())
                .FirstOrDefault(i => i.DocumentId == request.DocumentId && i.Id != id);

            if (conflict != null)
            {
                return Conflict(new
                {
                    error = "This document is already linked to another supplier invoice.",
                    supplierInvoiceId = conflict.Id
                });
            }

            invoice.DocumentId = request.DocumentId;
            invoice.Date = invoice.Date == default ? (document.DateDocument ?? DateTime.UtcNow) : invoice.Date;
            invoice.Notes = AppendDocumentOriginNote(invoice.Notes, document);

            if (invoice.Lines == null || invoice.Lines.Count == 0)
            {
                var documentLines = this.storage.SelectLinesByDocumentId(request.DocumentId).OrderBy(l => l.LineNumber).ToList();
                invoice.Lines = MapDocumentLines(documentLines, 21.0m);
                NormalizeSupplierInvoice(invoice);
            }

            if (string.IsNullOrWhiteSpace(invoice.InvoiceNumber))
            {
                await this.EnsureInvoiceNumberAsync(invoice, preferDocumentNumber: true, document);
            }

            var updated = await this.storage.UpdateSupplierInvoiceAsync(invoice);
            return Ok(updated);
        }

        [HttpPost("{id:int}/match-purchase-order")]
        [RequirePermission(Permissions.SupplierInvoiceCreate)]
        public async Task<IActionResult> MatchPurchaseOrder(int id, [FromBody] MatchPurchaseOrderRequest request)
        {
            if (request.PurchaseOrderId <= 0) return BadRequest("PurchaseOrderId required");

            var validation = await ValidateMatchPurchaseOrderRequestAsync(id, request.PurchaseOrderId);
            if (validation.ErrorResult != null) return validation.ErrorResult;

            var invoice = validation.Invoice!;
            var purchaseOrder = validation.PurchaseOrder!;

            var matchResult = BuildPurchaseOrderMatchResult(invoice, purchaseOrder);

            var invoiceLinkNote = $"Linked to purchase order #{purchaseOrder.Id}";
            if (!string.IsNullOrWhiteSpace(invoice.Notes) &&
                invoice.Notes.Contains(invoiceLinkNote, StringComparison.OrdinalIgnoreCase))
            {
                return Conflict(new
                {
                    error = $"Supplier invoice #{invoice.Id} is already linked to purchase order #{purchaseOrder.Id}."
                });
            }

            invoice.Notes = string.IsNullOrWhiteSpace(invoice.Notes)
                ? invoiceLinkNote
                : $"{invoice.Notes}{Environment.NewLine}{invoiceLinkNote}";
            invoice.PurchaseOrderId = purchaseOrder.Id;

            var purchaseOrderLinkNote = $"Linked to supplier invoice #{invoice.Id}";
            purchaseOrder.Notes = string.IsNullOrWhiteSpace(purchaseOrder.Notes)
                ? purchaseOrderLinkNote
                : $"{purchaseOrder.Notes}{Environment.NewLine}{purchaseOrderLinkNote}";

            if (purchaseOrder.Status is "Received" or "PartiallyReceived")
            {
                purchaseOrder.Status = matchResult.IsBalanced ? "Invoiced" : "PartiallyInvoiced";
            }

            // RG-CF6 : cohérence qté commande/réception/facture — bump InvoicedQuantity lignes PO.
            ApplyInvoicedQuantities(purchaseOrder, invoice);

            // RG-AC4/AC5 : matching 3 voies — écart → ApprovalRequired
            if (matchResult.IsBalanced)
            {
                if (string.Equals(invoice.Status, "Draft", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(invoice.Status, "ApprovalRequired", StringComparison.OrdinalIgnoreCase))
                {
                    invoice.Status = "Matched";
                }
            }
            else
            {
                invoice.Status = "ApprovalRequired";
                matchResult.RequiresApproval = true;
            }

            NormalizeSupplierInvoice(invoice);

            var updatedInvoice = await this.storage.UpdateSupplierInvoiceAsync(invoice);
            var updatedPurchaseOrder = await this.storage.UpdatePurchaseOrderAsync(purchaseOrder);
            await this.AuditSupplierInvoice(updatedInvoice.Id, "Matched", $"Rapprochement facture {updatedInvoice.InvoiceNumber} ↔ CDF {updatedPurchaseOrder.OrderNumber}");

            var postError = await TryPostSupplierAccountingAsync(updatedInvoice);
            if (postError != null) matchResult.Warnings.Add(postError);

            matchResult.Invoice = updatedInvoice;
            matchResult.PurchaseOrder = updatedPurchaseOrder;

            return Ok(matchResult);
        }

        /// <summary>RG-AC5 : validation manuelle après écart de matching.</summary>
        [HttpPost("{id:int}/approve")]
        [RequirePermission(Permissions.SupplierInvoiceCreate)]
        public async Task<IActionResult> Approve(int id, [FromBody] CancelOrApproveRequest? request)
        {
            var invoice = await this.storage.SelectSupplierInvoiceByIdAsync(id);
            if (invoice == null || !invoice.BelongsToCompany(this.companyContext.GetCurrentCompanyId())) return NotFound();
            if (!string.Equals(invoice.Status, "ApprovalRequired", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(invoice.Status, "Draft", StringComparison.OrdinalIgnoreCase))
            {
                return Conflict(new { error = $"Facture au statut {invoice.Status} — approbation non applicable." });
            }

            invoice.Status = "Validated";
            var note = string.IsNullOrWhiteSpace(request?.Reason)
                ? "Approuvée manuellement (écart matching)"
                : $"Approuvée manuellement : {request!.Reason!.Trim()}";
            invoice.Notes = string.IsNullOrWhiteSpace(invoice.Notes) ? note : $"{invoice.Notes}\n{note}";
            NormalizeSupplierInvoice(invoice);
            var updated = await this.storage.UpdateSupplierInvoiceAsync(invoice);
            await this.AuditSupplierInvoice(updated.Id, "Validated", $"Approbation facture fournisseur {updated.InvoiceNumber}");

            var postError = await TryPostSupplierAccountingAsync(updated);
            if (postError != null) return BadRequest(postError);

            return Ok(updated);
        }

        public class CancelOrApproveRequest
        {
            public string? Reason { get; set; }
        }

        [HttpPost("{id:int}/preview-match-purchase-order")]
        [RequirePermission(Permissions.SupplierInvoiceRead)]
        public async Task<IActionResult> PreviewMatchPurchaseOrder(int id, [FromBody] MatchPurchaseOrderRequest request)
        {
            if (request.PurchaseOrderId <= 0) return BadRequest("PurchaseOrderId required");

            var validation = await ValidateMatchPurchaseOrderRequestAsync(id, request.PurchaseOrderId);
            if (validation.ErrorResult != null) return validation.ErrorResult;

            var matchResult = BuildPurchaseOrderMatchResult(validation.Invoice!, validation.PurchaseOrder!);
            return Ok(matchResult);
        }

        private async Task<IActionResult?> ValidateSupplierInvoiceAsync(SupplierInvoiceEntity invoice, int? currentId = null)
        {
            if (invoice.SupplierId <= 0) return BadRequest("SupplierId required");

            var supplier = await this.storage.SelectSupplierByIdAsync(invoice.SupplierId);
            if (supplier == null) return BadRequest("Supplier not found");

            if (invoice.DocumentId.HasValue)
            {
                var document = await this.storage.SelectDocumentByIdAsync(invoice.DocumentId.Value);
                if (document == null) return BadRequest("Linked document not found");

                var duplicateDocumentLink = this.storage.SelectAllSupplierInvoices()
                    .ForCompany(this.companyContext.GetCurrentCompanyId())
                    .FirstOrDefault(i => i.DocumentId == invoice.DocumentId && (!currentId.HasValue || i.Id != currentId.Value));

                if (duplicateDocumentLink != null)
                {
                    return Conflict(new
                    {
                        error = "This document is already linked to another supplier invoice.",
                        supplierInvoiceId = duplicateDocumentLink.Id
                    });
                }
            }

            if (invoice.PurchaseOrderId.HasValue)
            {
                var purchaseOrder = await this.storage.SelectPurchaseOrderByIdAsync(invoice.PurchaseOrderId.Value);
                if (purchaseOrder == null) return BadRequest("Linked purchase order not found");

                if (purchaseOrder.SupplierId != invoice.SupplierId)
                {
                    return BadRequest("Linked purchase order must belong to the same supplier.");
                }
            }

            if (!string.IsNullOrWhiteSpace(invoice.InvoiceNumber))
            {
                var normalizedNumber = invoice.InvoiceNumber.Trim().ToLowerInvariant();
                var duplicateNumber = this.storage.SelectAllSupplierInvoices()
                    .ForCompany(this.companyContext.GetCurrentCompanyId())
                    .FirstOrDefault(i =>
                        i.SupplierId == invoice.SupplierId &&
                        i.InvoiceNumber.ToLower() == normalizedNumber &&
                        (!currentId.HasValue || i.Id != currentId.Value));

                if (duplicateNumber != null)
                {
                    return Conflict(new
                    {
                        error = "A supplier invoice with the same number already exists for this supplier.",
                        supplierInvoiceId = duplicateNumber.Id
                    });
                }
            }

            return null;
        }

        private async Task EnsureInvoiceNumberAsync(
            SupplierInvoiceEntity invoice,
            bool preferDocumentNumber,
            Document? document = null)
        {
            if (!string.IsNullOrWhiteSpace(invoice.InvoiceNumber))
            {
                invoice.InvoiceNumber = invoice.InvoiceNumber.Trim();
                return;
            }

            if (preferDocumentNumber && !string.IsNullOrWhiteSpace(document?.Numero))
            {
                invoice.InvoiceNumber = document.Numero.Trim();
                return;
            }

            invoice.InvoiceNumber = await this.numberingService.GetNextNumberAsync("SupplierInvoice", invoice.CompanyId);
        }

        private async Task ApplySupplierDueDateAsync(SupplierInvoiceEntity invoice)
        {
            if (invoice.DueDate != default)
            {
                invoice.DueDate = PaymentTermsHelper.EnsureNotBeforeInvoiceDate(
                    invoice.Date == default ? DateTime.UtcNow : invoice.Date,
                    invoice.DueDate);
                return;
            }

            string? terms = null;
            if (invoice.SupplierId > 0)
            {
                var supplier = await this.storage.SelectSupplierByIdAsync(invoice.SupplierId);
                terms = supplier?.PaymentTerms;
            }

            var date = invoice.Date == default ? DateTime.UtcNow : invoice.Date;
            invoice.DueDate = PaymentTermsHelper.ComputeDueDate(date, terms);
        }

        private static void NormalizeSupplierInvoice(SupplierInvoiceEntity invoice)
        {
            invoice.Date = invoice.Date == default ? DateTime.UtcNow : invoice.Date;
            invoice.DueDate = invoice.DueDate == default ? invoice.Date.AddDays(30) : invoice.DueDate;
            invoice.DueDate = PaymentTermsHelper.EnsureNotBeforeInvoiceDate(invoice.Date, invoice.DueDate);
            invoice.CreatedAt = invoice.CreatedAt == default ? DateTime.UtcNow : invoice.CreatedAt;
            invoice.Status = string.IsNullOrWhiteSpace(invoice.Status) ? "Draft" : invoice.Status.Trim();
            invoice.Lines ??= new List<SupplierInvoiceLineEntity>();

            for (int i = 0; i < invoice.Lines.Count; i++)
            {
                var line = invoice.Lines[i];
                line.LineNumber = line.LineNumber <= 0 ? i + 1 : line.LineNumber;
                line.Description = line.Description?.Trim() ?? string.Empty;
                line.ProductKey = line.ProductKey?.Trim() ?? string.Empty;
                line.TotalHT = line.Quantity * line.UnitPrice;
                line.TotalTTC = line.TotalHT * (1 + (line.VatRate / 100m));
            }

            invoice.TotalHT = invoice.Lines.Sum(l => l.TotalHT);
            invoice.TotalVat = invoice.Lines.Sum(l => l.TotalTTC - l.TotalHT);
            invoice.TotalTTC = invoice.Lines.Sum(l => l.TotalTTC);
        }

        private static List<SupplierInvoiceLineEntity> MapDocumentLines(IEnumerable<DocumentLine> lines, decimal defaultVatRate)
        {
            return lines.Select((line, index) =>
            {
                var quantity = line.Quantity == 0 ? 1m : line.Quantity;
                var unitPrice = line.UnitPrice;
                var totalHt = line.TotalValue != 0 ? line.TotalValue : quantity * unitPrice;
                if (unitPrice == 0 && quantity != 0 && totalHt != 0)
                {
                    unitPrice = totalHt / quantity;
                }

                return new SupplierInvoiceLineEntity
                {
                    ProductKey = ProductKeyHelper.Normalize(ProductKeyHelper.GetProductKey(line)),
                    Description = string.IsNullOrWhiteSpace(line.Product) ? (line.RawLine ?? string.Empty) : line.Product,
                    Quantity = quantity,
                    UnitPrice = unitPrice,
                    VatRate = defaultVatRate,
                    TotalHT = totalHt,
                    TotalTTC = totalHt * (1 + (defaultVatRate / 100m)),
                    LineNumber = line.LineNumber > 0 ? line.LineNumber : index + 1
                };
            }).ToList();
        }

        private static string BuildDocumentOriginNote(Document document)
        {
            var parts = new List<string> { $"Imported from document #{document.Id}" };

            if (!string.IsNullOrWhiteSpace(document.OriginalFileName))
            {
                parts.Add(document.OriginalFileName);
            }

            if (!string.IsNullOrWhiteSpace(document.Supplier))
            {
                parts.Add($"Supplier: {document.Supplier}");
            }

            return string.Join(" | ", parts);
        }

        private static string AppendDocumentOriginNote(string? existingNotes, Document document)
        {
            var documentNote = BuildDocumentOriginNote(document);

            if (string.IsNullOrWhiteSpace(existingNotes))
            {
                return documentNote;
            }

            if (existingNotes.Contains(documentNote, StringComparison.OrdinalIgnoreCase))
            {
                return existingNotes;
            }

            return $"{existingNotes}{Environment.NewLine}{documentNote}";
        }

        private async Task<(SupplierInvoiceEntity? Invoice, PurchaseOrder? PurchaseOrder, IActionResult? ErrorResult)> ValidateMatchPurchaseOrderRequestAsync(
            int invoiceId,
            int purchaseOrderId)
        {
            var invoice = await this.storage.SelectSupplierInvoiceByIdAsync(invoiceId);
            if (invoice == null || !invoice.BelongsToCompany(this.companyContext.GetCurrentCompanyId())) return (null, null, NotFound("Supplier invoice not found"));

            var purchaseOrder = await this.storage.SelectPurchaseOrderByIdAsync(purchaseOrderId);
            if (purchaseOrder == null) return (invoice, null, NotFound("Purchase order not found"));

            if (invoice.SupplierId != purchaseOrder.SupplierId)
            {
                return (invoice, purchaseOrder, BadRequest("Supplier invoice and purchase order must belong to the same supplier."));
            }

            return (invoice, purchaseOrder, null);
        }

        private static PurchaseOrderMatchResult BuildPurchaseOrderMatchResult(SupplierInvoiceEntity invoice, PurchaseOrder purchaseOrder)
        {
            const decimal tolerance = 0.01m;

            var result = new PurchaseOrderMatchResult
            {
                Invoice = invoice,
                PurchaseOrder = purchaseOrder,
                InvoiceTotalHt = invoice.TotalHT,
                PurchaseOrderTotalHt = purchaseOrder.TotalHT,
                TotalHtDelta = Math.Round(invoice.TotalHT - purchaseOrder.TotalHT, 2)
            };

            var invoiceLinesByKey = invoice.Lines
                .GroupBy(line => ProductKeyHelper.Normalize(ProductKeyHelper.GetProductKey(line.ProductKey, null, line.Description)), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

            var purchaseOrderLinesByKey = purchaseOrder.Lines
                .GroupBy(line => ProductKeyHelper.Normalize(ProductKeyHelper.GetProductKey(line.ProductKey, null, line.Description)), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

            foreach (var purchaseOrderEntry in purchaseOrderLinesByKey)
            {
                if (!invoiceLinesByKey.TryGetValue(purchaseOrderEntry.Key, out var invoiceLines))
                {
                    result.MissingInvoiceLineCount += purchaseOrderEntry.Value.Count;
                    result.Warnings.Add($"Missing invoice line for product '{purchaseOrderEntry.Key}'.");
                    continue;
                }

                result.MatchedLineCount += Math.Min(purchaseOrderEntry.Value.Count, invoiceLines.Count);

                var purchaseQty = purchaseOrderEntry.Value.Sum(line => line.Quantity);
                var receivedQty = purchaseOrderEntry.Value.Sum(line => line.ReceivedQuantity);
                var invoiceQty = invoiceLines.Sum(line => line.Quantity);

                if (Math.Abs(purchaseQty - invoiceQty) > tolerance)
                {
                    result.QuantityMismatchCount++;
                    result.Warnings.Add($"Écart qté PO/facture pour '{purchaseOrderEntry.Key}': PO {purchaseQty} vs facture {invoiceQty}.");
                }

                // RG-AC4 : 3e voie = réception (GRN via ReceivedQuantity)
                if (Math.Abs(receivedQty - invoiceQty) > tolerance || Math.Abs(receivedQty - purchaseQty) > tolerance)
                {
                    result.ReceivedQuantityMismatchCount++;
                    result.Warnings.Add($"Écart qté réception pour '{purchaseOrderEntry.Key}': PO {purchaseQty} / reçu {receivedQty} / facture {invoiceQty}.");
                }

                var purchasePrice = purchaseOrderEntry.Value.Average(line => line.UnitPrice);
                var invoicePrice = invoiceLines.Average(line => line.UnitPrice);
                if (Math.Abs(purchasePrice - invoicePrice) > tolerance)
                {
                    result.PriceMismatchCount++;
                    result.Warnings.Add($"Écart prix pour '{purchaseOrderEntry.Key}': PO {purchasePrice:0.##} vs facture {invoicePrice:0.##}.");
                }
            }

            foreach (var invoiceEntry in invoiceLinesByKey)
            {
                if (!purchaseOrderLinesByKey.ContainsKey(invoiceEntry.Key))
                {
                    result.MissingPurchaseOrderLineCount += invoiceEntry.Value.Count;
                    result.Warnings.Add($"Ligne facture '{invoiceEntry.Key}' absente de la commande.");
                }
            }

            if (Math.Abs(result.TotalHtDelta) > tolerance)
            {
                result.Warnings.Add($"Écart total HT : {result.TotalHtDelta:0.##}.");
            }

            result.IsBalanced =
                result.MissingInvoiceLineCount == 0 &&
                result.MissingPurchaseOrderLineCount == 0 &&
                result.QuantityMismatchCount == 0 &&
                result.ReceivedQuantityMismatchCount == 0 &&
                result.PriceMismatchCount == 0 &&
                Math.Abs(result.TotalHtDelta) <= tolerance;
            result.RequiresApproval = !result.IsBalanced;

            return result;
        }

        private async Task<string?> TryPostSupplierAccountingAsync(SupplierInvoiceEntity invoice)
        {
            if (!string.Equals(invoice.Status, "Validated", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(invoice.Status, "Matched", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (AccountingLedger.HasPostedEntry(this.storage, AccountingLedger.RefSupplierInvoice, invoice.Id, invoice.CompanyId))
                return null;

            var (_, error) = await AccountingLedger.PostSupplierInvoiceAsync(
                this.storage, this.numberingService, invoice, User.Identity?.Name);
            return error;
        }

        /// <summary>RG-CF6 : cohérence qté commande/réception/facture — cumule l'InvoicedQuantity par ligne PO (cap Quantity).</summary>
        private static void ApplyInvoicedQuantities(PurchaseOrder purchaseOrder, SupplierInvoiceEntity invoice)
        {
            var invoicedByKey = invoice.Lines
                .GroupBy(l => ProductKeyHelper.Normalize(ProductKeyHelper.GetProductKey(l.ProductKey, null, l.Description)), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity), StringComparer.OrdinalIgnoreCase);

            foreach (var line in purchaseOrder.Lines)
            {
                var key = ProductKeyHelper.Normalize(ProductKeyHelper.GetProductKey(line.ProductKey, null, line.Description));
                if (!invoicedByKey.TryGetValue(key, out var qty)) continue;
                line.InvoicedQuantity = Math.Min(line.Quantity, line.InvoicedQuantity + qty);
            }
        }

        private async Task AuditSupplierInvoice(int invoiceId, string action, string summary, string? details = null)
        {
            await SalesDocumentAudit.LogAsync(
                this.storage,
                this.companyContext.GetCurrentCompanyId(),
                "SupplierInvoice",
                invoiceId,
                action,
                SalesDocumentAudit.ActorFrom(User),
                summary,
                details);
        }

        private static bool IsSupplierInvoiceDocument(Document document)
        {
            var type = (document.TypeDocument ?? string.Empty).Trim().ToLowerInvariant();
            return type is "facture" or "invoice" or "fa" ||
                   type.Contains("facture") ||
                   type.Contains("invoice");
        }
    }
}
