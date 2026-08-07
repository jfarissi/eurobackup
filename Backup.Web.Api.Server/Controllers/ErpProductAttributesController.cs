using System;
using System.Linq;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Authorization;
using Authorize = Microsoft.AspNetCore.Authorization.AuthorizeAttribute;
using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Models.Catalog;
using Backup.Web.Api.Server.Models.Security;
using Backup.Web.Api.Server.Services.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RESTFulSense.Controllers;

namespace Backup.Web.Api.Server.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/erpproduct-attributes")]
    public class ErpProductAttributesController : RESTFulController
    {
        private readonly IStorageBroker storage;
        private readonly ICompanyContextService companyContext;

        public ErpProductAttributesController(IStorageBroker storage, ICompanyContextService companyContext)
        {
            this.storage = storage;
            this.companyContext = companyContext;
        }

        [HttpGet("definitions")]
        [RequirePermission(Permissions.ProductRead)]
        public IActionResult GetDefinitions()
        {
            var companyId = this.companyContext.GetCurrentCompanyId();
            if (string.IsNullOrWhiteSpace(companyId)) return BadRequest(new { error = "Société requise." });

            var list = this.storage.SelectAllErpProductAttributeDefinitions()
                .Where(d => d.CompanyId == companyId)
                .OrderBy(d => d.Name)
                .AsNoTracking()
                .ToList();
            return Ok(list);
        }

        [HttpPost("definitions")]
        [RequirePermission(Permissions.ProductCreate)]
        public async Task<IActionResult> CreateDefinition([FromBody] ErpProductAttributeDefinition dto)
        {
            var companyId = this.companyContext.GetCurrentCompanyId();
            if (string.IsNullOrWhiteSpace(companyId)) return BadRequest(new { error = "Société requise." });
            if (dto == null || string.IsNullOrWhiteSpace(dto.Code) || string.IsNullOrWhiteSpace(dto.Name))
                return BadRequest(new { error = "Code et Name requis." });

            dto.Id = Guid.NewGuid();
            dto.CompanyId = companyId;
            dto.CreatedBy = User.Identity?.Name;
            var created = await this.storage.InsertErpProductAttributeDefinitionAsync(dto);
            return Ok(created);
        }

        [HttpPut("definitions/{id:guid}")]
        [RequirePermission(Permissions.ProductUpdate)]
        public async Task<IActionResult> UpdateDefinition(Guid id, [FromBody] ErpProductAttributeDefinition dto)
        {
            var companyId = this.companyContext.GetCurrentCompanyId();
            if (string.IsNullOrWhiteSpace(companyId)) return BadRequest(new { error = "Société requise." });

            var existing = await this.storage.SelectErpProductAttributeDefinitionByIdAsync(id);
            if (existing == null) return NotFound();
            if (existing.CompanyId != companyId)
                return Forbid();

            existing.Code = dto.Code?.Trim() ?? existing.Code;
            existing.Name = dto.Name?.Trim() ?? existing.Name;
            existing.IsActive = dto.IsActive;
            existing.UpdatedBy = User.Identity?.Name;
            var updated = await this.storage.UpdateErpProductAttributeDefinitionAsync(existing);
            return Ok(updated);
        }

        [HttpDelete("definitions/{id:guid}")]
        [RequirePermission(Permissions.ProductDelete)]
        public async Task<IActionResult> DeleteDefinition(Guid id)
        {
            var companyId = this.companyContext.GetCurrentCompanyId();
            if (string.IsNullOrWhiteSpace(companyId)) return BadRequest(new { error = "Société requise." });

            var existing = await this.storage.SelectErpProductAttributeDefinitionByIdAsync(id);
            if (existing == null) return NotFound();
            if (existing.CompanyId != companyId)
                return Forbid();
            await this.storage.DeleteErpProductAttributeDefinitionAsync(existing);
            return NoContent();
        }

        [HttpGet("values/product/{productId:int}")]
        [RequirePermission(Permissions.ProductRead)]
        public IActionResult GetValuesByProduct(int productId)
        {
            var list = this.storage.SelectAllErpProductAttributeValues()
                .Where(v => v.ProductId == productId)
                .Include(v => v.Attribute)
                .AsNoTracking()
                .ToList();
            return Ok(list);
        }

        [HttpPost("values")]
        [RequirePermission(Permissions.ProductUpdate)]
        public async Task<IActionResult> UpsertValue([FromBody] ErpProductAttributeValue dto)
        {
            if (dto == null || dto.ProductId <= 0 || dto.AttributeId == Guid.Empty)
                return BadRequest(new { error = "ProductId et AttributeId requis." });

            var product = await this.storage.SelectErpProductByIdAsync(dto.ProductId);
            if (product == null)
                return BadRequest(new { error = "Produit introuvable." });

            var attr = await this.storage.SelectErpProductAttributeDefinitionByIdAsync(dto.AttributeId);
            if (attr == null)
                return BadRequest(new { error = "Attribut introuvable." });

            var existing = this.storage.SelectAllErpProductAttributeValues()
                .FirstOrDefault(v => v.ProductId == dto.ProductId && v.AttributeId == dto.AttributeId);

            if (existing != null)
            {
                existing.Value = dto.Value ?? string.Empty;
                existing.UpdatedBy = User.Identity?.Name;
                return Ok(await this.storage.UpdateErpProductAttributeValueAsync(existing));
            }

            dto.Id = Guid.NewGuid();
            dto.CreatedBy = User.Identity?.Name;
            dto.Product = null;
            dto.Attribute = null;
            return Ok(await this.storage.InsertErpProductAttributeValueAsync(dto));
        }

        [HttpDelete("values/{id:guid}")]
        [RequirePermission(Permissions.ProductDelete)]
        public async Task<IActionResult> DeleteValue(Guid id)
        {
            var existing = await this.storage.SelectErpProductAttributeValueByIdAsync(id);
            if (existing == null) return NotFound();
            await this.storage.DeleteErpProductAttributeValueAsync(existing);
            return NoContent();
        }
    }
}
