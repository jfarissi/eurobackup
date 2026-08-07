using System.Threading;
using System.Threading.Tasks;

namespace Backup.Web.Api.Server.Services.ErpSync
{
    public interface ICarApiImportService
    {
        /// <summary>
        /// Importe le catalogue pièces auto (lifeofcapo/car-api) dans ErpProducts + variantes.
        /// </summary>
        Task<CarApiImportResult> ImportAsync(
            string? dataPath = null,
            bool importParts = true,
            bool importVehicleBrands = false,
            bool applyFrenchNames = true,
            bool ensureVehicleAttribute = true,
            string? companyId = null,
            string? userName = null,
            CancellationToken ct = default);
    }
}
