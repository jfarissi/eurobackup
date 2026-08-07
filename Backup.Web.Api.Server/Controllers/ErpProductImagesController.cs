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
    [Route("api/erpproductimages")]
    public class ErpProductImagesController : RESTFulController
    {
        private readonly IStorageBroker storage;

        public ErpProductImagesController(IStorageBroker storage)
        {
            this.storage = storage;
        }

        [HttpGet("product/{productId:int}")]
        [RequirePermission(Permissions.ProductRead)]
        public IActionResult GetByProduct(int productId)
        {
            var list = this.storage.SelectAllErpProductImages()
                .Where(i => i.ProductId == productId)
                .OrderBy(i => i.SortOrder)
                .ThenBy(i => i.CreatedAt)
                .AsNoTracking()
                .ToList();
            return Ok(list);
        }

        [HttpGet("{id:guid}")]
        [RequirePermission(Permissions.ProductRead)]
        public async Task<IActionResult> GetById(Guid id)
        {
            var image = await this.storage.SelectErpProductImageByIdAsync(id);
            if (image == null) return NotFound();
            return Ok(image);
        }

        [HttpPost]
        [RequirePermission(Permissions.ProductCreate)]
        public async Task<IActionResult> Create([FromBody] ErpProductImage dto)
        {
            if (dto == null || dto.ProductId <= 0)
                return BadRequest(new { error = "ProductId requis." });
            if (string.IsNullOrWhiteSpace(dto.Url))
                return BadRequest(new { error = "Url requise." });

            var product = await this.storage.SelectErpProductByIdAsync(dto.ProductId);
            if (product == null)
                return BadRequest(new { error = "Produit introuvable." });

            if (dto.IsMain)
                await ClearMainAsync(dto.ProductId);

            dto.Id = Guid.NewGuid();
            dto.CreatedBy = User.Identity?.Name;
            dto.Product = null;
            var created = await this.storage.InsertErpProductImageAsync(dto);
            return Ok(created);
        }

        [HttpPut("{id:guid}")]
        [RequirePermission(Permissions.ProductUpdate)]
        public async Task<IActionResult> Update(Guid id, [FromBody] ErpProductImage dto)
        {
            var existing = await this.storage.SelectErpProductImageByIdAsync(id);
            if (existing == null) return NotFound();

            if (dto.IsMain && !existing.IsMain)
                await ClearMainAsync(existing.ProductId);

            existing.Url = dto.Url?.Trim() ?? existing.Url;
            existing.AltText = dto.AltText ?? string.Empty;
            existing.IsMain = dto.IsMain;
            existing.SortOrder = dto.SortOrder;
            existing.UpdatedBy = User.Identity?.Name;

            var updated = await this.storage.UpdateErpProductImageAsync(existing);
            return Ok(updated);
        }

        [HttpDelete("{id:guid}")]
        [RequirePermission(Permissions.ProductDelete)]
        public async Task<IActionResult> Delete(Guid id)
        {
            var existing = await this.storage.SelectErpProductImageByIdAsync(id);
            if (existing == null) return NotFound();
            await this.storage.DeleteErpProductImageAsync(existing);
            return NoContent();
        }

        private async Task ClearMainAsync(int productId)
        {
            var mains = this.storage.SelectAllErpProductImages()
                .Where(i => i.ProductId == productId && i.IsMain)
                .ToList();
            foreach (var img in mains)
            {
                img.IsMain = false;
                await this.storage.UpdateErpProductImageAsync(img);
            }
        }
    }
}
