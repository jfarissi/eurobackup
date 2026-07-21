using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Models;

namespace Backup.Web.Api.Server.Services.ErpSync
{
    public interface IErpProductSyncService
    {
        Task<ErpSyncLog> SyncAllProductsAsync(CancellationToken ct = default);
        Task<ErpSyncLog> SyncAllProductsAsync(string? modeOverride, CancellationToken ct = default);
        /// <summary>Démarre le sync en arrière-plan et retourne immédiatement le job (Status=Running).</summary>
        Task<ErpSyncLog> StartSyncAllAsync(CancellationToken ct = default);
        Task<ErpSyncLog> SyncCatalogAsync(ErpCatalogSyncFilter filter, CancellationToken ct = default);
        Task<ErpSyncLog> StartSyncCatalogAsync(ErpCatalogSyncFilter filter, CancellationToken ct = default);
        Task<ErpSyncLog?> GetSyncLogByJobIdAsync(string jobId, CancellationToken ct = default);
        Task<ErpProduct?> SyncProductByIdAsync(string erpProductId, CancellationToken ct = default);
        Task<ErpProduct?> SyncLocalProductByIdAsync(int localId, CancellationToken ct = default);
        Task<List<ErpProductChangeLog>> GetUnreadChangesAsync(CancellationToken ct = default);
        Task MarkChangesAsReadAsync(List<int> changeLogIds, CancellationToken ct = default);
        Task<int> DeleteChangesAsync(List<int> changeLogIds, CancellationToken ct = default);
        Task<int> CleanupFormattingFalsePositivesAsync(CancellationToken ct = default);
    }
}
