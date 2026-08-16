using System.Linq;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Models.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Backup.Web.Api.Server.Brokers.Storage
{
    public partial class StorageBroker
    {
        public DbSet<ErpProductDiagram> ErpProductDiagrams { get; set; } = null!;
        public DbSet<ErpDiagramHotspot> ErpDiagramHotspots { get; set; } = null!;

        public IQueryable<ErpProductDiagram> SelectAllErpProductDiagrams() =>
            this.ErpProductDiagrams
                .Include(d => d.Hotspots)
                .AsQueryable();

        public async ValueTask<ErpProductDiagram> InsertErpProductDiagramAsync(ErpProductDiagram diagram)
        {
            EntityEntry<ErpProductDiagram> entry = await this.ErpProductDiagrams.AddAsync(diagram);
            await this.SaveChangesAsync();
            return entry.Entity;
        }

        public async ValueTask<ErpDiagramHotspot> InsertErpDiagramHotspotAsync(ErpDiagramHotspot hotspot)
        {
            EntityEntry<ErpDiagramHotspot> entry = await this.ErpDiagramHotspots.AddAsync(hotspot);
            await this.SaveChangesAsync();
            return entry.Entity;
        }
    }
}
