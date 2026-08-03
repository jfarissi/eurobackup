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
    public class SuppliersController : RESTFulController
    {
        private readonly IStorageBroker storage;
        private readonly ICompanyContextService companyContext;

        public SuppliersController(IStorageBroker storage, ICompanyContextService companyContext)
        {
            this.storage = storage;
            this.companyContext = companyContext;
        }

        [HttpGet]
        [RequirePermission(Permissions.SupplierRead)]
        public IActionResult GetAll([FromQuery] string? search = null)
        {
            var query = this.storage.SelectAllSuppliers().ForCompany(this.companyContext.GetCurrentCompanyId());
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.ToLowerInvariant();
                query = query.Where(sup => sup.Name.ToLower().Contains(s) || sup.SupplierCode.ToLower().Contains(s));
            }
            return Ok(query.OrderBy(sup => sup.Name).ToList());
        }

        [HttpGet("{id:int}")]
        [RequirePermission(Permissions.SupplierRead)]
        public async Task<IActionResult> GetById(int id)
        {
            var supplier = await this.storage.SelectSupplierByIdAsync(id);
            if (supplier == null || !supplier.BelongsToCompany(this.companyContext.GetCurrentCompanyId())) return NotFound();
            return Ok(supplier);
        }

        [HttpPost]
        [RequirePermission(Permissions.SupplierCreate)]
        public async Task<IActionResult> Post([FromBody] Supplier supplier)
        {
            if (string.IsNullOrWhiteSpace(supplier.Name)) return BadRequest("Name required");
            if (string.IsNullOrWhiteSpace(supplier.SupplierCode))
            {
                supplier.SupplierCode = "SUP-" + Guid.NewGuid().ToString("N")[..6].ToUpper();
            }
            supplier.EnsureCompanyId(this.companyContext.GetCurrentCompanyId());
            supplier.CreatedAt = DateTime.UtcNow;
            supplier.UpdatedAt = DateTime.UtcNow;
            var created = await this.storage.InsertSupplierAsync(supplier);
            return Created(created);
        }

        [HttpPut("{id:int}")]
        [RequirePermission(Permissions.SupplierUpdate)]
        public async Task<IActionResult> Put(int id, [FromBody] Supplier supplier)
        {
            var existing = await this.storage.SelectSupplierByIdAsync(id);
            if (existing == null || !existing.BelongsToCompany(this.companyContext.GetCurrentCompanyId())) return NotFound();

            existing.Name = supplier.Name;
            existing.VatNumber = supplier.VatNumber;
            existing.Address = supplier.Address;
            existing.City = supplier.City;
            existing.PostalCode = supplier.PostalCode;
            existing.Country = supplier.Country;
            existing.Email = supplier.Email;
            existing.Phone = supplier.Phone;
            existing.PaymentTerms = supplier.PaymentTerms;
            existing.IsActive = supplier.IsActive;
            if (!string.IsNullOrWhiteSpace(supplier.Status))
                existing.Status = supplier.Status.Trim();
            else if (!supplier.IsActive)
                existing.Status = "Blocked";
            existing.UpdatedAt = DateTime.UtcNow;

            var updated = await this.storage.UpdateSupplierAsync(existing);
            return Ok(updated);
        }

        [HttpDelete("{id:int}")]
        [RequirePermission(Permissions.SupplierDelete)]
        public async Task<IActionResult> Delete(int id)
        {
            var existing = await this.storage.SelectSupplierByIdAsync(id);
            if (existing == null || !existing.BelongsToCompany(this.companyContext.GetCurrentCompanyId())) return NotFound();
            await this.storage.DeleteSupplierAsync(existing);
            return NoContent();
        }
    }
}
