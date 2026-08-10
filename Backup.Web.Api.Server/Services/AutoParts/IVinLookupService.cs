using System.Threading;
using System.Threading.Tasks;

namespace Backup.Web.Api.Server.Services.AutoParts
{
    public record VinVehicleDto(
        string Vin,
        string? Make,
        string? Model,
        int? Year,
        string? EngineCode,
        string? FuelType,
        int? PowerHP,
        string? ExternalVehicleId,
        string? ExternalModelId,
        string? ExternalManufacturerId);

    public record VinLookupResult(
        VinVehicleDto Vehicle,
        string Source,
        bool FromCache,
        bool IsDemo,
        string? Message = null);

    public interface IVinLookupService
    {
        /// <summary>
        /// Résout un VIN : cache local → NHTSA → démo.
        /// RapidAPI uniquement si RapidApi:EnableVinLookup = true (opt-in).
        /// Met à jour le cache pour toute source non-démo.
        /// </summary>
        Task<VinLookupResult> ResolveAsync(string vin, string? companyId = null, CancellationToken ct = default);
    }
}
