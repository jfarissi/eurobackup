using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Models.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Backup.Web.Api.Server.Services.ErpSync
{
    public class CarApiCatalogService : ICarApiCatalogService
    {
        public const string VehicleCompatAttributeCode = "vehicle_compat";
        public const string VehicleCompatAttributeName = "Compatibilité véhicule";

        private readonly ErpSyncOptions _options;
        private readonly IHostEnvironment _environment;
        private readonly ILogger<CarApiCatalogService> _logger;
        private readonly Backup.Web.Api.Server.Brokers.Storage.IStorageBroker _storage;

        private readonly SemaphoreSlim _loadLock = new(1, 1);
        private List<CarApiBrandFileDto>? _brandsCache;

        public CarApiCatalogService(
            Backup.Web.Api.Server.Brokers.Storage.IStorageBroker storage,
            IOptions<ErpSyncOptions> options,
            IHostEnvironment environment,
            ILogger<CarApiCatalogService> logger)
        {
            _storage = storage;
            _options = options.Value ?? new ErpSyncOptions();
            _environment = environment;
            _logger = logger;
        }

        public async Task<IReadOnlyList<CarApiVehicleBrand>> GetBrandsAsync(CancellationToken ct = default)
        {
            var brands = await LoadBrandsAsync(ct);
            return brands
                .Select(b => new CarApiVehicleBrand
                {
                    Brand = b.Brand ?? string.Empty,
                    ModelCount = b.Models?.Count ?? 0
                })
                .OrderBy(b => b.Brand, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public async Task<IReadOnlyList<CarApiVehicleModel>> GetModelsAsync(string brand, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(brand))
                return Array.Empty<CarApiVehicleModel>();

            var brands = await LoadBrandsAsync(ct);
            var match = brands.FirstOrDefault(b =>
                string.Equals(b.Brand, brand.Trim(), StringComparison.OrdinalIgnoreCase));

            return (match?.Models ?? new List<CarApiModelFileDto>())
                .Select(m => new CarApiVehicleModel
                {
                    Name = m.Name ?? string.Empty,
                    GenerationCount = m.Generations?.Count ?? 0
                })
                .OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public async Task<IReadOnlyList<CarApiVehicleGeneration>> GetGenerationsAsync(
            string brand,
            string model,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(brand) || string.IsNullOrWhiteSpace(model))
                return Array.Empty<CarApiVehicleGeneration>();

            var brands = await LoadBrandsAsync(ct);
            var brandMatch = brands.FirstOrDefault(b =>
                string.Equals(b.Brand, brand.Trim(), StringComparison.OrdinalIgnoreCase));
            var modelMatch = brandMatch?.Models?.FirstOrDefault(m =>
                string.Equals(m.Name, model.Trim(), StringComparison.OrdinalIgnoreCase));

            return (modelMatch?.Generations ?? new List<CarApiGenerationFileDto>())
                .Select(g => new CarApiVehicleGeneration
                {
                    Name = g.Name ?? string.Empty,
                    YearFrom = g.YearFrom,
                    YearTo = g.YearTo
                })
                .OrderBy(g => g.YearFrom ?? 0)
                .ToList();
        }

        public async Task<ErpProductAttributeDefinition> EnsureVehicleCompatAttributeAsync(
            string companyId,
            string? userName,
            CancellationToken ct = default)
        {
            var existing = await _storage.SelectAllErpProductAttributeDefinitions()
                .FirstOrDefaultAsync(d =>
                    d.CompanyId == companyId
                    && d.Code == VehicleCompatAttributeCode, ct);

            if (existing != null)
                return existing;

            var created = new ErpProductAttributeDefinition
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                Code = VehicleCompatAttributeCode,
                Name = VehicleCompatAttributeName,
                IsActive = true,
                CreatedBy = userName,
                CreatedAt = DateTime.UtcNow
            };

            return await _storage.InsertErpProductAttributeDefinitionAsync(created);
        }

        private async Task<List<CarApiBrandFileDto>> LoadBrandsAsync(CancellationToken ct)
        {
            if (_brandsCache != null)
                return _brandsCache;

            await _loadLock.WaitAsync(ct);
            try
            {
                if (_brandsCache != null)
                    return _brandsCache;

                var path = ResolveBrandsFilePath();
                if (!File.Exists(path))
                {
                    _logger.LogWarning("car-brands.json introuvable: {Path}", path);
                    _brandsCache = new List<CarApiBrandFileDto>();
                    return _brandsCache;
                }

                await using var stream = File.OpenRead(path);
                _brandsCache = await JsonSerializer.DeserializeAsync<List<CarApiBrandFileDto>>(stream, JsonOptions, ct)
                               ?? new List<CarApiBrandFileDto>();
                return _brandsCache;
            }
            finally
            {
                _loadLock.Release();
            }
        }

        private string ResolveBrandsFilePath()
        {
            var basePath = !string.IsNullOrWhiteSpace(_options.CarApiDataPath)
                ? _options.CarApiDataPath.Trim()
                : Path.Combine(_environment.ContentRootPath, "Data", "CarApi");
            return Path.Combine(basePath, "car-brands.json");
        }

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private sealed class CarApiBrandFileDto
        {
            public string? Brand { get; set; }
            public List<CarApiModelFileDto>? Models { get; set; }
        }

        private sealed class CarApiModelFileDto
        {
            public string? Name { get; set; }
            public List<CarApiGenerationFileDto>? Generations { get; set; }
        }

        private sealed class CarApiGenerationFileDto
        {
            public string? Name { get; set; }
            public int? YearFrom { get; set; }
            public int? YearTo { get; set; }
        }
    }
}
