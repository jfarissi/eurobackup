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
using Backup.Web.Api.Server.Services.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RESTFulSense.Controllers;

namespace Backup.Web.Api.Server.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class QuotesController : RESTFulController
    {
        private readonly IStorageBroker storage;
        private readonly INumberingSequenceService numberingService;
        private readonly ICompanyContextService companyContext;
        private readonly IErpPricingService erpPricing;

        public QuotesController(
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
        private async Task ApplyPriceListFallbackAsync(int customerId, string? companyId, List<QuoteLine> lines)
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

        /// <summary>RG-CP3 : recalcule les totaux avec remise ligne + remise pied de page.</summary>
        private static void RecalcQuoteTotals(Quote quote)
        {
            foreach (var line in quote.Lines)
            {
                line.DiscountPercent = SalesBusinessRules.CapDiscountPercent(line.DiscountPercent);
                line.TotalHT = line.Quantity * line.UnitPrice * (1 - (line.DiscountPercent / 100m));
                line.TotalTTC = line.TotalHT * (1 + (line.VatRate / 100m));
            }

            var totalHT = quote.Lines.Sum(l => l.TotalHT);
            var totalVat = quote.Lines.Sum(l => l.TotalTTC - l.TotalHT);
            var totalTTC = quote.Lines.Sum(l => l.TotalTTC);
            SalesBusinessRules.ApplyHeaderDiscount(quote.HeaderDiscountPercent, ref totalHT, ref totalVat, ref totalTTC);
            quote.TotalHT = totalHT;
            quote.TotalVat = totalVat;
            quote.TotalTTC = totalTTC;
        }

        [HttpGet]
        [RequirePermission(Permissions.QuoteRead)]
        public IActionResult GetAll([FromQuery] string? search = null)
        {
            var query = this.storage.SelectAllQuotes().ForCompany(this.companyContext.GetCurrentCompanyId());
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.ToLowerInvariant();
                query = query.Where(q => q.QuoteNumber.ToLower().Contains(s) || (q.Customer != null && q.Customer.Name.ToLower().Contains(s)));
            }
            return Ok(query.OrderByDescending(q => q.Date).ToList());
        }

        [HttpGet("{id:int}")]
        [RequirePermission(Permissions.QuoteRead)]
        public async Task<IActionResult> GetById(int id)
        {
            var quote = await this.storage.SelectQuoteByIdAsync(id);
            if (quote == null || !quote.BelongsToCompany(this.companyContext.GetCurrentCompanyId())) return NotFound();
            return Ok(quote);
        }

        [HttpPost]
        [RequirePermission(Permissions.QuoteCreate)]
        public async Task<IActionResult> Post([FromBody] Quote quote)
        {
            quote.EnsureCompanyId(this.companyContext.GetCurrentCompanyId());

            var customer = await this.storage.SelectCustomerByIdAsync(quote.CustomerId);
            if (customer == null) return BadRequest("Client introuvable.");
            var partyErr = SalesBusinessRules.RejectIfPartyNotActive(customer.Status, customer.Name);
            if (partyErr != null) return BadRequest(partyErr);

            // RG-FC7 : TVA par défaut selon le pays du client si non renseignée sur la ligne.
            var defaultVatRate = VatLocalization.DefaultRateForCountry(customer.Country);
            foreach (var line in quote.Lines)
            {
                if (line.VatRate <= 0) line.VatRate = defaultVatRate;
                var discountErr = SalesBusinessRules.ValidateDiscountPercent(line.DiscountPercent, $"ligne {line.ProductKey}");
                if (discountErr != null) return BadRequest(discountErr);
            }
            var headerDiscountErr = SalesBusinessRules.ValidateDiscountPercent(quote.HeaderDiscountPercent, "remise pied de page");
            if (headerDiscountErr != null) return BadRequest(headerDiscountErr);

            // RG-PT1–5 lite : si prix de ligne non renseigné, tenter le tarif client puis l'ERP.
            await this.ApplyPriceListFallbackAsync(quote.CustomerId, quote.CompanyId, quote.Lines);

            // RG-CP1 : devise figée à la création depuis Company.DefaultCurrencyCode.
            var company = await this.storage.SelectCompanyByIdAsync(quote.CompanyId);
            quote.CurrencyCode = string.IsNullOrWhiteSpace(company?.DefaultCurrencyCode) ? "EUR" : company!.DefaultCurrencyCode;

            if (string.IsNullOrWhiteSpace(quote.QuoteNumber))
            {
                quote.QuoteNumber = await this.numberingService.GetNextNumberAsync("Quote", quote.CompanyId);
            }
            quote.Date = quote.Date == default ? DateTime.UtcNow : quote.Date;
            if (quote.ExpirationDate == default)
                quote.ExpirationDate = quote.Date.AddDays(30);
            quote.CreatedAt = DateTime.UtcNow;
            if (string.IsNullOrWhiteSpace(quote.Status)) quote.Status = "Draft";

            // RG-DV1 : hérite des conditions de paiement du client.
            if (!string.IsNullOrWhiteSpace(customer.PaymentTerms)
                && (string.IsNullOrWhiteSpace(quote.Notes) || !quote.Notes.Contains(customer.PaymentTerms, StringComparison.OrdinalIgnoreCase)))
            {
                var termsNote = $"Conditions de paiement : {customer.PaymentTerms.Trim()}";
                quote.Notes = string.IsNullOrWhiteSpace(quote.Notes)
                    ? termsNote
                    : $"{quote.Notes.TrimEnd()}\n{termsNote}";
            }

            RecalcQuoteTotals(quote);

            var created = await this.storage.InsertQuoteAsync(quote);
            await SalesDocumentAudit.LogAsync(
                this.storage, created.CompanyId ?? this.companyContext.GetCurrentCompanyId(),
                "Quote", created.Id, "Created", SalesDocumentAudit.ActorFrom(User),
                $"Création devis {created.QuoteNumber}");
            return Created(created);
        }

        [HttpPut("{id:int}")]
        [RequirePermission(Permissions.QuoteUpdate)]
        public async Task<IActionResult> Put(int id, [FromBody] Quote quote)
        {
            var existing = await this.storage.SelectQuoteByIdAsync(id);
            if (existing == null || !existing.BelongsToCompany(this.companyContext.GetCurrentCompanyId())) return NotFound();

            if (!SalesBusinessRules.CanFullyEdit(existing.Status)
                && !string.Equals(existing.Status, "Accepted", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest($"Un devis au statut {existing.Status} ne peut plus être modifié.");
            }

            // RG-CP1 : devise figée hors Draft.
            var currencyErr = SalesBusinessRules.RejectCurrencyChangeIfFrozen(existing.Status, existing.CurrencyCode, quote.CurrencyCode);
            if (currencyErr != null) return BadRequest(currencyErr);

            foreach (var line in quote.Lines)
            {
                var discountErr = SalesBusinessRules.ValidateDiscountPercent(line.DiscountPercent, $"ligne {line.ProductKey}");
                if (discountErr != null) return BadRequest(discountErr);
            }
            var headerDiscountErr = SalesBusinessRules.ValidateDiscountPercent(quote.HeaderDiscountPercent, "remise pied de page");
            if (headerDiscountErr != null) return BadRequest(headerDiscountErr);

            // RG-DV7 : au-delà du Draft (Sent/Accepted), toute modification bascule en versionning
            // (édition autorisée mais Version incrémentée + note + audit "Versioned" au lieu d'un simple écrasement).
            var isVersionedEdit = !string.Equals(existing.Status, "Draft", StringComparison.OrdinalIgnoreCase);

            existing.CustomerId = quote.CustomerId;
            existing.Date = quote.Date;
            existing.ExpirationDate = quote.ExpirationDate;
            // Ne pas écraser Accepted/Converted via PUT libre — garder statut serveur sauf Draft/Sent.
            if (SalesBusinessRules.CanFullyEdit(existing.Status) && !string.IsNullOrWhiteSpace(quote.Status))
                existing.Status = quote.Status;
            existing.Notes = quote.Notes;
            existing.Lines = quote.Lines;
            existing.HeaderDiscountPercent = quote.HeaderDiscountPercent;

            RecalcQuoteTotals(existing);

            if (isVersionedEdit)
            {
                existing.Version += 1;
                var versionNote = $"Version {existing.Version} — modifié après envoi le {DateTime.UtcNow:dd/MM/yyyy HH:mm}";
                existing.Notes = string.IsNullOrWhiteSpace(existing.Notes)
                    ? versionNote
                    : $"{existing.Notes.TrimEnd()}\n{versionNote}";
            }

            var updated = await this.storage.UpdateQuoteAsync(existing);
            await SalesDocumentAudit.LogAsync(
                this.storage, updated.CompanyId ?? this.companyContext.GetCurrentCompanyId(),
                "Quote", updated.Id, isVersionedEdit ? "Versioned" : "Updated", SalesDocumentAudit.ActorFrom(User),
                isVersionedEdit
                    ? $"Révision v{updated.Version} devis {updated.QuoteNumber}"
                    : $"Modification devis {updated.QuoteNumber}");
            return Ok(updated);
        }

        /// <summary>RG-DV3 : accepter le devis avant conversion en commande.</summary>
        [HttpPost("{id:int}/accept")]
        [RequirePermission(Permissions.QuoteUpdate)]
        public async Task<IActionResult> Accept(int id)
        {
            var quote = await this.storage.SelectQuoteByIdAsync(id);
            if (quote == null || !quote.BelongsToCompany(this.companyContext.GetCurrentCompanyId())) return NotFound();

            if (SalesBusinessRules.IsQuoteExpired(quote))
            {
                quote.Status = "Expired";
                await this.storage.UpdateQuoteAsync(quote);
                return BadRequest($"Le devis {quote.QuoteNumber} est expiré.");
            }

            var s = quote.Status ?? "";
            if (string.Equals(s, "Converted", StringComparison.OrdinalIgnoreCase)
                || string.Equals(s, "Cancelled", StringComparison.OrdinalIgnoreCase)
                || string.Equals(s, "Rejected", StringComparison.OrdinalIgnoreCase)
                || string.Equals(s, "Refused", StringComparison.OrdinalIgnoreCase)
                || string.Equals(s, "Expired", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest($"Le devis au statut {s} ne peut pas être accepté.");
            }

            if (string.Equals(s, "Accepted", StringComparison.OrdinalIgnoreCase))
                return Ok(quote);

            quote.Status = "Accepted";
            var updated = await this.storage.UpdateQuoteAsync(quote);
            await SalesDocumentAudit.LogAsync(
                this.storage, updated.CompanyId ?? this.companyContext.GetCurrentCompanyId(),
                "Quote", updated.Id, "Accepted", SalesDocumentAudit.ActorFrom(User),
                $"Acceptation devis {updated.QuoteNumber}");
            return Ok(updated);
        }

        /// <summary>RG-DV5 : duplication d'un devis existant → nouveau devis Draft (nouveau numéro).</summary>
        [HttpPost("{id:int}/duplicate")]
        [RequirePermission(Permissions.QuoteCreate)]
        public async Task<IActionResult> Duplicate(int id)
        {
            var source = await this.storage.SelectQuoteByIdAsync(id);
            if (source == null || !source.BelongsToCompany(this.companyContext.GetCurrentCompanyId())) return NotFound();

            var companyId = source.CompanyId ?? this.companyContext.GetCurrentCompanyId();
            var originNote = $"Dupliqué depuis le devis {source.QuoteNumber}";

            var duplicate = new Quote
            {
                QuoteNumber = await this.numberingService.GetNextNumberAsync("Quote", companyId),
                CustomerId = source.CustomerId,
                Date = DateTime.UtcNow,
                ExpirationDate = DateTime.UtcNow.AddDays(30),
                Status = "Draft",
                Notes = string.IsNullOrWhiteSpace(source.Notes) ? originNote : $"{originNote}{Environment.NewLine}{source.Notes}",
                CompanyId = companyId,
                CreatedAt = DateTime.UtcNow,
                Lines = source.Lines.Select((l, i) => new QuoteLine
                {
                    ProductKey = l.ProductKey,
                    Description = l.Description,
                    Quantity = l.Quantity,
                    UnitPrice = l.UnitPrice,
                    VatRate = l.VatRate,
                    TotalHT = l.Quantity * l.UnitPrice,
                    TotalTTC = l.Quantity * l.UnitPrice * (1 + l.VatRate / 100m),
                    LineNumber = i + 1,
                    SupplierId = l.SupplierId
                }).ToList()
            };

            duplicate.TotalHT = duplicate.Lines.Sum(l => l.TotalHT);
            duplicate.TotalVat = duplicate.Lines.Sum(l => l.TotalTTC - l.TotalHT);
            duplicate.TotalTTC = duplicate.Lines.Sum(l => l.TotalTTC);

            var created = await this.storage.InsertQuoteAsync(duplicate);
            await SalesDocumentAudit.LogAsync(
                this.storage, created.CompanyId ?? companyId,
                "Quote", created.Id, "Duplicated", SalesDocumentAudit.ActorFrom(User),
                $"Duplication devis {source.QuoteNumber} → {created.QuoteNumber}");
            return Created(created);
        }

        public class ConvertToOrderLineRequest
        {
            public int? QuoteLineId { get; set; }
            public string? ProductKey { get; set; }
            public decimal Quantity { get; set; }
        }

        public class ConvertToOrderRequest
        {
            /// <summary>RG-DV3 : conversion partielle — si null/vide, convertit le reliquat de toutes les lignes.</summary>
            public List<ConvertToOrderLineRequest>? Lines { get; set; }
        }

        /// <summary>RG-DV3 : devis Accepté (ou PartiellementConverti) → 1..N commandes, conversion totale ou partielle.</summary>
        [HttpPost("{id:int}/convert-to-order")]
        [RequirePermission(Permissions.OrderCreate)]
        public async Task<IActionResult> ConvertToOrder(int id, [FromBody] ConvertToOrderRequest? request)
        {
            var quote = await this.storage.SelectQuoteByIdAsync(id);
            if (quote == null || !quote.BelongsToCompany(this.companyContext.GetCurrentCompanyId())) return NotFound("Quote not found");

            if (SalesBusinessRules.IsQuoteExpired(quote))
            {
                quote.Status = "Expired";
                await this.storage.UpdateQuoteAsync(quote);
            }

            var convertError = SalesBusinessRules.ValidateQuoteConvertible(quote);
            if (convertError != null) return BadRequest(convertError);

            // RG-DV3 : quantités demandées par ligne (n° de ligne ou ProductKey) ; sinon reliquat de chaque ligne.
            var requestedByLineId = new Dictionary<int, decimal>();
            var requestedByProductKey = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            var hasSpecificSelection = false;
            if (request?.Lines != null && request.Lines.Count > 0)
            {
                foreach (var rl in request.Lines)
                {
                    if (rl.Quantity <= 0) continue;
                    hasSpecificSelection = true;
                    if (rl.QuoteLineId.HasValue && rl.QuoteLineId.Value > 0)
                        requestedByLineId[rl.QuoteLineId.Value] = rl.Quantity;
                    else if (!string.IsNullOrWhiteSpace(rl.ProductKey))
                        requestedByProductKey[rl.ProductKey.Trim()] = rl.Quantity;
                }
            }

            var linesToConvert = new List<(QuoteLine Line, decimal Qty)>();
            foreach (var line in quote.Lines)
            {
                var remaining = Math.Max(0m, line.Quantity - line.ConvertedQuantity);
                if (remaining <= 0.0001m) continue;

                decimal qty;
                if (requestedByLineId.TryGetValue(line.Id, out var byId))
                    qty = Math.Min(remaining, byId);
                else if (!string.IsNullOrWhiteSpace(line.ProductKey)
                    && requestedByProductKey.TryGetValue(line.ProductKey.Trim(), out var byKey))
                    qty = Math.Min(remaining, byKey);
                else if (!hasSpecificSelection)
                    qty = remaining;
                else
                    qty = 0m;

                if (qty > 0.0001m) linesToConvert.Add((line, qty));
            }

            if (linesToConvert.Count == 0)
                return BadRequest("Aucune quantité à convertir (déjà convertie ou sélection vide).");

            var convertedHt = linesToConvert.Sum(x => x.Qty * x.Line.UnitPrice);
            var convertedVat = linesToConvert.Sum(x => x.Qty * x.Line.UnitPrice * (x.Line.VatRate / 100m));
            var convertedTtc = convertedHt + convertedVat;

            var customer = await this.storage.SelectCustomerByIdAsync(quote.CustomerId);
            if (customer == null) return BadRequest("Client introuvable.");
            // RG-T5 : le plafond de crédit est vérifié sur le TTC réellement converti, pas le TTC total du devis.
            var creditError = SalesBusinessRules.ValidateCreditLimit(this.storage, customer, convertedTtc);
            if (creditError != null) return BadRequest(creditError);

            var orderCompanyId = quote.CompanyId ?? this.companyContext.GetCurrentCompanyId();
            var order = new SalesOrder
            {
                // RG-CC3 : conversion depuis un devis Accepté = commande déjà engagée → n° définitif immédiat.
                OrderNumber = await this.numberingService.GetNextNumberAsync("Order", quote.CompanyId),
                CustomerId = quote.CustomerId,
                QuoteId = quote.Id,
                Date = DateTime.UtcNow,
                Status = "Confirmed",
                TotalHT = convertedHt,
                TotalVat = convertedVat,
                TotalTTC = convertedTtc,
                HeaderDiscountPercent = quote.HeaderDiscountPercent,
                // RG-CP1 : devise figée à la création depuis Company.DefaultCurrencyCode.
                CurrencyCode = await SalesBusinessRules.ResolveCompanyCurrencyAsync(this.storage, orderCompanyId),
                Notes = $"Converti depuis le devis {quote.QuoteNumber}",
                CompanyId = orderCompanyId,
                CreatedAt = DateTime.UtcNow,
                Lines = linesToConvert.Select((x, i) => new SalesOrderLine
                {
                    ProductKey = x.Line.ProductKey,
                    Description = x.Line.Description,
                    Quantity = x.Qty,
                    UnitPrice = x.Line.UnitPrice,
                    DiscountPercent = x.Line.DiscountPercent,
                    VatRate = x.Line.VatRate,
                    TotalHT = x.Qty * x.Line.UnitPrice,
                    TotalTTC = x.Qty * x.Line.UnitPrice * (1 + x.Line.VatRate / 100m),
                    LineNumber = i + 1,
                    SupplierId = x.Line.SupplierId
                }).ToList()
            };

            foreach (var (line, qty) in linesToConvert)
                line.ConvertedQuantity = Math.Min(line.Quantity, line.ConvertedQuantity + qty);

            var allFullyConverted = quote.Lines.All(l => l.ConvertedQuantity >= l.Quantity - 0.0001m);
            quote.Status = allFullyConverted ? "Converted" : "PartiallyConverted";
            await this.storage.UpdateQuoteAsync(quote);

            var createdOrder = await this.storage.InsertSalesOrderAsync(order);
            await SalesDocumentAudit.LogAsync(
                this.storage, quote.CompanyId ?? this.companyContext.GetCurrentCompanyId(),
                "Quote", quote.Id, "Converted",
                SalesDocumentAudit.ActorFrom(User),
                $"Conversion {(allFullyConverted ? "totale" : "partielle")} devis {quote.QuoteNumber} → commande {createdOrder.OrderNumber}");
            return Ok(createdOrder);
        }

        [HttpPost("{id:int}/cancel")]
        [RequirePermission(Permissions.QuoteUpdate)]
        public async Task<IActionResult> Cancel(int id, [FromBody] CancelRequest? request)
        {
            var quote = await this.storage.SelectQuoteByIdAsync(id);
            if (quote == null || !quote.BelongsToCompany(this.companyContext.GetCurrentCompanyId())) return NotFound();
            if (string.Equals(quote.Status, "Converted", StringComparison.OrdinalIgnoreCase))
                return BadRequest("Un devis déjà converti ne peut pas être annulé.");
            if (string.Equals(quote.Status, "Cancelled", StringComparison.OrdinalIgnoreCase))
                return Conflict(new { error = "Devis déjà annulé." });

            quote.Status = "Cancelled";
            var motif = string.IsNullOrWhiteSpace(request?.Reason) ? "Annulation" : request!.Reason!.Trim();
            quote.Notes = string.IsNullOrWhiteSpace(quote.Notes) ? motif : $"{quote.Notes}\n{motif}";
            await this.storage.UpdateQuoteAsync(quote);
            return Ok(quote);
        }

        [HttpDelete("{id:int}")]
        [RequirePermission(Permissions.QuoteDelete)]
        public async Task<IActionResult> Delete(int id)
        {
            var existing = await this.storage.SelectQuoteByIdAsync(id);
            if (existing == null || !existing.BelongsToCompany(this.companyContext.GetCurrentCompanyId()))
                return NotFound();

            if (!SalesBusinessRules.CanPhysicallyDelete(existing.Status))
                return BadRequest("Seuls les devis Draft peuvent être supprimés. Sinon annulez ou archivez.");

            var actor = SalesDocumentAudit.ActorFrom(User);
            SalesBusinessRules.SoftDelete(existing, actor);
            await this.storage.DeleteQuoteAsync(existing);
            await SalesDocumentAudit.LogAsync(
                this.storage, existing.CompanyId ?? this.companyContext.GetCurrentCompanyId(),
                "Quote", existing.Id, "Deleted", actor,
                $"Suppression soft devis {existing.QuoteNumber}");
            return NoContent();
        }

        public class CancelRequest
        {
            public string? Reason { get; set; }
        }
    }
}
