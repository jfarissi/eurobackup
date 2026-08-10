using System.Linq;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Models.Catalog;

namespace Backup.Web.Api.Server.Brokers.Storage
{
    public partial interface IStorageBroker
    {
        IQueryable<ErpVinVehicle> SelectAllErpVinVehicles();
        ValueTask<ErpVinVehicle?> SelectErpVinVehicleByVinAsync(string vin);
        ValueTask<ErpVinVehicle> UpsertErpVinVehicleAsync(ErpVinVehicle row);
        ValueTask TouchErpVinVehicleHitAsync(ErpVinVehicle row);
    }
}
