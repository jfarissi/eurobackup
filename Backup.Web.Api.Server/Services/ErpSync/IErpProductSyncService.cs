using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Models;

namespace Backup.Web.Api.Server.Services.ErpSync
{
    public interface IErpProductSyncService
    {
        Task<ErpSyncLog> SyncAllProductsAsync(CancellationToken ct = default);
        Task<ErpProduct?> SyncProductByIdAsync(string erpProductId, CancellationToken ct = default);
        Task<List<ErpProductChangeLog>> GetUnreadChangesAsync(CancellationToken ct = default);
        Task MarkChangesAsReadAsync(List<int> changeLogIds, CancellationToken ct = default);
    }
}
