using System;
using System.Linq;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Authorization;
using Authorize = Microsoft.AspNetCore.Authorization.AuthorizeAttribute;
using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Models.Catalog;
using Backup.Web.Api.Server.Models.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RESTFulSense.Controllers;

namespace Backup.Web.Api.Server.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/erpproductvariants")]
    public class ErpProductVariantsController : RESTFulController
    {
        private readonly IStorageBroker storage;

        public ErpProductVariantsController(IStorageBroker storage)
        {
            this.storage = storage;
        }

        [HttpGet("product/{productId:int}")]
        [RequirePermission(Permissions.ProductRead)]
        public IActionResult GetByProduct(int productId)
        {
            var list = this.storage.SelectAllErpProductVariants()
                .Where(v => v.ProductId == productId)
                .OrderBy(v => v.Sku)
                .AsNoTracking()
                .ToList();
            return Ok(list);
        }

        [HttpGet("{id:guid}")]
        [RequirePermission(Permissions.ProductRead)]
        public async Task<IActionResult> GetById(Guid id)
        {
            var variant = await this.storage.SelectErpProductVariantByIdAsync(id);
            if (variant == null) return NotFound();
            return Ok(variant);
        }

        [HttpPost]
        [RequirePermission(Permissions.ProductCreate)]
        public async Task<IActionResult> Create([FromBody] ErpProductVariant dto)
        {
            if (dto == null || dto.ProductId <= 0)
                return BadRequest(new { error = "ProductId requis." });
            if (string.IsNullOrWhiteSpace(dto.Sku))
                return BadRequest(new { error = "Sku requis." });

            var product = await this.storage.SelectErpProductByIdAsync(dto.ProductId);
            if (product == null)
                return BadRequest(new { error = "Produit introuvable." });

            dto.Id = Guid.NewGuid();
            dto.CreatedBy = User.Identity?.Name;
            dto.Product = null;
            var created = await this.storage.InsertErpProductVariantAsync(dto);
            return Ok(created);
        }

        [HttpPut("{id:guid}")]
        [RequirePermission(Permissions.ProductUpdate)]
        public async Task<IActionResult> Update(Guid id, [FromBody] ErpProductVariant dto)
        {
            var existing = await this.storage.SelectErpProductVariantByIdAsync(id);
            if (existing == null) return NotFound();

            existing.Sku = dto.Sku?.Trim() ?? existing.Sku;
            existing.Barcode = string.IsNullOrWhiteSpace(dto.Barcode) ? null : dto.Barcode.Trim();
            existing.CostPrice = dto.CostPrice;
            existing.PriceOverride = dto.PriceOverride;
            existing.StockQuantity = dto.StockQuantity;
            existing.AttributesJson = string.IsNullOrWhiteSpace(dto.AttributesJson) ? "{}" : dto.AttributesJson;
            existing.Weight = dto.Weight;
            existing.Length = dto.Length;
            existing.Width = dto.Width;
            existing.Height = dto.Height;
            existing.IsActive = dto.IsActive;
            existing.UpdatedBy = User.Identity?.Name;

            var updated = await this.storage.UpdateErpProductVariantAsync(existing);
            return Ok(updated);
        }

        [HttpDelete("{id:guid}")]
        [RequirePermission(Permissions.ProductDelete)]
        public async Task<IActionResult> Delete(Guid id)
        {
            var existing = await this.storage.SelectErpProductVariantByIdAsync(id);
            if (existing == null) return NotFound();
            await this.storage.DeleteErpProductVariantAsync(existing);
            return NoContent();
        }
    }
}
