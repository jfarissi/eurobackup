using System.Threading.Tasks;
using Backup.Web.Api.Server.Models.Catalog;

namespace Backup.Web.Api.Server.Brokers.Storage
{
    public partial interface IStorageBroker
    {
        ValueTask<bool> ErpProductVehicleKTypeExistsAsync(string kType);
        ValueTask<ErpKTypeEnrichmentQueue> EnqueueErpKTypeEnrichmentAsync(ErpKTypeEnrichmentQueue row);
        ValueTask UpdateErpKTypeEnrichmentStatusAsync(string kType, string status, int? productsImported);
    }
}
