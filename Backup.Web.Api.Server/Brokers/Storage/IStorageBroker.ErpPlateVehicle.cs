using System;
using System.Linq;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Models.Catalog;

namespace Backup.Web.Api.Server.Brokers.Storage
{
    public partial interface IStorageBroker
    {
        IQueryable<ErpPlateVehicle> SelectAllErpPlateVehicles();
        ValueTask<ErpPlateVehicle?> SelectErpPlateVehicleAsync(string companyId, string plateNumber, string country);
        ValueTask<ErpPlateVehicle> UpsertErpPlateVehicleAsync(ErpPlateVehicle row);
        ValueTask TouchErpPlateVehicleHitAsync(ErpPlateVehicle row);
    }
}
