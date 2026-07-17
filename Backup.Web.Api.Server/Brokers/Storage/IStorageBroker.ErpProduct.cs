using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Models;

namespace Backup.Web.Api.Server.Brokers.Storage
{
    public partial interface IStorageBroker
    {
        ValueTask<ErpProduct> InsertErpProductAsync(ErpProduct product);
        ValueTask<ErpProduct> StageInsertErpProductAsync(ErpProduct product);
        void StageUpdateErpProduct(ErpProduct product);
        Task FlushChangesAsync(CancellationToken cancellationToken = default);
        IQueryable<ErpProduct> SelectAllErpProducts();
        ValueTask<ErpProduct?> SelectErpProductByIdAsync(int id);
        ValueTask<ErpProduct?> SelectErpProductByErpIdAsync(string erpProductId);
        ValueTask<ErpProduct> UpdateErpProductAsync(ErpProduct product);

        ValueTask<ErpProductChangeLog> InsertErpProductChangeLogAsync(ErpProductChangeLog changeLog);
        ValueTask InsertErpProductChangeLogsAsync(IEnumerable<ErpProductChangeLog> changeLogs);
        IQueryable<ErpProductChangeLog> SelectAllErpProductChangeLogs();
        ValueTask MarkErpProductChangeLogsAsReadAsync(IEnumerable<int> changeLogIds);

        ValueTask<ErpSyncLog> InsertErpSyncLogAsync(ErpSyncLog syncLog);
        ValueTask<ErpSyncLog> UpdateErpSyncLogAsync(ErpSyncLog syncLog);
        IQueryable<ErpSyncLog> SelectAllErpSyncLogs();
        ValueTask<ErpSyncLog?> SelectErpSyncLogByJobIdAsync(string jobId);
    }
}
