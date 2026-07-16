using System.Threading;
using System.Threading.Tasks;

namespace Backup.Web.Api.Server.Services.ErpSync
{
    public interface IErpExcelImportService
    {
        Task<ExcelImportResult> ImportFromDirectoryAsync(string? directoryPath = null, CancellationToken ct = default);
    }
}
