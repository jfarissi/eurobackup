using System;
using System.Linq;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Authorization;
using Authorize = Microsoft.AspNetCore.Authorization.AuthorizeAttribute;
using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Models.Entities;
using Backup.Web.Api.Server.Models.Security;
using Backup.Web.Api.Server.Services.Accounting;
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
            var list = query.OrderByDescending(i => i.Date).ToList();
            var ids = list.Select(i => i.Id).ToList();
            var linkedIds = this.storage.SelectAllSalesDeliveryNotes()
                .Where(n => n.SalesInvoiceId != null
                    && ids.Contains(n.SalesInvoiceId.Value)
                    && (n.Status == "Delivered" || n.Status == "Invoiced"))
                .Select(n => n.SalesInvoiceId!.Value)
                .Distinct()
                .ToHashSet();
            foreach (var invoice in list)
            {
                SalesInvoiceSettlement.Enrich(invoice, this.storage, linkedIds.Contains(invoice.Id));
                invoice.IsOverdue = ComputeIsOverdue(invoice);
            }
            return Ok(list);
        }

        /// <summary>RG-RG9 lite : factures échues non soldées (Validated/PartiallyPaid, DueDate dépassée, reste dû &gt; 0).</summary>
        [HttpGet("overdue")]
        [RequirePermission(Permissions.InvoiceRead)]
        public IActionResult GetOverdue()
        {
            var list = this.storage.SelectAllSalesInvoices()
                .ForCompany(this.companyContext.GetCurrentCompanyId())
                .OrderBy(i => i.DueDate)
                .ToList();
            foreach (var invoice in list)
                SalesInvoiceSettlement.Enrich(invoice, this.storage);
            var overdue = list.Where(ComputeIsOverdue).ToList();
            foreach (var invoice in overdue) invoice.IsOverdue = true;
            return Ok(overdue);
        }

        /// <summary>RG-RG9 lite : Validated/PartiallyPaid + DueDate dépassée + reste dû &gt; 0.</summary>
        private static bool ComputeIsOverdue(SalesInvoice invoice)
        {
            var isEligibleStatus = string.Equals(invoice.Status, "Validated", StringComparison.OrdinalIgnoreCase)
                || string.Equals(invoice.Status, "PartiallyPaid", StringComparison.OrdinalIgnoreCase)
                || string.Equals(invoice.Status, "Reminded", StringComparison.OrdinalIgnoreCase);
            if (!isEligibleStatus) return false;
            if (invoice.DueDate.Date >= DateTime.UtcNow.Date) return false;
            return invoice.RemainingAmount > 0.01m;
        }

        [HttpGet("{id:int}")]
        [RequirePermission(Permissions.InvoiceRead)]
        public async Task<IActionResult> GetById(int id)
        {
            var invoice = await this.storage.SelectSalesInvoiceByIdAsync(id);
            if (invoice == null || !invoice.BelongsToCompany(this.companyContext.GetCurrentCompanyId())) return NotFound();
            SalesInvoiceSettlement.Enrich(invoice, this.storage);
            invoice.IsOverdue = ComputeIsOverdue(invoice);
            return Ok(invoice);
        }

        /// <summary>RG-RG9 lite : relance manuelle — ajoute une note, audite "Reminded" et bascule le statut Validated → Reminded.</summary>
        [HttpPost("{id:int}/remind")]
        [RequirePermission(Permissions.InvoiceUpdate)]
        public async Task<IActionResult> Remind(int id)
        {
            var invoice = await this.storage.SelectSalesInvoiceByIdAsync(id);
            if (invoice == null || !invoice.BelongsToCompany(this.companyContext.GetCurrentCompanyId())) return NotFound();
            SalesInvoiceSettlement.Enrich(invoice, this.storage);

            if (!ComputeIsOverdue(invoice))
                return BadRequest("Cette facture n'est pas en retard de paiement (statut, échéance ou reste dû non éligibles).");

            var note = $"Relance envoyée le {DateTime.UtcNow:dd/MM/yyyy HH:mm} UTC (échéance {invoice.DueDate:dd/MM/yyyy}).";
            invoice.Notes = string.IsNullOrWhiteSpace(invoice.Notes) ? note : $"{invoice.Notes}{Environment.NewLine}{note}";

            // Ne pas écraser un statut PartiallyPaid déjà informatif ; seul Validated bascule en Reminded.
            if (string.Equals(invoice.Status, "Validated", StringComparison.OrdinalIgnoreCase))
                invoice.Status = "Reminded";

            var updated = await this.storage.UpdateSalesInvoiceAsync(invoice);
            await SalesDocumentAudit.LogAsync(
                this.storage,
                updated.CompanyId ?? this.companyContext.GetCurrentCompanyId(),
                "Invoice",
                updated.Id,
                "Reminded",
                SalesDocumentAudit.ActorFrom(User),
                $"Relance facture {updated.InvoiceNumber}");

            SalesInvoiceSettlement.Enrich(updated, this.storage);
            updated.IsOverdue = ComputeIsOverdue(updated);
            return Ok(updated);
        }

        [HttpPost]
        [RequirePermission(Permissions.InvoiceCreate)]
        public async Task<IActionResult> Post([FromBody] SalesInvoice invoice)
        {
            invoice.EnsureCompanyId(this.companyContext.GetCurrentCompanyId());
            var companyId = this.companyContext.GetCurrentCompanyId();

            // RG-BL7 / FC1 : un ou plusieurs BL livrés obligatoires.
            var noteIds = new System.Collections.Generic.List<int>();
            if (invoice.SalesDeliveryNoteIds != null)
            {
                foreach (var id in invoice.SalesDeliveryNoteIds)
                {
                    if (id > 0 && !noteIds.Contains(id)) noteIds.Add(id);
                }
            }
            if (invoice.SalesDeliveryNoteId.HasValue && invoice.SalesDeliveryNoteId.Value > 0
                && !noteIds.Contains(invoice.SalesDeliveryNoteId.Value))
            {
                noteIds.Add(invoice.SalesDeliveryNoteId.Value);
            }

            invoice.SalesDeliveryNoteId = null;
            invoice.SalesDeliveryNoteIds = null;

            if (noteIds.Count == 0)
            {
                if (invoice.SalesOrderId.HasValue)
                    return BadRequest("Facturation directe depuis une commande interdite. Créez d'abord un BL livré, puis facturez ce(s) BL.");
                return BadRequest("Une facture doit être liée à au moins un bon de livraison livré (parcours Commande → BL → Facture).");
            }

            var linkedNotes = new System.Collections.Generic.List<SalesDeliveryNote>();
            foreach (var noteId in noteIds)
            {
                var note = await this.storage.SelectSalesDeliveryNoteByIdAsync(noteId);
                if (note == null || !note.BelongsToCompany(companyId))
                    return BadRequest($"Bon de livraison #{noteId} introuvable.");
                if (string.Equals(note.Status, "Cancelled", StringComparison.OrdinalIgnoreCase))
                    return BadRequest($"Le BL {note.DeliveryNumber} est annulé.");
                if (!string.Equals(note.Status, "Delivered", StringComparison.OrdinalIgnoreCase))
                    return BadRequest($"Le BL {note.DeliveryNumber} doit être validé (livré) avant facturation.");
                if (note.SalesInvoiceId.HasValue
                    || string.Equals(note.Status, "Invoiced", StringComparison.OrdinalIgnoreCase))
                    return Conflict(new { error = $"Le BL {note.DeliveryNumber} a déjà été facturé (facture #{note.SalesInvoiceId})." });

                var dnCustomerError = SalesBusinessRules.ValidateSameCustomer(
                    note.CustomerId, invoice.CustomerId, "BL → facture");
                if (dnCustomerError != null) return BadRequest(dnCustomerError);

                linkedNotes.Add(note);
            }

            if (linkedNotes.Select(n => n.CustomerId).Distinct().Count() > 1)
                return BadRequest("Tous les BL regroupés doivent concerner le même client.");

            // Prefer order from first BL that has one (same-order regroup is typical).
            var orderIdFromNotes = linkedNotes.Select(n => n.SalesOrderId).FirstOrDefault(id => id.HasValue);
            if (orderIdFromNotes.HasValue)
                invoice.SalesOrderId = orderIdFromNotes;

            SalesOrder? linkedOrder = null;
            if (invoice.SalesOrderId.HasValue)
            {
                linkedOrder = await this.storage.SelectSalesOrderByIdAsync(invoice.SalesOrderId.Value);
                if (linkedOrder == null || !linkedOrder.BelongsToCompany(companyId))
                    return BadRequest("Commande liée introuvable.");
                if (string.Equals(linkedOrder.Status, "Cancelled", StringComparison.OrdinalIgnoreCase))
                    return BadRequest("Une commande annulée ne peut pas être facturée.");
                if (string.Equals(linkedOrder.Status, "Invoiced", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(linkedOrder.Status, "Closed", StringComparison.OrdinalIgnoreCase))
                    return BadRequest("Cette commande est déjà entièrement facturée.");

                var customerError = SalesBusinessRules.ValidateSameCustomer(
                    linkedOrder.CustomerId, invoice.CustomerId, "commande → facture");
                if (customerError != null) return BadRequest(customerError);
            }

            if (string.IsNullOrWhiteSpace(invoice.InvoiceNumber)
                || ProvisionalDocumentNumber.IsProvisional(invoice.InvoiceNumber))
            {
                // RG-FC6 : brouillon = n° provisoire (ne consomme pas la séquence FAC-).
                invoice.InvoiceNumber = ProvisionalDocumentNumber.Create();
            }
            invoice.Date = invoice.Date == default ? DateTime.UtcNow : invoice.Date;

            // RG-EC1 / EC2 : échéance depuis conditions client.
            var customer = await this.storage.SelectCustomerByIdAsync(invoice.CustomerId);
            if (customer == null) return BadRequest("Client introuvable.");
            var partyErr = SalesBusinessRules.RejectIfPartyNotActive(customer.Status, customer.Name);
            if (partyErr != null) return BadRequest(partyErr);
            if (invoice.DueDate == default)
                invoice.DueDate = PaymentTermsHelper.ComputeDueDate(invoice.Date, customer.PaymentTerms);
            else
                invoice.DueDate = PaymentTermsHelper.EnsureNotBeforeInvoiceDate(invoice.Date, invoice.DueDate);

            invoice.CreatedAt = DateTime.UtcNow;

            // Si lignes vides et multi-BL : fusionner les lignes livrées.
            if ((invoice.Lines == null || invoice.Lines.Count == 0) && linkedNotes.Count > 0)
            {
                invoice.Lines = MergeDeliveryNoteLines(linkedNotes);
            }

            if (invoice.Lines == null || invoice.Lines.Count == 0)
                return BadRequest("La facture doit contenir au moins une ligne.");

            // RG-FC7 : TVA par défaut selon le pays du client si non renseignée sur la ligne.
            var defaultVatRate = VatLocalization.DefaultRateForCountry(customer.Country);
            foreach (var line in invoice.Lines)
            {
                if (line.VatRate <= 0) line.VatRate = defaultVatRate;
                var discountErr = SalesBusinessRules.ValidateDiscountPercent(line.DiscountPercent, $"ligne {line.ProductKey}");
                if (discountErr != null) return BadRequest(discountErr);
            }
            var headerDiscountErr = SalesBusinessRules.ValidateDiscountPercent(invoice.HeaderDiscountPercent, "remise pied de page");
            if (headerDiscountErr != null) return BadRequest(headerDiscountErr);

            // RG-CP1 : devise figée à la création depuis Company.DefaultCurrencyCode.
            var invoiceCompany = await this.storage.SelectCompanyByIdAsync(invoice.CompanyId);
            invoice.CurrencyCode = string.IsNullOrWhiteSpace(invoiceCompany?.DefaultCurrencyCode) ? "EUR" : invoiceCompany!.DefaultCurrencyCode;

            SalesBusinessRules.RecalculateInvoiceTotals(invoice);

            foreach (var line in invoice.Lines)
            {
                if (line.DeliveredQuantity <= 0 && line.Quantity > 0)
                    line.DeliveredQuantity = line.Quantity;
                if (line.OrderedQuantity <= 0 && line.Quantity > 0)
                    line.OrderedQuantity = line.Quantity;
            }

            // Enrichir Ordered/Delivered (+ n° de lot, RG-LS1–5 lite) depuis les BL sources.
            foreach (var line in invoice.Lines)
            {
                decimal ordered = 0m, delivered = 0m;
                foreach (var note in linkedNotes)
                {
                    var src = note.Lines.FirstOrDefault(l =>
                        string.Equals(l.ProductKey?.Trim(), line.ProductKey?.Trim(), StringComparison.OrdinalIgnoreCase));
                    if (src == null) continue;
                    ordered += src.OrderedQuantity > 0 ? src.OrderedQuantity : src.DeliveredQuantity;
                    delivered += src.DeliveredQuantity > 0 ? src.DeliveredQuantity : 0m;
                    if (string.IsNullOrWhiteSpace(line.LotNumber) && !string.IsNullOrWhiteSpace(src.LotNumber))
                        line.LotNumber = src.LotNumber;
                }
                if (line.OrderedQuantity <= 0 && ordered > 0) line.OrderedQuantity = ordered;
                if (delivered > 0) line.DeliveredQuantity = delivered;
            }

            if (linkedNotes.Count > 1)
            {
                var nums = string.Join(", ", linkedNotes.Select(n => n.DeliveryNumber));
                invoice.Notes = string.IsNullOrWhiteSpace(invoice.Notes)
                    ? $"Facture regroupée depuis BL : {nums}"
                    : $"{invoice.Notes}{Environment.NewLine}BL regroupés : {nums}";
            }

            var created = await this.storage.InsertSalesInvoiceAsync(invoice);
            if (string.IsNullOrWhiteSpace(invoice.Status)) created.Status = "Draft";

            // Lier tous les BL + sync qté facturées par commande.
            var orderDeltas = new System.Collections.Generic.Dictionary<int, System.Collections.Generic.List<(string ProductKey, decimal Qty)>>();
            foreach (var note in linkedNotes)
            {
                note.SalesInvoiceId = created.Id;
                note.Status = "Invoiced";
                await this.storage.UpdateSalesDeliveryNoteAsync(note);

                if (!note.SalesOrderId.HasValue) continue;
                if (!orderDeltas.TryGetValue(note.SalesOrderId.Value, out var list))
                {
                    list = new System.Collections.Generic.List<(string, decimal)>();
                    orderDeltas[note.SalesOrderId.Value] = list;
                }
                foreach (var l in note.Lines)
                    list.Add((l.ProductKey, l.DeliveredQuantity));
            }

            foreach (var (orderId, lines) in orderDeltas)
            {
                var order = linkedOrder?.Id == orderId
                    ? linkedOrder
                    : await this.storage.SelectSalesOrderByIdAsync(orderId);
                if (order == null) continue;
                SalesBusinessRules.AddInvoicedQuantities(order, lines);
                await this.storage.UpdateSalesOrderAsync(order);
            }

            SalesInvoiceSettlement.Enrich(created, this.storage);
            await SalesDocumentAudit.LogAsync(
                this.storage,
                created.CompanyId ?? this.companyContext.GetCurrentCompanyId(),
                "Invoice",
                created.Id,
                "Created",
                SalesDocumentAudit.ActorFrom(User),
                $"Création facture {created.InvoiceNumber}");
            return Created(created);
        }

        private static System.Collections.Generic.List<SalesInvoiceLine> MergeDeliveryNoteLines(
            System.Collections.Generic.List<SalesDeliveryNote> notes)
        {
            var map = new System.Collections.Generic.Dictionary<string, SalesInvoiceLine>(StringComparer.OrdinalIgnoreCase);
            var lineNumber = 0;
            foreach (var note in notes)
            {
                foreach (var l in note.Lines.Where(x => x.DeliveredQuantity > 0))
                {
                    var key = (l.ProductKey ?? "").Trim();
                    if (string.IsNullOrEmpty(key)) key = $"__desc_{l.Description}";
                    if (!map.TryGetValue(key, out var agg))
                    {
                        lineNumber++;
                        agg = new SalesInvoiceLine
                        {
                            ProductKey = l.ProductKey,
                            Description = l.Description,
                            Quantity = 0,
                            OrderedQuantity = 0,
                            DeliveredQuantity = 0,
                            UnitPrice = l.UnitPrice,
                            VatRate = l.VatRate,
                            LineNumber = lineNumber
                        };
                        map[key] = agg;
                    }
                    agg.Quantity += l.DeliveredQuantity;
                    agg.DeliveredQuantity += l.DeliveredQuantity;
                    agg.OrderedQuantity += l.OrderedQuantity > 0 ? l.OrderedQuantity : l.DeliveredQuantity;
                    agg.TotalHT = agg.Quantity * agg.UnitPrice;
                    agg.TotalTTC = agg.TotalHT * (1 + agg.VatRate / 100m);
                }
            }
            return map.Values.OrderBy(l => l.LineNumber).ToList();
        }

        [HttpPut("{id:int}")]
        [RequirePermission(Permissions.InvoiceUpdate)]
        public async Task<IActionResult> Put(int id, [FromBody] SalesInvoice invoice)
        {
            var existing = await this.storage.SelectSalesInvoiceByIdAsync(id);
            if (existing == null || !existing.BelongsToCompany(this.companyContext.GetCurrentCompanyId()))
                return NotFound();

            if (!string.Equals(existing.Status, "Draft", StringComparison.OrdinalIgnoreCase))
                return BadRequest($"Une facture au statut {existing.Status} ne peut pas être modifiée. Seules les factures Draft sont éditables.");

            // RG-CP1 : devise figée hors Draft (defense en profondeur, la facture est déjà Draft ici).
            var currencyErr = SalesBusinessRules.RejectCurrencyChangeIfFrozen(existing.Status, existing.CurrencyCode, invoice.CurrencyCode);
            if (currencyErr != null) return BadRequest(currencyErr);

            var incoming = invoice.Lines ?? new System.Collections.Generic.List<SalesInvoiceLine>();
            if (incoming.Count == 0)
                return BadRequest("La facture doit contenir au moins une ligne.");

            foreach (var line in incoming)
            {
                var discountErr = SalesBusinessRules.ValidateDiscountPercent(line.DiscountPercent, $"ligne {line.ProductKey}");
                if (discountErr != null) return BadRequest(discountErr);
            }
            var headerDiscountErr = SalesBusinessRules.ValidateDiscountPercent(invoice.HeaderDiscountPercent, "remise pied de page");
            if (headerDiscountErr != null) return BadRequest(headerDiscountErr);

            // Lignes issues d'un BL : produit figé, qté ≤ livré, suppression interdite.
            var locked = existing.Lines.Where(l => l.DeliveredQuantity > 0).ToList();
            foreach (var lockedLine in locked)
            {
                var match = incoming.FirstOrDefault(l =>
                    (l.Id > 0 && l.Id == lockedLine.Id)
                    || string.Equals(l.ProductKey?.Trim(), lockedLine.ProductKey?.Trim(), StringComparison.OrdinalIgnoreCase));
                if (match == null)
                    return BadRequest($"Impossible de supprimer la ligne '{lockedLine.ProductKey}' issue d'un BL.");
                if (!string.Equals(match.ProductKey?.Trim(), lockedLine.ProductKey?.Trim(), StringComparison.OrdinalIgnoreCase))
                    return BadRequest($"Impossible de changer l'article '{lockedLine.ProductKey}' issu d'un BL.");
                if (match.Quantity + 0.0001m < 0.0001m)
                    return BadRequest($"Quantité invalide pour '{lockedLine.ProductKey}'.");
                if (match.Quantity > lockedLine.DeliveredQuantity + 0.0001m)
                    return BadRequest($"Quantité de '{lockedLine.ProductKey}' supérieure à la qté livrée ({lockedLine.DeliveredQuantity:0.####}).");
            }

            if (locked.Count > 0 && existing.CustomerId != invoice.CustomerId)
                return BadRequest("Impossible de changer le client : la facture est liée à un BL.");

            if (existing.SalesOrderId.HasValue && existing.CustomerId != invoice.CustomerId)
            {
                var linkedOrder = await this.storage.SelectSalesOrderByIdAsync(existing.SalesOrderId.Value);
                if (linkedOrder != null)
                {
                    var customerError = SalesBusinessRules.ValidateSameCustomer(
                        linkedOrder.CustomerId, invoice.CustomerId, "commande → facture");
                    if (customerError != null) return BadRequest(customerError);
                }
            }

            var merged = new System.Collections.Generic.List<SalesInvoiceLine>();
            var lineNumber = 0;
            foreach (var incomingLine in incoming)
            {
                lineNumber++;
                var prev = existing.Lines.FirstOrDefault(l =>
                    (incomingLine.Id > 0 && l.Id == incomingLine.Id)
                    || string.Equals(l.ProductKey?.Trim(), incomingLine.ProductKey?.Trim(), StringComparison.OrdinalIgnoreCase));

                var qty = incomingLine.Quantity;
                var delivered = prev?.DeliveredQuantity ?? 0m;
                if (delivered > 0 && qty > delivered) qty = delivered;

                // RG-CP2 : prix figés pour lignes issues d'un BL.
                var unitPrice = delivered > 0 && prev != null ? prev.UnitPrice : incomingLine.UnitPrice;
                var vatRate = delivered > 0 && prev != null ? prev.VatRate : incomingLine.VatRate;
                if (delivered > 0 && prev != null
                    && (Math.Abs(incomingLine.UnitPrice - prev.UnitPrice) > 0.0001m
                        || Math.Abs(incomingLine.VatRate - prev.VatRate) > 0.0001m))
                {
                    return BadRequest($"Prix/TVA de '{prev.ProductKey}' figés (ligne issue d'un BL).");
                }

                var discountPercent = SalesBusinessRules.CapDiscountPercent(incomingLine.DiscountPercent);
                var lineHt = qty * unitPrice * (1 - (discountPercent / 100m));
                merged.Add(new SalesInvoiceLine
                {
                    ProductKey = incomingLine.ProductKey,
                    Description = incomingLine.Description,
                    Quantity = qty,
                    OrderedQuantity = prev?.OrderedQuantity > 0 ? prev.OrderedQuantity : qty,
                    DeliveredQuantity = delivered,
                    UnitPrice = unitPrice,
                    DiscountPercent = discountPercent,
                    VatRate = vatRate,
                    TotalHT = lineHt,
                    TotalTTC = lineHt * (1 + vatRate / 100m),
                    LineNumber = lineNumber,
                    LotNumber = incomingLine.LotNumber ?? prev?.LotNumber
                });
            }

            // Resync qté facturées sur la commande liée (delta old → new).
            if (existing.SalesOrderId.HasValue)
            {
                var order = await this.storage.SelectSalesOrderByIdAsync(existing.SalesOrderId.Value);
                if (order != null)
                {
                    var deltas = new System.Collections.Generic.Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
                    void Acc(string? key, decimal delta)
                    {
                        if (string.IsNullOrWhiteSpace(key) || delta == 0m) return;
                        var k = key.Trim();
                        deltas[k] = deltas.TryGetValue(k, out var cur) ? cur + delta : delta;
                    }
                    foreach (var l in existing.Lines) Acc(l.ProductKey, -l.Quantity);
                    foreach (var l in merged) Acc(l.ProductKey, l.Quantity);
                    SalesBusinessRules.AdjustInvoicedQuantities(order, deltas.Select(kv => (kv.Key, kv.Value)));
                    await this.storage.UpdateSalesOrderAsync(order);
                }
            }

            existing.CustomerId = invoice.CustomerId;
            existing.Notes = invoice.Notes;
            if (invoice.DueDate != default)
                existing.DueDate = PaymentTermsHelper.EnsureNotBeforeInvoiceDate(existing.Date, invoice.DueDate);
            if (invoice.Date != default) existing.Date = invoice.Date;
            existing.Lines = merged;
            existing.HeaderDiscountPercent = SalesBusinessRules.CapDiscountPercent(invoice.HeaderDiscountPercent);
            SalesBusinessRules.RecalculateInvoiceTotals(existing);

            var updated = await this.storage.UpdateSalesInvoiceAsync(existing);
            await SalesDocumentAudit.LogAsync(
                this.storage,
                this.companyContext.GetCurrentCompanyId(),
                "Invoice",
                updated.Id,
                "Updated",
                SalesDocumentAudit.ActorFrom(User),
                $"Modification {updated.InvoiceNumber} — {updated.Lines.Count} ligne(s)");
            SalesInvoiceSettlement.Enrich(updated, this.storage);
            return Ok(updated);
        }

        [HttpDelete("{id:int}")]
        [RequirePermission(Permissions.InvoiceDelete)]
        public async Task<IActionResult> Delete(int id)
        {
            var existing = await this.storage.SelectSalesInvoiceByIdAsync(id);
            if (existing == null || !existing.BelongsToCompany(this.companyContext.GetCurrentCompanyId()))
                return NotFound();

            if (!SalesBusinessRules.CanPhysicallyDelete(existing.Status))
                return BadRequest("Seules les factures Draft peuvent être supprimées. Une facture validée ne peut plus être purgée.");

            if (existing.PaidAmount > 0.0001m)
                return BadRequest("Impossible de supprimer une facture qui a déjà des règlements.");

            var credited = SalesInvoiceSettlement.GetAppliedCreditTotal(this.storage, existing.Id);
            if (credited > 0.0001m)
                return BadRequest("Impossible de supprimer une facture liée à un avoir appliqué.");

            // Libérer le BL (redevient facturable) et resync qté facturées commande.
            var linkedNotes = this.storage.SelectAllSalesDeliveryNotes()
                .Where(n => n.SalesInvoiceId == existing.Id)
                .ToList();

            foreach (var note in linkedNotes)
            {
                note.SalesInvoiceId = null;
                if (string.Equals(note.Status, "Invoiced", StringComparison.OrdinalIgnoreCase))
                    note.Status = "Delivered";
                await this.storage.UpdateSalesDeliveryNoteAsync(note);
            }

            if (existing.SalesOrderId.HasValue)
            {
                var order = await this.storage.SelectSalesOrderByIdAsync(existing.SalesOrderId.Value);
                if (order != null)
                {
                    var deltas = existing.Lines
                        .Select(l => (l.ProductKey, Delta: -l.Quantity))
                        .ToList();
                    SalesBusinessRules.AdjustInvoicedQuantities(order, deltas);
                    await this.storage.UpdateSalesOrderAsync(order);
                }
            }

            var actor = SalesDocumentAudit.ActorFrom(User);
            SalesBusinessRules.SoftDelete(existing, actor);
            await this.storage.DeleteSalesInvoiceAsync(existing);
            await SalesDocumentAudit.LogAsync(
                this.storage,
                existing.CompanyId ?? this.companyContext.GetCurrentCompanyId(),
                "Invoice",
                existing.Id,
                "Deleted",
                actor,
                $"Suppression soft facture {existing.InvoiceNumber}");

            return NoContent();
        }

        [HttpPost("{id:int}/validate")]
        [RequirePermission(Permissions.InvoiceUpdate)]
        public async Task<IActionResult> Validate(int id)
        {
            var invoice = await this.storage.SelectSalesInvoiceByIdAsync(id);
            if (invoice == null || !invoice.BelongsToCompany(this.companyContext.GetCurrentCompanyId())) return NotFound("Invoice not found");
            if (string.Equals(invoice.Status, "Cancelled", StringComparison.OrdinalIgnoreCase))
                return BadRequest("Une facture annulée ne peut pas être validée.");
            if (!string.Equals(invoice.Status, "Draft", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(invoice.Status)
                && !string.Equals(invoice.Status, "Validated", StringComparison.OrdinalIgnoreCase))
            {
                // Already paid / partially paid — allow posting if missing entry
            }
            else if (string.Equals(invoice.Status, "Validated", StringComparison.OrdinalIgnoreCase)
                     && AccountingLedger.HasPostedEntry(this.storage, AccountingLedger.RefSalesInvoice, invoice.Id, invoice.CompanyId))
            {
                return Conflict(new { error = "Facture déjà validée." });
            }

            if (invoice.Lines == null || invoice.Lines.Count == 0)
                return BadRequest("La facture doit contenir au moins une ligne.");

            // RG-FC8 : cohérence totaux = lignes.
            SalesBusinessRules.RecalculateInvoiceTotals(invoice);

            // RG-FC3 / FC6 / N2 : numéro définitif uniquement à la validation.
            if (ProvisionalDocumentNumber.IsProvisional(invoice.InvoiceNumber))
            {
                invoice.InvoiceNumber = await this.numberingService.GetNextNumberAsync("Invoice", invoice.CompanyId);
            }

            if (!AccountingLedger.HasPostedEntry(this.storage, AccountingLedger.RefSalesInvoice, invoice.Id, invoice.CompanyId))
            {
                var (entry, error) = await AccountingLedger.PostSalesInvoiceAsync(
                    this.storage, this.numberingService, invoice, User.Identity?.Name);
                if (error != null) return BadRequest(error);
                _ = entry;
            }

            if (string.Equals(invoice.Status, "Draft", StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(invoice.Status))
            {
                invoice.Status = "Validated";
            }

            var updated = await this.storage.UpdateSalesInvoiceAsync(invoice);
            SalesInvoiceSettlement.Enrich(updated, this.storage);
            await SalesDocumentAudit.LogAsync(
                this.storage,
                updated.CompanyId ?? this.companyContext.GetCurrentCompanyId(),
                "Invoice",
                updated.Id,
                "Validated",
                SalesDocumentAudit.ActorFrom(User),
                $"Validation facture {updated.InvoiceNumber}");
            return Ok(updated);
        }

        [HttpGet("{id:int}/audit")]
        [RequirePermission(Permissions.InvoiceRead)]
        public async Task<IActionResult> GetAudit(int id)
        {
            var invoice = await this.storage.SelectSalesInvoiceByIdIncludingDeletedAsync(id);
            if (invoice == null || !invoice.BelongsToCompany(this.companyContext.GetCurrentCompanyId()))
                return NotFound();

            var logs = this.storage.SelectAllDocumentAuditLogs()
                .Where(a => a.DocumentType == "Invoice" && a.DocumentId == id)
                .AsEnumerable()
                .OrderByDescending(a => a.CreatedAt)
                .ToList();
            return Ok(logs);
        }

        public class PaymentRequest
        {
            public decimal Amount { get; set; }
            public string PaymentMethod { get; set; } = "Cash";
            public string? Notes { get; set; }
            public string? Reference { get; set; }
            public string? Bank { get; set; }
            public decimal ReceivedAmount { get; set; }
            public decimal ChangeAmount { get; set; }
            public decimal RoundingDifference { get; set; }
        }

        [HttpPost("{id:int}/pay")]
        [RequirePermission(Permissions.InvoiceUpdate)]
        public async Task<IActionResult> RecordPayment(int id, [FromBody] PaymentRequest request)
        {
            var invoice = await this.storage.SelectSalesInvoiceByIdAsync(id);
            if (invoice == null || !invoice.BelongsToCompany(this.companyContext.GetCurrentCompanyId())) return NotFound("Invoice not found");
            if (request.Amount <= 0) return BadRequest("Amount must be positive");

            var payableError = SalesInvoiceSettlement.ValidatePayable(invoice, this.storage);
            if (payableError != null) return BadRequest(payableError);

            var credited = SalesInvoiceSettlement.GetAppliedCreditTotal(this.storage, invoice.Id);
            var remaining = Math.Max(0m, invoice.TotalTTC - invoice.PaidAmount - credited);
            if (remaining <= SalesInvoiceSettlement.SettlementTolerance)
                return BadRequest("Invoice is already fully settled (payments + credit notes).");

            // Pas de surpaiement silencieux : le montant demandé ne peut pas dépasser le reste dû.
            if (request.Amount > remaining + SalesInvoiceSettlement.SettlementTolerance)
            {
                return BadRequest($"Le paiement ({request.Amount:0.##} €) dépasse le reste dû ({remaining:0.##} €).");
            }

            var amount = request.Amount;
            invoice.PaidAmount += amount;
            SalesInvoiceSettlement.RefreshPaymentStatus(invoice, credited);

            var paymentNote = $"Payment {amount:0.##} via {request.PaymentMethod}";
            if (!string.IsNullOrWhiteSpace(request.Notes))
            {
                paymentNote = $"{paymentNote} | {request.Notes.Trim()}";
            }

            invoice.Notes = string.IsNullOrWhiteSpace(invoice.Notes)
                ? paymentNote
                : $"{invoice.Notes}{Environment.NewLine}{paymentNote}";

            var updated = await this.storage.UpdateSalesInvoiceAsync(invoice);

            int? cashSessionId = null;
            if (string.Equals(request.PaymentMethod, "Cash", StringComparison.OrdinalIgnoreCase))
            {
                var activeSession = await this.storage.SelectActiveCashSessionAsync(invoice.CompanyId);
                if (activeSession != null)
                {
                    cashSessionId = activeSession.Id;
                    await this.storage.InsertCashOperationAsync(new CashOperation
                    {
                        CashSessionId = activeSession.Id,
                        OperationType = "SalePayment",
                        Amount = amount,
                        Description = $"Invoice {invoice.InvoiceNumber}",
                        ReferenceDocument = invoice.InvoiceNumber,
                        CreatedBy = User.Identity?.Name ?? "System",
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }

            var payment = await this.storage.InsertPaymentAsync(new Payment
            {
                CompanyId = invoice.CompanyId,
                SalesInvoiceId = invoice.Id,
                Amount = amount,
                RoundingDifference = request.RoundingDifference,
                ReceivedAmount = request.ReceivedAmount > 0 ? request.ReceivedAmount : amount,
                ChangeAmount = request.ChangeAmount,
                PaidAt = DateTime.UtcNow,
                Method = request.PaymentMethod,
                Reference = request.Reference,
                Bank = request.Bank,
                Status = "Success",
                CashSessionId = cashSessionId,
                CreatedBy = User.Identity?.Name ?? "System",
                CreatedAt = DateTime.UtcNow
            });

            var (_, payError) = await AccountingLedger.PostSalesPaymentAsync(
                this.storage, this.numberingService, invoice, payment, User.Identity?.Name);
            if (payError != null) return BadRequest(payError);

            SalesInvoiceSettlement.Enrich(updated, this.storage);
            return Ok(new { invoice = updated, payment });
        }
    }
}
