using System;
using System.Linq;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Authorization;
using Authorize = Microsoft.AspNetCore.Authorization.AuthorizeAttribute;
using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Models.Entities;
using Backup.Web.Api.Server.Models.Security;
using Backup.Web.Api.Server.Services.Numbering;
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

        public QuotesController(IStorageBroker storage, INumberingSequenceService numberingService, ICompanyContextService companyContext)
        {
            this.storage = storage;
            this.numberingService = numberingService;
            this.companyContext = companyContext;
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
            if (string.IsNullOrWhiteSpace(quote.QuoteNumber))
            {
                quote.QuoteNumber = await this.numberingService.GetNextNumberAsync("Quote", quote.CompanyId);
            }
            quote.Date = quote.Date == default ? DateTime.UtcNow : quote.Date;
            quote.CreatedAt = DateTime.UtcNow;
            
            // Re-calculate totals
            quote.TotalHT = quote.Lines.Sum(l => l.Quantity * l.UnitPrice);
            quote.TotalVat = quote.Lines.Sum(l => l.Quantity * l.UnitPrice * (l.VatRate / 100m));
            quote.TotalTTC = quote.TotalHT + quote.TotalVat;

            var created = await this.storage.InsertQuoteAsync(quote);
            return Created(created);
        }

        [HttpPut("{id:int}")]
        [RequirePermission(Permissions.QuoteUpdate)]
        public async Task<IActionResult> Put(int id, [FromBody] Quote quote)
        {
            var existing = await this.storage.SelectQuoteByIdAsync(id);
            if (existing == null || !existing.BelongsToCompany(this.companyContext.GetCurrentCompanyId())) return NotFound();

            existing.CustomerId = quote.CustomerId;
            existing.Date = quote.Date;
            existing.ExpirationDate = quote.ExpirationDate;
            existing.Status = quote.Status;
            existing.Notes = quote.Notes;
            existing.Lines = quote.Lines;

            existing.TotalHT = quote.Lines.Sum(l => l.Quantity * l.UnitPrice);
            existing.TotalVat = quote.Lines.Sum(l => l.Quantity * l.UnitPrice * (l.VatRate / 100m));
            existing.TotalTTC = existing.TotalHT + existing.TotalVat;

            var updated = await this.storage.UpdateQuoteAsync(existing);
            return Ok(updated);
        }

        [HttpPost("{id:int}/convert-to-order")]
        [RequirePermission(Permissions.OrderCreate)]
        public async Task<IActionResult> ConvertToOrder(int id)
        {
            var quote = await this.storage.SelectQuoteByIdAsync(id);
            if (quote == null || !quote.BelongsToCompany(this.companyContext.GetCurrentCompanyId())) return NotFound("Quote not found");

            quote.Status = "Converted";
            await this.storage.UpdateQuoteAsync(quote);

            var order = new SalesOrder
            {
                OrderNumber = await this.numberingService.GetNextNumberAsync("Order", quote.CompanyId),
                CustomerId = quote.CustomerId,
                QuoteId = quote.Id,
                Date = DateTime.UtcNow,
                Status = "Confirmed",
                TotalHT = quote.TotalHT,
                TotalVat = quote.TotalVat,
                TotalTTC = quote.TotalTTC,
                Notes = $"Converti depuis le devis {quote.QuoteNumber}",
                CompanyId = quote.CompanyId ?? this.companyContext.GetCurrentCompanyId(),
                CreatedAt = DateTime.UtcNow,
                Lines = quote.Lines.Select((l, i) => new SalesOrderLine
                {
                    ProductKey = l.ProductKey,
                    Description = l.Description,
                    Quantity = l.Quantity,
                    UnitPrice = l.UnitPrice,
                    VatRate = l.VatRate,
                    TotalHT = l.TotalHT,
                    TotalTTC = l.TotalTTC,
                    LineNumber = i + 1
                }).ToList()
            };

            var createdOrder = await this.storage.InsertSalesOrderAsync(order);
            return Ok(createdOrder);
        }
    }
}
