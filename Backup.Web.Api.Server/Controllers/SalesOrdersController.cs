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
    public class SalesOrdersController : RESTFulController
    {
        private readonly IStorageBroker storage;
        private readonly INumberingSequenceService numberingService;
        private readonly ICompanyContextService companyContext;

        public SalesOrdersController(IStorageBroker storage, INumberingSequenceService numberingService, ICompanyContextService companyContext)
        {
            this.storage = storage;
            this.numberingService = numberingService;
            this.companyContext = companyContext;
        }

        [HttpGet]
        [RequirePermission(Permissions.OrderRead)]
        public IActionResult GetAll([FromQuery] string? search = null)
        {
            var query = this.storage.SelectAllSalesOrders().ForCompany(this.companyContext.GetCurrentCompanyId());
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.ToLowerInvariant();
                query = query.Where(o => o.OrderNumber.ToLower().Contains(s) || (o.Customer != null && o.Customer.Name.ToLower().Contains(s)));
            }
            return Ok(query.OrderByDescending(o => o.Date).ToList());
        }

        [HttpGet("{id:int}")]
        [RequirePermission(Permissions.OrderRead)]
        public async Task<IActionResult> GetById(int id)
        {
            var order = await this.storage.SelectSalesOrderByIdAsync(id);
            if (order == null || !order.BelongsToCompany(this.companyContext.GetCurrentCompanyId())) return NotFound();
            return Ok(order);
        }

        [HttpPost]
        [RequirePermission(Permissions.OrderCreate)]
        public async Task<IActionResult> Post([FromBody] SalesOrder order)
        {
            order.EnsureCompanyId(this.companyContext.GetCurrentCompanyId());
            if (string.IsNullOrWhiteSpace(order.OrderNumber))
            {
                order.OrderNumber = await this.numberingService.GetNextNumberAsync("Order", order.CompanyId);
            }
            order.Date = order.Date == default ? DateTime.UtcNow : order.Date;
            order.CreatedAt = DateTime.UtcNow;
            
            order.TotalHT = order.Lines.Sum(l => l.Quantity * l.UnitPrice);
            order.TotalVat = order.Lines.Sum(l => l.Quantity * l.UnitPrice * (l.VatRate / 100m));
            order.TotalTTC = order.TotalHT + order.TotalVat;

            var created = await this.storage.InsertSalesOrderAsync(order);
            return Created(created);
        }

        [HttpPost("{id:int}/convert-to-invoice")]
        [RequirePermission(Permissions.InvoiceCreate)]
        public async Task<IActionResult> ConvertToInvoice(int id)
        {
            var order = await this.storage.SelectSalesOrderByIdAsync(id);
            if (order == null || !order.BelongsToCompany(this.companyContext.GetCurrentCompanyId())) return NotFound("Order not found");

            order.Status = "Invoiced";
            await this.storage.UpdateSalesOrderAsync(order);

            var invoice = new SalesInvoice
            {
                InvoiceNumber = await this.numberingService.GetNextNumberAsync("Invoice", order.CompanyId),
                CustomerId = order.CustomerId,
                SalesOrderId = order.Id,
                Date = DateTime.UtcNow,
                DueDate = DateTime.UtcNow.AddDays(30),
                Status = "Draft",
                TotalHT = order.TotalHT,
                TotalVat = order.TotalVat,
                TotalTTC = order.TotalTTC,
                Notes = $"Facture générée depuis la commande client {order.OrderNumber}",
                CompanyId = order.CompanyId ?? this.companyContext.GetCurrentCompanyId(),
                CreatedAt = DateTime.UtcNow,
                Lines = order.Lines.Select((l, i) => new SalesInvoiceLine
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

            var createdInvoice = await this.storage.InsertSalesInvoiceAsync(invoice);
            return Ok(createdInvoice);
        }
    }
}
