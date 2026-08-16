using System;
using System.Linq;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Models.Catalog;

namespace Backup.Web.Api.Server.Brokers.Storage
{
    public partial interface IStorageBroker
    {
        IQueryable<ErpProductDiagram> SelectAllErpProductDiagrams();
        ValueTask<ErpProductDiagram> InsertErpProductDiagramAsync(ErpProductDiagram diagram);
        ValueTask<ErpDiagramHotspot> InsertErpDiagramHotspotAsync(ErpDiagramHotspot hotspot);
    }
}
