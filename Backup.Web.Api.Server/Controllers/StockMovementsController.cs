using System;
using System.Linq;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Authorization;
using Authorize = Microsoft.AspNetCore.Authorization.AuthorizeAttribute;
using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Models.Entities;
using Backup.Web.Api.Server.Models.Security;
using Backup.Web.Api.Server.Services.Stock;
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

            var companyId = this.companyContext.GetCurrentCompanyId();
            movement.EnsureCompanyId(companyId);
            var createdBy = User.Identity?.Name ?? "System";

            // Inventaire : surplus (In / Adjustment+) valorisé au coût saisi ou au CMUP ; manquant (Out) au CMUP.
            var created = await StockLedger.ApplyAsync(
                this.storage,
                companyId,
                movement.ProductKey,
                string.IsNullOrWhiteSpace(movement.MovementType) ? "Adjustment" : movement.MovementType,
                movement.Quantity,
                movement.ReferenceDocument ?? "INVENTORY",
                movement.Reason ?? "Ajustement stock manuel",
                createdBy,
                movement.UnitCost);

            return created != null ? Created(created) : Ok(movement);
        }
    }
}
