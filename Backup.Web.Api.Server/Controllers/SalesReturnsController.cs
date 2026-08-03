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
    /// <summary>Bon de retour client (BRC) — RG-BR1–5. Retour → réception stock → contrôle qualité → intégration/avoir.</summary>
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class SalesReturnsController : RESTFulController
    {
        private readonly IStorageBroker storage;
        private readonly INumberingSequenceService numberingService;
        private readonly ICompanyContextService companyContext;

        public SalesReturnsController(IStorageBroker storage, INumberingSequenceService numberingService, ICompanyContextService companyContext)
        {
            this.storage = storage;
            this.numberingService = numberingService;
            this.companyContext = companyContext;
        }

        public class CreateFromDeliveryLineRequest
        {
            public string? ProductKey { get; set; }
            public decimal Quantity { get; set; }
            public string? QualityStatus { get; set; }
        }

        public class CreateFromDeliveryRequest
        {
            public int SalesDeliveryNoteId { get; set; }
            /// <summary>Lignes à retourner ; si null/vide = toutes les lignes livrées (reliquat non déjà retourné).</summary>
            public List<CreateFromDeliveryLineRequest>? Lines { get; set; }
            public string? Notes { get; set; }
        }

        public class ControlRequest
        {
            public string? QualityStatus { get; set; }
        }

        public class IntegrateRequest
        {
            public bool CreateCreditNote { get; set; }
            public int? SalesInvoiceId { get; set; }
        }

        public class CreateCreditNoteRequest
        {
            public int? SalesInvoiceId { get; set; }
        }

        public class CancelRequest
        {
            public string? Reason { get; set; }
        }

        [HttpGet]
        [RequirePermission(Permissions.SalesReturnRead)]
        public IActionResult GetAll(
            [FromQuery] string? search = null,
            [FromQuery] int? customerId = null,
            [FromQuery] int? salesDeliveryNoteId = null)
        {
            var query = this.storage.SelectAllSalesReturns().ForCompany(this.companyContext.GetCurrentCompanyId());

            if (customerId.HasValue)
                query = query.Where(r => r.CustomerId == customerId.Value);

            if (salesDeliveryNoteId.HasValue)
                query = query.Where(r => r.SalesDeliveryNoteId == salesDeliveryNoteId.Value);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.ToLowerInvariant();
                query = query.Where(r =>
                    r.ReturnNumber.ToLower().Contains(s) ||
                    (r.Customer != null && r.Customer.Name.ToLower().Contains(s)));
            }

            return Ok(query.OrderByDescending(r => r.CreatedAt).ToList());
        }

        [HttpGet("{id:int}")]
        [RequirePermission(Permissions.SalesReturnRead)]
        public async Task<IActionResult> GetById(int id)
        {
            var salesReturn = await this.storage.SelectSalesReturnByIdAsync(id);
            if (salesReturn == null || !salesReturn.BelongsToCompany(this.companyContext.GetCurrentCompanyId())) return NotFound();
            return Ok(salesReturn);
        }

        /// <summary>RG-BR1 : le retour est toujours créé depuis un BL Livré ou Facturé.</summary>
        [HttpPost("from-delivery")]
        [RequirePermission(Permissions.SalesReturnCreate)]
        public async Task<IActionResult> CreateFromDelivery([FromBody] CreateFromDeliveryRequest request)
        {
            if (request.SalesDeliveryNoteId <= 0) return BadRequest("SalesDeliveryNoteId requis.");

            var note = await this.storage.SelectSalesDeliveryNoteByIdAsync(request.SalesDeliveryNoteId);
            if (note == null || !note.BelongsToCompany(this.companyContext.GetCurrentCompanyId()))
                return NotFound("Bon de livraison introuvable.");

            if (!string.Equals(note.Status, "Delivered", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(note.Status, "Invoiced", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest($"Le retour ne peut être créé que depuis un BL Livré ou Facturé (statut actuel : {note.Status}).");
            }

            var companyId = note.CompanyId ?? this.companyContext.GetCurrentCompanyId();

            // Reliquat retournable = qté livrée - qté déjà retournée sur BRC non annulés pour ce BL.
            var alreadyReturnedByKey = this.storage.SelectAllSalesReturns()
                .Where(r => r.SalesDeliveryNoteId == note.Id)
                .AsEnumerable()
                .Where(r => !string.Equals(r.Status, "Cancelled", StringComparison.OrdinalIgnoreCase))
                .SelectMany(r => r.Lines ?? new List<SalesReturnLine>())
                .GroupBy(l => (l.ProductKey ?? string.Empty).Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Sum(l => l.Quantity), StringComparer.OrdinalIgnoreCase);

            var deliveredLines = note.Lines.Where(l => l.DeliveredQuantity > 0).ToList();
            if (deliveredLines.Count == 0) return BadRequest("Aucune ligne livrée sur ce BL.");

            var requestedByKey = new Dictionary<string, (decimal Quantity, string? QualityStatus)>(StringComparer.OrdinalIgnoreCase);
            var hasSpecificSelection = false;
            if (request.Lines != null && request.Lines.Count > 0)
            {
                foreach (var rl in request.Lines)
                {
                    if (string.IsNullOrWhiteSpace(rl.ProductKey) || rl.Quantity <= 0) continue;
                    hasSpecificSelection = true;
                    requestedByKey[rl.ProductKey.Trim()] = (rl.Quantity, rl.QualityStatus);
                }
            }

            var lines = new List<SalesReturnLine>();
            var lineNumber = 0;
            foreach (var dl in deliveredLines)
            {
                var key = (dl.ProductKey ?? string.Empty).Trim();
                var alreadyReturnedQty = alreadyReturnedByKey.TryGetValue(key, out var ar) ? ar : 0m;
                var maxReturnable = Math.Max(0m, dl.DeliveredQuantity - alreadyReturnedQty);
                if (maxReturnable <= 0.0001m) continue;

                decimal qty;
                string? quality = null;
                if (requestedByKey.TryGetValue(key, out var req))
                {
                    qty = Math.Min(maxReturnable, req.Quantity);
                    quality = req.QualityStatus;
                }
                else if (!hasSpecificSelection)
                {
                    qty = maxReturnable;
                }
                else
                {
                    continue;
                }

                if (qty <= 0.0001m) continue;

                lineNumber++;
                lines.Add(new SalesReturnLine
                {
                    ProductKey = dl.ProductKey,
                    Description = dl.Description,
                    Quantity = qty,
                    UnitPrice = dl.UnitPrice,
                    VatRate = dl.VatRate,
                    TotalHT = qty * dl.UnitPrice,
                    TotalTTC = qty * dl.UnitPrice * (1 + dl.VatRate / 100m),
                    LineNumber = lineNumber,
                    QualityStatus = quality
                });
            }

            if (lines.Count == 0)
                return BadRequest("Aucune quantité retournable (déjà retournée ou sélection invalide / quantité demandée dépassant le reliquat).");

            var salesReturn = new SalesReturn
            {
                ReturnNumber = await this.numberingService.GetNextNumberAsync("SalesReturn", companyId),
                CustomerId = note.CustomerId,
                SalesDeliveryNoteId = note.Id,
                SalesOrderId = note.SalesOrderId,
                ReturnDate = DateTime.UtcNow,
                Status = "Draft",
                Notes = string.IsNullOrWhiteSpace(request.Notes)
                    ? $"Retour depuis BL {note.DeliveryNumber}"
                    : request.Notes.Trim(),
                CompanyId = companyId,
                CreatedAt = DateTime.UtcNow,
                Lines = lines
            };
            // RG-CP1 : devise figée à la création depuis Company.DefaultCurrencyCode.
            salesReturn.CurrencyCode = await SalesBusinessRules.ResolveCompanyCurrencyAsync(this.storage, companyId);
            RecalcTotals(salesReturn);

            var created = await this.storage.InsertSalesReturnAsync(salesReturn);
            await AuditReturnAsync(created.Id, "Created", $"Création retour {created.ReturnNumber} depuis BL {note.DeliveryNumber}");
            return Created(created);
        }

        [HttpPut("{id:int}")]
        [RequirePermission(Permissions.SalesReturnUpdate)]
        public async Task<IActionResult> Put(int id, [FromBody] SalesReturn salesReturn)
        {
            var existing = await this.storage.SelectSalesReturnByIdAsync(id);
            if (existing == null || !existing.BelongsToCompany(this.companyContext.GetCurrentCompanyId())) return NotFound();

            if (!string.Equals(existing.Status, "Draft", StringComparison.OrdinalIgnoreCase))
                return BadRequest($"Un retour au statut {existing.Status} ne peut plus être modifié.");

            existing.Notes = salesReturn.Notes;
            if (!string.IsNullOrWhiteSpace(salesReturn.QualityStatus))
                existing.QualityStatus = salesReturn.QualityStatus;
            if (salesReturn.Lines != null && salesReturn.Lines.Count > 0)
                existing.Lines = salesReturn.Lines;

            RecalcTotals(existing);

            var updated = await this.storage.UpdateSalesReturnAsync(existing);
            return Ok(updated);
        }

        /// <summary>RG-BR2 : réception physique → entrée stock (Conforme/Degraded, hors NonRecoverable).</summary>
        [HttpPost("{id:int}/receive")]
        [RequirePermission(Permissions.SalesReturnUpdate)]
        public async Task<IActionResult> Receive(int id)
        {
            var salesReturn = await this.storage.SelectSalesReturnByIdAsync(id);
            if (salesReturn == null || !salesReturn.BelongsToCompany(this.companyContext.GetCurrentCompanyId())) return NotFound();

            var receiveErr = SalesBusinessRules.RejectIfSalesReturnCannotReceive(salesReturn.Status);
            if (receiveErr != null) return Conflict(new { error = receiveErr });

            if (salesReturn.Lines == null || salesReturn.Lines.Count == 0)
                return BadRequest("Le retour doit contenir au moins une ligne.");

            var companyId = salesReturn.CompanyId ?? this.companyContext.GetCurrentCompanyId();
            await ApplyStockInAsync(salesReturn, companyId);

            salesReturn.Status = "Received";
            var updated = await this.storage.UpdateSalesReturnAsync(salesReturn);
            await AuditReturnAsync(updated.Id, "Received", $"Réception retour {updated.ReturnNumber}");
            return Ok(updated);
        }

        /// <summary>RG-BR3 : contrôle qualité (Conforme/Degraded/NonRecoverable).</summary>
        [HttpPost("{id:int}/control")]
        [RequirePermission(Permissions.SalesReturnUpdate)]
        public async Task<IActionResult> Control(int id, [FromBody] ControlRequest? request)
        {
            var salesReturn = await this.storage.SelectSalesReturnByIdAsync(id);
            if (salesReturn == null || !salesReturn.BelongsToCompany(this.companyContext.GetCurrentCompanyId())) return NotFound();

            var controlErr = SalesBusinessRules.RejectIfSalesReturnCannotControl(salesReturn.Status);
            if (controlErr != null) return Conflict(new { error = controlErr });

            if (!string.IsNullOrWhiteSpace(request?.QualityStatus))
                salesReturn.QualityStatus = request!.QualityStatus!.Trim();

            salesReturn.Status = "Controlled";
            var updated = await this.storage.UpdateSalesReturnAsync(salesReturn);
            await AuditReturnAsync(updated.Id, "Controlled", $"Contrôle qualité retour {updated.ReturnNumber} ({updated.QualityStatus ?? "n/a"})");
            return Ok(updated);
        }

        /// <summary>RG-BR4/AC4 : intégration finale du retour ; génère l'avoir Draft lié si demandé.</summary>
        [HttpPost("{id:int}/integrate")]
        [RequirePermission(Permissions.SalesReturnUpdate)]
        public async Task<IActionResult> Integrate(int id, [FromBody] IntegrateRequest? request)
        {
            var salesReturn = await this.storage.SelectSalesReturnByIdAsync(id);
            if (salesReturn == null || !salesReturn.BelongsToCompany(this.companyContext.GetCurrentCompanyId())) return NotFound();

            var integrateErr = SalesBusinessRules.RejectIfSalesReturnCannotIntegrate(salesReturn.Status);
            if (integrateErr != null)
            {
                if (string.Equals(salesReturn.Status, "Integrated", StringComparison.OrdinalIgnoreCase))
                    return Conflict(new { error = integrateErr });
                return BadRequest(integrateErr);
            }

            var companyId = salesReturn.CompanyId ?? this.companyContext.GetCurrentCompanyId();
            await ApplyStockInAsync(salesReturn, companyId);

            salesReturn.Status = "Integrated";
            var updated = await this.storage.UpdateSalesReturnAsync(salesReturn);
            await AuditReturnAsync(updated.Id, "Integrated", $"Intégration retour {updated.ReturnNumber}");

            if (request?.CreateCreditNote == true && !updated.CreditNoteId.HasValue)
            {
                var (creditNote, error) = await CreateCreditNoteForReturnAsync(updated, request.SalesInvoiceId);
                if (error != null) return Ok(new { salesReturn = updated, creditNoteError = error });
                return Ok(new { salesReturn = updated, creditNote });
            }

            return Ok(updated);
        }

        /// <summary>RG-BR5 : annulation possible sauf si déjà intégré ; réversion stock si déjà réceptionné.</summary>
        [HttpPost("{id:int}/cancel")]
        [RequirePermission(Permissions.SalesReturnUpdate)]
        public async Task<IActionResult> Cancel(int id, [FromBody] CancelRequest? request)
        {
            var salesReturn = await this.storage.SelectSalesReturnByIdAsync(id);
            if (salesReturn == null || !salesReturn.BelongsToCompany(this.companyContext.GetCurrentCompanyId())) return NotFound();

            var cancelErr = SalesBusinessRules.RejectIfSalesReturnCannotCancel(salesReturn.Status);
            if (cancelErr != null)
            {
                if (string.Equals(salesReturn.Status, "Cancelled", StringComparison.OrdinalIgnoreCase))
                    return Conflict(new { error = cancelErr });
                return BadRequest(cancelErr);
            }

            var companyId = salesReturn.CompanyId ?? this.companyContext.GetCurrentCompanyId();
            if (salesReturn.StockApplied)
            {
                foreach (var line in salesReturn.Lines)
                {
                    var quality = ResolveLineQuality(salesReturn, line);
                    if (string.Equals(quality, "NonRecoverable", StringComparison.OrdinalIgnoreCase)) continue;

                    await StockLedger.ApplyAsync(
                        this.storage,
                        companyId,
                        line.ProductKey,
                        "Out",
                        line.Quantity,
                        $"BRC-CANCEL:{salesReturn.ReturnNumber}",
                        $"Annulation retour {salesReturn.ReturnNumber}",
                        User.Identity?.Name);
                }
                salesReturn.StockApplied = false;
            }

            salesReturn.Status = "Cancelled";
            var motif = string.IsNullOrWhiteSpace(request?.Reason) ? "Annulation" : request!.Reason!.Trim();
            salesReturn.Notes = string.IsNullOrWhiteSpace(salesReturn.Notes) ? motif : $"{salesReturn.Notes}\n{motif}";

            var updated = await this.storage.UpdateSalesReturnAsync(salesReturn);
            await AuditReturnAsync(updated.Id, "Cancelled", $"Annulation retour {updated.ReturnNumber}", motif);
            return Ok(updated);
        }

        /// <summary>Soft-delete : brouillons ou annulés uniquement (BRC Cancelled / Draft).</summary>
        [HttpDelete("{id:int}")]
        [RequirePermission(Permissions.SalesReturnUpdate)]
        public async Task<IActionResult> Delete(int id)
        {
            var salesReturn = await this.storage.SelectSalesReturnByIdAsync(id);
            if (salesReturn == null || !salesReturn.BelongsToCompany(this.companyContext.GetCurrentCompanyId()))
                return NotFound();

            if (!string.Equals(salesReturn.Status, "Cancelled", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(salesReturn.Status, "Draft", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest("Seuls les BRC brouillon ou annulés peuvent être supprimés. Annulez d'abord le retour.");
            }

            if (salesReturn.CreditNoteId.HasValue)
            {
                var cn = await this.storage.SelectCreditNoteByIdAsync(salesReturn.CreditNoteId.Value);
                if (cn != null && !string.Equals(cn.Status, "Cancelled", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(cn.Status, "Draft", StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest($"Impossible de supprimer : un avoir {cn.CreditNoteNumber} ({cn.Status}) est lié à ce retour.");
                }
            }

            var actor = SalesDocumentAudit.ActorFrom(User);
            SalesBusinessRules.SoftDelete(salesReturn, actor);
            await this.storage.DeleteSalesReturnAsync(salesReturn);
            await AuditReturnAsync(salesReturn.Id, "Deleted", $"Suppression soft retour {salesReturn.ReturnNumber}");
            return NoContent();
        }

        /// <summary>RG-AC4 : génère un avoir Draft depuis ce retour (facture du BL, ou SalesInvoiceId explicite).</summary>
        [HttpPost("{id:int}/create-credit-note")]
        [RequirePermission(Permissions.SalesReturnUpdate)]
        public async Task<IActionResult> CreateCreditNote(int id, [FromBody] CreateCreditNoteRequest? request)
        {
            var salesReturn = await this.storage.SelectSalesReturnByIdAsync(id);
            if (salesReturn == null || !salesReturn.BelongsToCompany(this.companyContext.GetCurrentCompanyId())) return NotFound();

            if (salesReturn.CreditNoteId.HasValue)
            {
                return Conflict(new
                {
                    error = $"Un avoir existe déjà pour ce retour (#{salesReturn.CreditNoteId}).",
                    creditNoteId = salesReturn.CreditNoteId
                });
            }

            var linkedCredit = this.storage.SelectAllCreditNotes()
                .FirstOrDefault(c => c.SalesReturnId == salesReturn.Id
                    && c.Status.ToLower() != "cancelled");
            if (linkedCredit != null)
            {
                salesReturn.CreditNoteId = linkedCredit.Id;
                await this.storage.UpdateSalesReturnAsync(salesReturn);
                return Conflict(new
                {
                    error = $"Un avoir existe déjà pour ce retour ({linkedCredit.CreditNoteNumber}).",
                    creditNoteId = linkedCredit.Id
                });
            }

            var (creditNote, error) = await CreateCreditNoteForReturnAsync(salesReturn, request?.SalesInvoiceId);
            if (error != null) return BadRequest(error);
            return Created(creditNote);
        }

        private async Task ApplyStockInAsync(SalesReturn salesReturn, string? companyId)
        {
            if (salesReturn.StockApplied) return;

            foreach (var line in salesReturn.Lines)
            {
                var quality = ResolveLineQuality(salesReturn, line);
                if (string.Equals(quality, "NonRecoverable", StringComparison.OrdinalIgnoreCase)) continue;

                await StockLedger.ApplyAsync(
                    this.storage,
                    companyId,
                    line.ProductKey,
                    "In",
                    line.Quantity,
                    $"BRC:{salesReturn.ReturnNumber}",
                    $"Retour client {salesReturn.ReturnNumber} ({quality})",
                    User.Identity?.Name);
            }

            salesReturn.StockApplied = true;
        }

        private async Task<(CreditNoteEntity? CreditNote, string? Error)> CreateCreditNoteForReturnAsync(
            SalesReturn salesReturn,
            int? explicitSalesInvoiceId)
        {
            if (salesReturn.CreditNoteId.HasValue)
            {
                var existing = await this.storage.SelectCreditNoteByIdAsync(salesReturn.CreditNoteId.Value);
                return (existing, existing == null ? "Avoir lié introuvable." : null);
            }

            var alreadyLinked = this.storage.SelectAllCreditNotes()
                .FirstOrDefault(c => c.SalesReturnId == salesReturn.Id
                    && c.Status.ToLower() != "cancelled");
            if (alreadyLinked != null)
            {
                salesReturn.CreditNoteId = alreadyLinked.Id;
                await this.storage.UpdateSalesReturnAsync(salesReturn);
                return (alreadyLinked, null);
            }

            var salesInvoiceId = explicitSalesInvoiceId;
            if (!salesInvoiceId.HasValue || salesInvoiceId.Value <= 0)
            {
                var note = salesReturn.SalesDeliveryNote
                    ?? await this.storage.SelectSalesDeliveryNoteByIdAsync(salesReturn.SalesDeliveryNoteId);
                salesInvoiceId = note?.SalesInvoiceId;
            }

            if (!salesInvoiceId.HasValue || salesInvoiceId.Value <= 0)
                return (null, "Aucune facture liée au BL d'origine : précisez SalesInvoiceId pour générer l'avoir.");

            var invoice = await this.storage.SelectSalesInvoiceByIdAsync(salesInvoiceId.Value);
            if (invoice == null) return (null, "Facture liée introuvable.");
            if (invoice.CustomerId != salesReturn.CustomerId)
                return (null, "La facture liée doit appartenir au même client que le retour.");

            var companyId = salesReturn.CompanyId ?? this.companyContext.GetCurrentCompanyId();
            var creditLines = salesReturn.Lines.Select((l, i) => new CreditNoteLineEntity
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

            if (creditLines.Count == 0)
                return (null, "Le retour n'a aucune ligne à créditer.");

            var existingCredits = SalesBusinessRules.GetLinkedCreditNotesTotal(this.storage, invoice.Id);
            var remainingCap = Math.Max(0m, invoice.TotalTTC - existingCredits);
            if (remainingCap <= 0.01m)
            {
                return (null,
                    $"Impossible de créer un avoir : la facture {invoice.InvoiceNumber} est déjà entièrement couverte par des avoirs "
                    + $"({existingCredits:0.##} € / {invoice.TotalTTC:0.##} €).");
            }

            var requestedTtc = creditLines.Sum(l => l.TotalTTC);
            var capped = false;
            if (requestedTtc > remainingCap + 0.01m)
            {
                CapCreditLinesToMaxTtc(creditLines, remainingCap);
                capped = true;
                if (creditLines.Count == 0)
                    return (null, $"Capacité d'avoir restante trop faible ({remainingCap:0.##} €) pour générer des lignes.");
            }

            var creditNote = new CreditNoteEntity
            {
                CustomerId = salesReturn.CustomerId,
                SalesInvoiceId = invoice.Id,
                SalesReturnId = salesReturn.Id,
                Date = DateTime.UtcNow,
                Status = "Draft",
                Notes = capped
                    ? $"Avoir généré depuis retour {salesReturn.ReturnNumber} — plafonné à la capacité restante de la facture "
                      + $"({remainingCap:0.##} € ; retour {requestedTtc:0.##} €, avoirs déjà liés {existingCredits:0.##} €)."
                    : $"Avoir généré depuis retour {salesReturn.ReturnNumber}",
                CompanyId = companyId,
                CreatedAt = DateTime.UtcNow,
                Lines = creditLines
            };
            creditNote.TotalHT = creditNote.Lines.Sum(l => l.TotalHT);
            creditNote.TotalVat = creditNote.Lines.Sum(l => l.TotalTTC - l.TotalHT);
            creditNote.TotalTTC = creditNote.Lines.Sum(l => l.TotalTTC);
            creditNote.CreditNoteNumber = await this.numberingService.GetNextNumberAsync("CreditNote", companyId);

            // Filet de sécurité (après plafonnement, doit passer).
            var capError = SalesBusinessRules.ValidateCreditCap(this.storage, invoice, creditNote.TotalTTC);
            if (capError != null) return (null, capError);

            var created = await this.storage.InsertCreditNoteAsync(creditNote);

            salesReturn.CreditNoteId = created.Id;
            await this.storage.UpdateSalesReturnAsync(salesReturn);

            await SalesDocumentAudit.LogAsync(
                this.storage, companyId, "CreditNote", created.Id, "Created", SalesDocumentAudit.ActorFrom(User),
                capped
                    ? $"Avoir {created.CreditNoteNumber} depuis retour {salesReturn.ReturnNumber} (plafonné {created.TotalTTC:0.##} €)"
                    : $"Avoir {created.CreditNoteNumber} généré depuis retour {salesReturn.ReturnNumber}");

            return (created, null);
        }

        /// <summary>Réduit proportionnellement les qtés pour que Σ TTC ≤ maxTtc (capacité restante facture).</summary>
        private static void CapCreditLinesToMaxTtc(List<CreditNoteLineEntity> lines, decimal maxTtc)
        {
            var total = lines.Sum(l => l.TotalTTC);
            if (total <= maxTtc + 0.01m || total <= 0m || maxTtc <= 0m) return;

            var factor = maxTtc / total;
            foreach (var line in lines)
            {
                line.Quantity = Math.Round(line.Quantity * factor, 4, MidpointRounding.ToZero);
                line.TotalHT = line.Quantity * line.UnitPrice;
                line.TotalTTC = line.TotalHT * (1 + (line.VatRate / 100m));
            }

            lines.RemoveAll(l => l.Quantity <= 0.0001m);

            // Ajustement final si l'arrondi dépasse encore légèrement le plafond.
            var again = lines.Sum(l => l.TotalTTC);
            if (again > maxTtc + 0.01m && lines.Count > 0)
            {
                var last = lines[^1];
                var unitTtc = last.UnitPrice * (1 + (last.VatRate / 100m));
                if (unitTtc > 0.0001m)
                {
                    var excess = again - maxTtc;
                    var qtyCut = Math.Ceiling((excess / unitTtc) * 10000m) / 10000m;
                    last.Quantity = Math.Max(0m, last.Quantity - qtyCut);
                    last.TotalHT = last.Quantity * last.UnitPrice;
                    last.TotalTTC = last.TotalHT * (1 + (last.VatRate / 100m));
                }

                lines.RemoveAll(l => l.Quantity <= 0.0001m);
            }

            for (var i = 0; i < lines.Count; i++)
                lines[i].LineNumber = i + 1;
        }

        private static string ResolveLineQuality(SalesReturn salesReturn, SalesReturnLine line) =>
            !string.IsNullOrWhiteSpace(line.QualityStatus) ? line.QualityStatus!.Trim()
            : !string.IsNullOrWhiteSpace(salesReturn.QualityStatus) ? salesReturn.QualityStatus!.Trim()
            : "Conforme";

        private static void RecalcTotals(SalesReturn salesReturn)
        {
            salesReturn.Lines ??= new List<SalesReturnLine>();
            for (var i = 0; i < salesReturn.Lines.Count; i++)
            {
                var line = salesReturn.Lines[i];
                line.LineNumber = line.LineNumber <= 0 ? i + 1 : line.LineNumber;
                line.TotalHT = line.Quantity * line.UnitPrice;
                line.TotalTTC = line.TotalHT * (1 + (line.VatRate / 100m));
            }

            salesReturn.TotalHT = salesReturn.Lines.Sum(l => l.TotalHT);
            salesReturn.TotalVat = salesReturn.Lines.Sum(l => l.TotalTTC - l.TotalHT);
            salesReturn.TotalTTC = salesReturn.Lines.Sum(l => l.TotalTTC);
        }

        private async Task AuditReturnAsync(int returnId, string action, string summary, string? details = null)
        {
            await SalesDocumentAudit.LogAsync(
                this.storage,
                this.companyContext.GetCurrentCompanyId(),
                "SalesReturn",
                returnId,
                action,
                SalesDocumentAudit.ActorFrom(User),
                summary,
                details);
        }
    }
}
