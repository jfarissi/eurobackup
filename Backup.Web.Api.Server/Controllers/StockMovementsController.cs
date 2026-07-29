using System;
using System.Linq;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Authorization;
using Authorize = Microsoft.AspNetCore.Authorization.AuthorizeAttribute;
using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Models;
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
    public class StockMovementsController : RESTFulController
    {
        private readonly IStorageBroker storage;
        private readonly ICompanyContextService companyContext;

        public StockMovementsController(IStorageBroker storage, ICompanyContextService companyContext)
        {
            this.storage = storage;
            this.companyContext = companyContext;
        }

        [HttpGet]
        [RequirePermission(Permissions.StockRead)]
        public IActionResult GetAll([FromQuery] string? productKey = null)
        {
            var query = this.storage.SelectAllStockMovements().ForCompany(this.companyContext.GetCurrentCompanyId());
            if (!string.IsNullOrWhiteSpace(productKey))
            {
                var k = productKey.ToLowerInvariant();
                query = query.Where(m => m.ProductKey.ToLower().Contains(k));
            }
            return Ok(query.OrderByDescending(m => m.CreatedAt).ToList());
        }

        [HttpPost]
        [RequirePermission(Permissions.StockUpdate)]
        public async Task<IActionResult> Post([FromBody] StockMovement movement)
        {
            if (string.IsNullOrWhiteSpace(movement.ProductKey)) return BadRequest("ProductKey required");
            if (movement.Quantity == 0) return BadRequest("Quantity must be non-zero");

            movement.EnsureCompanyId(this.companyContext.GetCurrentCompanyId());
            movement.CreatedAt = DateTime.UtcNow;
            movement.CreatedBy = User.Identity?.Name ?? "System";

            var created = await this.storage.InsertStockMovementAsync(movement);

            var companyId = this.companyContext.GetCurrentCompanyId();
            var existingStock = this.storage.SelectAllStock()
                .ForCompany(companyId)
                .FirstOrDefault(s => s.ProductKey == movement.ProductKey);
            decimal delta = movement.MovementType switch
            {
                "In" => Math.Abs(movement.Quantity),
                "Out" => -Math.Abs(movement.Quantity),
                "Adjustment" => movement.Quantity,
                "Transfer" => -Math.Abs(movement.Quantity),
                _ => movement.Quantity
            };

            if (existingStock != null)
            {
                existingStock.QuantityOnHand += delta;
                existingStock.LastUpdated = DateTime.UtcNow;
                await this.storage.UpdateStockAsync(existingStock);
            }
            else
            {
                var newStock = new StockItem
                {
                    ProductKey = movement.ProductKey,
                    QuantityOnHand = delta > 0 ? delta : 0,
                    Unit = "PCS",
                    LastUpdated = DateTime.UtcNow,
                    CompanyId = companyId
                };
                await this.storage.InsertStockAsync(newStock);
            }

            return Created(created);
        }
    }
}
