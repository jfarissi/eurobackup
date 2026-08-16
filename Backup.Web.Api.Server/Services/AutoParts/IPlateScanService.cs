using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Backup.Web.Api.Server.Services.AutoParts
{
    public record PlateCompatibleProductDto(
        int Id,
        string ErpProductId,
        string? Name,
        string? Reference,
        string? Brand,
        decimal? PriceHT,
        decimal? StockQuantity,
        string? ImageUrl,
        string? CategoryName,
        string? VehicleMake = null,
        string? VehicleModel = null,
        string? VehicleTypeName = null,
        int? YearFrom = null,
        int? YearTo = null,
        string? EngineCode = null,
        string? KType = null,
        string? FuelType = null,
        int? OemCount = null);

    public record PlateScanResultDto(
        string PlateNumber,
        string? Country,
        string? Vin,
        string? Make,
        string? Model,
        int? Year,
        string? EngineCode,
        string? FuelType,
        int? PowerHP,
        string? KType,
        bool IsDemoData,
        bool FromRegistry,
        bool NeedsVehicleLink,
        string? OcrProvider,
        double? OcrScore,
        string? Message,
        /// <summary>KType | MakeModel | None</summary>
        string? ProductMatchMode,
        bool KTypeInCatalog,
        bool KTypeEnrichmentQueued,
        List<PlateCompatibleProductDto> CompatibleProducts,
        bool KTypeSyncInProgress = false,
        bool NeedsCategorySelection = false);

    public record PlateHistoryDto(
        string Id,
        string PlateNumber,
        string? Country,
        string? Vin,
        string? Make,
        string? Model,
        int? Year,
        int ProductsFound,
        DateTime SearchedAt);

    public record LinkPlateVinRequest(
        string Plate,
        string? Country,
        string Vin);

    public interface IPlateScanService
    {
        Task<PlateScanResultDto> ScanPlateAsync(string companyId, IFormFile image, string? userId, CancellationToken ct = default);
        Task<PlateScanResultDto> SearchByPlateAsync(string companyId, string plateNumber, string? country, string? userId, CancellationToken ct = default);
        Task<PlateScanResultDto> SearchByVinAsync(string companyId, string vin, string? userId, CancellationToken ct = default);
        /// <summary>Scénario B : associer un VIN à une plaque inconnue (enregistre le registre local).</summary>
        Task<PlateScanResultDto> LinkPlateToVinAsync(string companyId, LinkPlateVinRequest request, string? userId, CancellationToken ct = default);
        Task<List<PlateHistoryDto>> GetHistoryAsync(string companyId, int limit = 20, CancellationToken ct = default);
    }
}
