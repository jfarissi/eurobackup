using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Models.Users;

namespace Backup.Web.Api.Server.Services.Garage
{
    public interface IGaragePortalService
    {
        Task<GarageMeDto> GetMeAsync(User user, string companyId, CancellationToken ct);
        Task<IReadOnlyList<GarageOrderDto>> GetOrdersAsync(User user, string companyId, CancellationToken ct);
        Task<GarageOrderDetailDto> GetOrderAsync(User user, string companyId, int orderId, CancellationToken ct);
        Task<IReadOnlyList<GarageVehicleDto>> GetVehiclesAsync(User user, string companyId, CancellationToken ct);
    }
}
