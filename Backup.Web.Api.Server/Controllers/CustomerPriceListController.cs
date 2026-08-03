using System;
using System.Linq;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Authorization;
using Authorize = Microsoft.AspNetCore.Authorization.AuthorizeAttribute;
using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Models.Entities;
using Backup.Web.Api.Server.Models.Security;
using Backup.Web.Api.Server.Services.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RESTFulSense.Controllers;

namespace Backup.Web.Api.Server.Controllers
{
    /// <summary>
    /// RG-PT1–5 lite : tarifs spécifiques client (fallback utilisé sur les lignes Devis/Commande à prix vide).
    /// </summary>
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class CustomerPriceListController : RESTFulController
    {
        private readonly IStorageBroker storage;
        private readonly ICompanyContextService companyContext;

        public CustomerPriceListController(IStorageBroker storage, ICompanyContextService companyContext)
        {
            this.storage = storage;
            this.companyContext = companyContext;
        }

        [HttpGet]
        [RequirePermission(Permissions.CustomerRead)]
        public IActionResult GetAll([FromQuery] int? customerId = null)
        {
            var query = this.storage.SelectAllCustomerPriceListItems().ForCompany(this.companyContext.GetCurrentCompanyId());
            if (customerId.HasValue) query = query.Where(p => p.CustomerId == customerId.Value);
            return Ok(query.OrderBy(p => p.CustomerId).ThenBy(p => p.ProductKey).ToList());
        }

        [HttpGet("{id:int}")]
        [RequirePermission(Permissions.CustomerRead)]
        public async Task<IActionResult> GetById(int id)
        {
            var item = await this.storage.SelectCustomerPriceListItemByIdAsync(id);
            if (item == null || !item.BelongsToCompany(this.companyContext.GetCurrentCompanyId())) return NotFound();
            return Ok(item);
        }

        [HttpPost]
        [RequirePermission(Permissions.CustomerUpdate)]
        public async Task<IActionResult> Post([FromBody] CustomerPriceListItem item)
        {
            item.EnsureCompanyId(this.companyContext.GetCurrentCompanyId());
            var customer = await this.storage.SelectCustomerByIdAsync(item.CustomerId);
            if (customer == null || !customer.BelongsToCompany(item.CompanyId)) return BadRequest("Client introuvable.");
            if (string.IsNullOrWhiteSpace(item.ProductKey)) return BadRequest("ProductKey requis.");
            if (item.UnitPrice < 0) return BadRequest("Le prix ne peut pas être négatif.");

            item.ProductKey = item.ProductKey.Trim();
            item.CreatedAt = DateTime.UtcNow;
            var created = await this.storage.InsertCustomerPriceListItemAsync(item);
            return Created(created);
        }

        [HttpPut("{id:int}")]
        [RequirePermission(Permissions.CustomerUpdate)]
        public async Task<IActionResult> Put(int id, [FromBody] CustomerPriceListItem item)
        {
            var existing = await this.storage.SelectCustomerPriceListItemByIdAsync(id);
            if (existing == null || !existing.BelongsToCompany(this.companyContext.GetCurrentCompanyId())) return NotFound();
            if (item.UnitPrice < 0) return BadRequest("Le prix ne peut pas être négatif.");

            existing.ProductKey = string.IsNullOrWhiteSpace(item.ProductKey) ? existing.ProductKey : item.ProductKey.Trim();
            existing.UnitPrice = item.UnitPrice;
            existing.VatRate = item.VatRate;
            existing.ValidFrom = item.ValidFrom;
            existing.ValidTo = item.ValidTo;

            var updated = await this.storage.UpdateCustomerPriceListItemAsync(existing);
            return Ok(updated);
        }

        [HttpDelete("{id:int}")]
        [RequirePermission(Permissions.CustomerUpdate)]
        public async Task<IActionResult> Delete(int id)
        {
            var existing = await this.storage.SelectCustomerPriceListItemByIdAsync(id);
            if (existing == null || !existing.BelongsToCompany(this.companyContext.GetCurrentCompanyId())) return NotFound();
            await this.storage.DeleteCustomerPriceListItemAsync(existing);
            return NoContent();
        }
    }
}
