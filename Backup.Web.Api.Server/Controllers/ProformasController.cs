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
    /// Facture proforma (PF) — RG-PF1–4. Apparence de facture, jamais de GL/stock/balance,
    /// et jamais convertible directement en facture (nécessite de repasser par commande → BL → facture).
    /// </summary>
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class ProformasController : RESTFulController
    {
        private readonly IStorageBroker storage;
        private readonly INumberingSequenceService numberingService;
        private readonly ICompanyContextService companyContext;

        public ProformasController(IStorageBroker storage, INumberingSequenceService numberingService, ICompanyContextService companyContext)
        {
            this.storage = storage;
            this.numberingService = numberingService;
            this.companyContext = companyContext;
        }

        public class CreateProformaRequest
        {
            public int? QuoteId { get; set; }
            public int? SalesOrderId { get; set; }
            public int CustomerId { get; set; }
            public string? Notes { get; set; }
            /// <summary>Lignes libres — utilisées uniquement si QuoteId/SalesOrderId non fournis.</summary>
            public List<ProformaLine>? Lines { get; set; }
        }

        public class CancelRequest
        {
            public string? Reason { get; set; }
        }

        [HttpGet]
        [RequirePermission(Permissions.InvoiceRead)]
        public IActionResult GetAll([FromQuery] string? search = null, [FromQuery] int? customerId = null)
        {
            var query = this.storage.SelectAllProformas().ForCompany(this.companyContext.GetCurrentCompanyId());

            if (customerId.HasValue)
                query = query.Where(p => p.CustomerId == customerId.Value);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.ToLowerInvariant();
                query = query.Where(p => p.ProformaNumber.ToLower().Contains(s) || (p.Customer != null && p.Customer.Name.ToLower().Contains(s)));
            }

            return Ok(query.OrderByDescending(p => p.Date).ToList());
        }

        [HttpGet("{id:int}")]
        [RequirePermission(Permissions.InvoiceRead)]
        public async Task<IActionResult> GetById(int id)
        {
            var proforma = await this.storage.SelectProformaByIdAsync(id);
            if (proforma == null || !proforma.BelongsToCompany(this.companyContext.GetCurrentCompanyId())) return NotFound();
            return Ok(proforma);
        }

        /// <summary>RG-PF1 : proforma libre, depuis un devis, ou depuis une commande.</summary>
        [HttpPost]
        [RequirePermission(Permissions.InvoiceCreate)]
        public async Task<IActionResult> Post([FromBody] CreateProformaRequest request)
        {
            var companyId = this.companyContext.GetCurrentCompanyId();
            int customerId;
            List<ProformaLine> lines;
            string originNote;

            if (request.QuoteId.HasValue && request.QuoteId.Value > 0)
            {
                var quote = await this.storage.SelectQuoteByIdAsync(request.QuoteId.Value);
                if (quote == null || !quote.BelongsToCompany(companyId)) return BadRequest("Devis introuvable.");
                customerId = quote.CustomerId;
                lines = quote.Lines.Select((l, i) => new ProformaLine
                {
                    ProductKey = l.ProductKey,
                    Description = l.Description,
                    Quantity = l.Quantity,
                    UnitPrice = l.UnitPrice,
                    VatRate = l.VatRate,
                    LineNumber = i + 1
                }).ToList();
                originNote = $"Proforma générée depuis le devis {quote.QuoteNumber}";
            }
            else if (request.SalesOrderId.HasValue && request.SalesOrderId.Value > 0)
            {
                var order = await this.storage.SelectSalesOrderByIdAsync(request.SalesOrderId.Value);
                if (order == null || !order.BelongsToCompany(companyId)) return BadRequest("Commande introuvable.");
                customerId = order.CustomerId;
                lines = order.Lines.Select((l, i) => new ProformaLine
                {
                    ProductKey = l.ProductKey,
                    Description = l.Description,
                    Quantity = l.Quantity,
                    UnitPrice = l.UnitPrice,
                    VatRate = l.VatRate,
                    LineNumber = i + 1
                }).ToList();
                originNote = $"Proforma générée depuis la commande {order.OrderNumber}";
            }
            else
            {
                if (request.CustomerId <= 0) return BadRequest("CustomerId requis pour une proforma libre.");
                lines = (request.Lines ?? new List<ProformaLine>())
                    .Select((l, i) => new ProformaLine
                    {
                        ProductKey = l.ProductKey,
                        Description = l.Description,
                        Quantity = l.Quantity,
                        UnitPrice = l.UnitPrice,
                        VatRate = l.VatRate,
                        LineNumber = i + 1
                    }).ToList();
                customerId = request.CustomerId;
                originNote = "Proforma libre";
            }

            var customer = await this.storage.SelectCustomerByIdAsync(customerId);
            if (customer == null) return BadRequest("Client introuvable.");
            var partyErr = SalesBusinessRules.RejectIfPartyNotActive(customer.Status, customer.Name);
            if (partyErr != null) return BadRequest(partyErr);

            if (lines.Count == 0) return BadRequest("La proforma doit contenir au moins une ligne.");

            var proforma = new Proforma
            {
                ProformaNumber = await this.numberingService.GetNextNumberAsync("Proforma", companyId),
                CustomerId = customerId,
                QuoteId = request.QuoteId,
                SalesOrderId = request.SalesOrderId,
                Date = DateTime.UtcNow,
                Status = "Draft",
                Notes = string.IsNullOrWhiteSpace(request.Notes) ? originNote : $"{originNote}{Environment.NewLine}{request.Notes.Trim()}",
                CompanyId = companyId,
                CreatedAt = DateTime.UtcNow,
                Lines = lines
            };
            // RG-CP1 : devise figée à la création depuis Company.DefaultCurrencyCode.
            proforma.CurrencyCode = await SalesBusinessRules.ResolveCompanyCurrencyAsync(this.storage, companyId);
            RecalcTotals(proforma);

            var created = await this.storage.InsertProformaAsync(proforma);
            await AuditProformaAsync(created.Id, "Created", $"Création proforma {created.ProformaNumber}");
            return Created(created);
        }

        [HttpPut("{id:int}")]
        [RequirePermission(Permissions.InvoiceUpdate)]
        public async Task<IActionResult> Put(int id, [FromBody] Proforma proforma)
        {
            var existing = await this.storage.SelectProformaByIdAsync(id);
            if (existing == null || !existing.BelongsToCompany(this.companyContext.GetCurrentCompanyId())) return NotFound();

            var draftErr = DocumentLifecycleRules.RejectIfNotDraft(existing.Status, "proforma");
            if (draftErr != null) return BadRequest(draftErr);

            existing.CustomerId = proforma.CustomerId;
            existing.Date = proforma.Date == default ? existing.Date : proforma.Date;
            existing.Notes = proforma.Notes;
            if (proforma.Lines != null && proforma.Lines.Count > 0)
                existing.Lines = proforma.Lines;

            RecalcTotals(existing);

            var updated = await this.storage.UpdateProformaAsync(existing);
            await AuditProformaAsync(updated.Id, "Updated", $"Modification proforma {updated.ProformaNumber}");
            return Ok(updated);
        }

        /// <summary>RG-PF3 : Draft → Sent (aucun effet GL/stock).</summary>
        [HttpPost("{id:int}/send")]
        [RequirePermission(Permissions.InvoiceUpdate)]
        public async Task<IActionResult> Send(int id)
        {
            var proforma = await this.storage.SelectProformaByIdAsync(id);
            if (proforma == null || !proforma.BelongsToCompany(this.companyContext.GetCurrentCompanyId())) return NotFound();

            var sendErr = DocumentLifecycleRules.RejectIfProformaCannotSend(proforma.Status);
            if (sendErr != null) return Conflict(new { error = sendErr });

            proforma.Status = "Sent";
            var updated = await this.storage.UpdateProformaAsync(proforma);
            await AuditProformaAsync(updated.Id, "Sent", $"Envoi proforma {updated.ProformaNumber}");
            return Ok(updated);
        }

        [HttpPost("{id:int}/cancel")]
        [RequirePermission(Permissions.InvoiceUpdate)]
        public async Task<IActionResult> Cancel(int id, [FromBody] CancelRequest? request)
        {
            var proforma = await this.storage.SelectProformaByIdAsync(id);
            if (proforma == null || !proforma.BelongsToCompany(this.companyContext.GetCurrentCompanyId())) return NotFound();

            var cancelErr = DocumentLifecycleRules.RejectIfProformaCannotCancel(proforma.Status);
            if (cancelErr != null) return Conflict(new { error = cancelErr });

            proforma.Status = "Cancelled";
            var motif = string.IsNullOrWhiteSpace(request?.Reason) ? "Annulation" : request!.Reason!.Trim();
            proforma.Notes = string.IsNullOrWhiteSpace(proforma.Notes) ? motif : $"{proforma.Notes}\n{motif}";

            var updated = await this.storage.UpdateProformaAsync(proforma);
            await AuditProformaAsync(updated.Id, "Cancelled", $"Annulation proforma {updated.ProformaNumber}", motif);
            return Ok(updated);
        }

        /// <summary>RG-PF4 : suppression (soft) réservée aux proformas Draft ; aucune conversion directe en facture n'existe.</summary>
        [HttpDelete("{id:int}")]
        [RequirePermission(Permissions.InvoiceUpdate)]
        public async Task<IActionResult> Delete(int id)
        {
            var existing = await this.storage.SelectProformaByIdAsync(id);
            if (existing == null || !existing.BelongsToCompany(this.companyContext.GetCurrentCompanyId())) return NotFound();

            var deleteErr = DocumentLifecycleRules.RejectIfProformaCannotDelete(existing.Status);
            if (deleteErr != null) return BadRequest(deleteErr);

            var actor = SalesDocumentAudit.ActorFrom(User);
            SalesBusinessRules.SoftDelete(existing, actor);
            await this.storage.DeleteProformaAsync(existing);
            await AuditProformaAsync(existing.Id, "Deleted", $"Suppression soft proforma {existing.ProformaNumber}");
            return NoContent();
        }

        private static void RecalcTotals(Proforma proforma)
        {
            proforma.Lines ??= new List<ProformaLine>();
            for (var i = 0; i < proforma.Lines.Count; i++)
            {
                var line = proforma.Lines[i];
                line.LineNumber = line.LineNumber <= 0 ? i + 1 : line.LineNumber;
                line.TotalHT = line.Quantity * line.UnitPrice;
                line.TotalTTC = line.TotalHT * (1 + (line.VatRate / 100m));
            }

            proforma.TotalHT = proforma.Lines.Sum(l => l.TotalHT);
            proforma.TotalVat = proforma.Lines.Sum(l => l.TotalTTC - l.TotalHT);
            proforma.TotalTTC = proforma.Lines.Sum(l => l.TotalTTC);
        }

        private async Task AuditProformaAsync(int proformaId, string action, string summary, string? details = null)
        {
            await SalesDocumentAudit.LogAsync(
                this.storage,
                this.companyContext.GetCurrentCompanyId(),
                "Proforma",
                proformaId,
                action,
                SalesDocumentAudit.ActorFrom(User),
                summary,
                details);
        }
    }
}
