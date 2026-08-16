using System;
using System.Linq;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Models;
using Backup.Web.Api.Server.Models.Catalog;
using Backup.Web.Api.Server.Services.Modules;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Backup.Web.Api.Server.Services.Tenancy
{
    /// <summary>Schéma éclaté Demo (F6) : kit frein + 3 pièces cliquables.</summary>
    public sealed class ProductDiagramSeedService
    {
        public const string KitErpId = "DEMO-DIAG-KIT";

        private static readonly string DemoSvg = "data:image/svg+xml;utf8," +
            "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 800 480'>" +
            "<rect fill='%23f8fafc' width='800' height='480'/>" +
            "<text x='24' y='36' font-size='20' fill='%230f172a' font-family='sans-serif'>Front brake kit — Demo</text>" +
            "<rect x='40' y='70' width='240' height='280' rx='12' fill='%23e2e8f0' stroke='%2364748b' stroke-width='2'/>" +
            "<text x='160' y='220' text-anchor='middle' font-size='18' fill='%230f172a' font-family='sans-serif'>Disc</text>" +
            "<rect x='320' y='70' width='200' height='160' rx='12' fill='%23e2e8f0' stroke='%2364748b' stroke-width='2'/>" +
            "<text x='420' y='155' text-anchor='middle' font-size='18' fill='%230f172a' font-family='sans-serif'>Pads</text>" +
            "<rect x='560' y='90' width='200' height='240' rx='12' fill='%23e2e8f0' stroke='%2364748b' stroke-width='2'/>" +
            "<text x='660' y='220' text-anchor='middle' font-size='18' fill='%230f172a' font-family='sans-serif'>Caliper</text>" +
            "</svg>";

        private readonly IStorageBroker storage;
        private readonly IModuleService modules;
        private readonly ILogger<ProductDiagramSeedService> logger;

        public ProductDiagramSeedService(
            IStorageBroker storage,
            IModuleService modules,
            ILogger<ProductDiagramSeedService> logger)
        {
            this.storage = storage;
            this.modules = modules;
            this.logger = logger;
        }

        public async Task EnsureDemoDiagramAsync()
        {
            await this.modules.EnsureModuleAsync(TenancySeedService.DefaultCompanyId, ModuleCodes.AutoParts);

            if (await this.storage.SelectAllErpProductDiagrams().AnyAsync(d => d.Source == "demo"))
            {
                this.logger.LogInformation("Diagram seed: demo schema already present");
                return;
            }

            var kit = await this.EnsureProductAsync(KitErpId, "Kit frein avant (Demo)", "DIAG-KIT", 189.00m);
            var disc = await this.EnsureProductAsync("DEMO-DIAG-DISC", "Disque de frein avant (Demo)", "DIAG-DISC", 62.00m);
            var pads = await this.EnsureProductAsync("DEMO-DIAG-PAD", "Plaquettes avant (Demo)", "DIAG-PAD", 34.50m);
            var caliper = await this.EnsureProductAsync("DEMO-DIAG-CAL", "Étrier de frein avant (Demo)", "DIAG-CAL", 94.00m);

            var diagram = await this.storage.InsertErpProductDiagramAsync(new ErpProductDiagram
            {
                ProductId = kit.Id,
                Title = "Schéma éclaté — kit frein avant",
                ImageUrl = DemoSvg,
                MediaKind = "svg",
                Source = "demo",
                CreatedBy = "diagram-seed"
            });

            await this.storage.InsertErpDiagramHotspotAsync(Rect(diagram.Id, "Disque", disc.Id, 5, 14.6, 30, 58.3, 1));
            await this.storage.InsertErpDiagramHotspotAsync(Rect(diagram.Id, "Plaquettes", pads.Id, 40, 14.6, 25, 33.3, 2));
            await this.storage.InsertErpDiagramHotspotAsync(Rect(diagram.Id, "Étrier", caliper.Id, 70, 18.8, 25, 50, 3));

            this.logger.LogInformation(
                "Diagram seed: kit {KitId} ({ErpId}) with 3 hotspots",
                kit.Id, KitErpId);
        }

        private async Task<ErpProduct> EnsureProductAsync(string erpId, string name, string reference, decimal price)
        {
            var existing = await this.storage.SelectAllErpProducts()
                .FirstOrDefaultAsync(p => p.ErpProductId == erpId);
            if (existing != null) return existing;

            return await this.storage.InsertErpProductAsync(new ErpProduct
            {
                ErpProductId = erpId,
                Name = name,
                Reference = reference,
                Brand = "Demo",
                UnitPrice = price,
                RPrice = price,
                CPrice = Math.Round(price * 0.62m, 2),
                TypeVatPerc = 21m,
                StockQuantity = 8,
                DataSource = "Demo",
                CreatedBy = "diagram-seed",
                UpdatedBy = "diagram-seed"
            });
        }

        private static ErpDiagramHotspot Rect(
            Guid diagramId, string label, int targetId,
            double x, double y, double w, double h, int sort) =>
            new()
            {
                DiagramId = diagramId,
                Label = label,
                Shape = "rect",
                CoordsJson = $"{{\"x\":{x.ToString(System.Globalization.CultureInfo.InvariantCulture)},\"y\":{y.ToString(System.Globalization.CultureInfo.InvariantCulture)},\"w\":{w.ToString(System.Globalization.CultureInfo.InvariantCulture)},\"h\":{h.ToString(System.Globalization.CultureInfo.InvariantCulture)}}}",
                TargetProductId = targetId,
                SortOrder = sort
            };
    }
}
