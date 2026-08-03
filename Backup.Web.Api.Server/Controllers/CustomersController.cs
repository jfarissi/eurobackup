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
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class CustomersController : RESTFulController
    {
        private readonly IStorageBroker storage;
        private readonly ICompanyContextService companyContext;

        public CustomersController(IStorageBroker storage, ICompanyContextService companyContext)
        {
            this.storage = storage;
            this.companyContext = companyContext;
        }

        [HttpGet]
        [RequirePermission(Permissions.CustomerRead)]
        public IActionResult GetAll([FromQuery] string? search = null)
        {
            var query = this.storage.SelectAllCustomers().ForCompany(this.companyContext.GetCurrentCompanyId());
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.ToLowerInvariant();
                query = query.Where(c => c.Name.ToLower().Contains(s) || c.CustomerCode.ToLower().Contains(s));
            }
            return Ok(query.OrderBy(c => c.Name).ToList());
        }

        [HttpGet("{id:int}")]
        [RequirePermission(Permissions.CustomerRead)]
        public async Task<IActionResult> GetById(int id)
        {
            var customer = await this.storage.SelectCustomerByIdAsync(id);
            if (customer == null || !customer.BelongsToCompany(this.companyContext.GetCurrentCompanyId())) return NotFound();
            return Ok(customer);
        }

        [HttpPost]
        [RequirePermission(Permissions.CustomerCreate)]
        public async Task<IActionResult> Post([FromBody] Customer customer)
        {
            if (string.IsNullOrWhiteSpace(customer.Name)) return BadRequest("Name required");
            if (string.IsNullOrWhiteSpace(customer.CustomerCode))
            {
                customer.CustomerCode = "CUST-" + Guid.NewGuid().ToString("N")[..6].ToUpper();
            }
            customer.EnsureCompanyId(this.companyContext.GetCurrentCompanyId());
            customer.CreatedAt = DateTime.UtcNow;
            customer.UpdatedAt = DateTime.UtcNow;
            var created = await this.storage.InsertCustomerAsync(customer);
            return Created(created);
        }

        [HttpPut("{id:int}")]
        [RequirePermission(Permissions.CustomerUpdate)]
        public async Task<IActionResult> Put(int id, [FromBody] Customer customer)
        {
            var existing = await this.storage.SelectCustomerByIdAsync(id);
            if (existing == null || !existing.BelongsToCompany(this.companyContext.GetCurrentCompanyId())) return NotFound();

            existing.Name = customer.Name;
            existing.CustomerCode = customer.CustomerCode;
            existing.VatNumber = customer.VatNumber;
            existing.Address = customer.Address;
            existing.City = customer.City;
            existing.PostalCode = customer.PostalCode;
            existing.Country = customer.Country;
            existing.Email = customer.Email;
            existing.Phone = customer.Phone;
            existing.Balance = customer.Balance;
            existing.CreditLimit = customer.CreditLimit;
            existing.PaymentTerms = customer.PaymentTerms;
            if (!string.IsNullOrWhiteSpace(customer.Status))
                existing.Status = customer.Status.Trim();
            existing.UpdatedAt = DateTime.UtcNow;
            if (!string.IsNullOrWhiteSpace(customer.CompanyId))
                existing.CompanyId = customer.CompanyId;

            var updated = await this.storage.UpdateCustomerAsync(existing);
            return Ok(updated);
        }

        [HttpDelete("{id:int}")]
        [RequirePermission(Permissions.CustomerDelete)]
        public async Task<IActionResult> Delete(int id)
        {
            var existing = await this.storage.SelectCustomerByIdAsync(id);
            if (existing == null || !existing.BelongsToCompany(this.companyContext.GetCurrentCompanyId())) return NotFound();
            await this.storage.DeleteCustomerAsync(existing);
            return NoContent();
        }
    }
}
