using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Authorization;
using MsAuthorize = Microsoft.AspNetCore.Authorization.AuthorizeAttribute;
using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Models.Security;
using Backup.Web.Api.Server.Services.Diagrams;
using Backup.Web.Api.Server.Services.Modules;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RESTFulSense.Controllers;

namespace Backup.Web.Api.Server.Controllers
{
    [MsAuthorize]
    [ApiController]
    [Route("api/product-diagrams")]
    [RequireModule(ModuleCodes.AutoParts)]
    public class ProductDiagramsController : RESTFulController
    {
        private readonly IStorageBroker storage;
        private readonly ILogger<ProductDiagramsController> logger;

        public ProductDiagramsController(
            IStorageBroker storage,
            ILogger<ProductDiagramsController> logger)
        {
            this.storage = storage;
            this.logger = logger;
        }

        [HttpGet("{productId:int}")]
        [RequirePermission(Permissions.ProductRead)]
        public async Task<IActionResult> GetByProduct(int productId, CancellationToken ct)
        {
            try
            {
                var diagrams = await this.storage.SelectAllErpProductDiagrams()
                    .AsNoTracking()
                    .Where(d => d.ProductId == productId)
                    .OrderBy(d => d.SortOrder)
                    .ToListAsync(ct);

                var targetIds = diagrams
                    .SelectMany(d => d.Hotspots)
                    .Select(h => h.TargetProductId)
                    .Distinct()
                    .ToList();

                var targets = await this.storage.SelectAllErpProducts()
                    .AsNoTracking()
                    .Where(p => targetIds.Contains(p.Id))
                    .Select(p => new { p.Id, p.Name, p.Reference })
                    .ToDictionaryAsync(p => p.Id, ct);

                var result = diagrams.Select(d => new ProductDiagramDto
                {
                    Id = d.Id,
                    ProductId = d.ProductId,
                    Title = d.Title,
                    ImageUrl = d.ImageUrl,
                    MediaKind = d.MediaKind,
                    Source = d.Source,
                    Hotspots = d.Hotspots
                        .OrderBy(h => h.SortOrder)
                        .Select(h =>
                        {
                            targets.TryGetValue(h.TargetProductId, out var t);
                            return DiagramHotspotDto.FromJson(
                                h.Id,
                                h.Label,
                                h.Shape,
                                h.CoordsJson,
                                h.TargetProductId,
                                t?.Name,
                                t?.Reference);
                        })
                        .ToList()
                }).ToList();

                return Ok(result);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "GET product-diagrams {ProductId} failed", productId);
                return StatusCode(500, new { error = "Échec du schéma éclaté." });
            }
        }
    }
}
