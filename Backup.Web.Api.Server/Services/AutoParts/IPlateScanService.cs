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
        string? CategoryName);

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
        bool IsDemoData,
        string? Message,
        List<PlateCompatibleProductDto> CompatibleProducts);

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

    public interface IPlateScanService
    {
        Task<PlateScanResultDto> ScanPlateAsync(string companyId, IFormFile image, string? userId, CancellationToken ct = default);
        Task<PlateScanResultDto> SearchByPlateAsync(string companyId, string plateNumber, string? country, string? userId, CancellationToken ct = default);
        Task<PlateScanResultDto> SearchByVinAsync(string companyId, string vin, string? userId, CancellationToken ct = default);
        Task<List<PlateHistoryDto>> GetHistoryAsync(string companyId, int limit = 20, CancellationToken ct = default);
    }
}
