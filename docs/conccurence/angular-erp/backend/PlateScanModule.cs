// ============================================================
// BACKEND .NET — Module Lecture de Plaque
// À ajouter dans ton projet MyErp.Api
// ============================================================

// ── 1. ENTITÉS ──

namespace MyErp.Models.Entities
{
    /// <summary>
    /// Historique des recherches par plaque d'immatriculation
    /// </summary>
    public class ErpPlateHistory
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string CompanyId { get; set; } = null!;
        public string PlateNumber { get; set; } = null!;
        public string? Country { get; set; }
        public string? Vin { get; set; }
        public string? Make { get; set; }
        public string? Model { get; set; }
        public int? Year { get; set; }
        public string? EngineCode { get; set; }
        public string? FuelType { get; set; }
        public int? PowerHP { get; set; }
        public int ProductsFound { get; set; }
        public string? SearchedBy { get; set; }  // user_id
        public DateTime SearchedAt { get; set; } = DateTime.UtcNow;
    }
}

// ── 2. DTOs ──

namespace MyErp.Models.Dtos
{
    public record PlateScanRequestDto(IFormFile? Image, string? PlateNumber, string? Country, string? Vin);

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
        List<PlateCompatibleProductDto> CompatibleProducts
    );

    public record PlateCompatibleProductDto(
        int Id,
        string ErpProductId,
        string? Name,
        string? Reference,
        string? Brand,
        decimal? PriceHT,
        decimal? StockQuantity,
        string? ImageUrl,
        string? CategoryName
    );

    public record PlateHistoryDto(
        string Id,
        string PlateNumber,
        string? Vin,
        string? Make,
        string? Model,
        int? Year,
        int ProductsFound,
        DateTime SearchedAt
    );
}

// ── 3. SERVICE ──

namespace MyErp.Services.AutoParts
{
    using MyErp.Data;
    using MyErp.Models.Dtos;
    using MyErp.Models.Entities;
    using Microsoft.EntityFrameworkCore;

    public interface IPlateScanService
    {
        Task<PlateScanResultDto> ScanPlateAsync(string companyId, IFormFile image, string? userId);
        Task<PlateScanResultDto> SearchByPlateAsync(string companyId, string plateNumber, string? country, string? userId);
        Task<PlateScanResultDto> SearchByVinAsync(string companyId, string vin, string? userId);
        Task<List<PlateHistoryDto>> GetHistoryAsync(string companyId, int limit = 20);
    }

    public class PlateScanService : IPlateScanService
    {
        private readonly ErpDbContext _db;
        private readonly ILogger<PlateScanService> _logger;
        private readonly IHttpClientFactory _httpFactory;

        // APIs externes (à configurer dans appsettings)
        private const string AFTERIIZE_BASE = "https://api.afteriize.com/v1";
        private const string NHTSA_VIN_URL = "https://vpic.nhtsa.dot.gov/api/vehicles/DecodeVinValuesExtended";

        public PlateScanService(ErpDbContext db, ILogger<PlateScanService> logger, IHttpClientFactory httpFactory)
        {
            _db = db;
            _logger = logger;
            _httpFactory = httpFactory;
        }

        public async Task<PlateScanResultDto> ScanPlateAsync(string companyId, IFormFile image, string? userId)
        {
            _logger.LogInformation("Scan plaque pour {Company}", companyId);

            // Étape 1: OCR de la plaque via API externe (Afteriize ou service custom)
            var plateNumber = await ExtractPlateFromImageAsync(image);
            if (string.IsNullOrEmpty(plateNumber))
                throw new InvalidOperationException("Impossible de lire la plaque sur l'image");

            // Étape 2: Recherche par plaque
            return await SearchByPlateAsync(companyId, plateNumber, null, userId);
        }

        public async Task<PlateScanResultDto> SearchByPlateAsync(string companyId, string plateNumber, string? country, string? userId)
        {
            // Étape 1: Décode la plaque → VIN + véhicule
            var vehicleInfo = await DecodePlateAsync(plateNumber, country);

            // Étape 2: Cherche les pièces compatibles
            var products = await FindCompatibleProductsAsync(vehicleInfo);

            // Étape 3: Sauvegarde dans l'historique
            await SaveHistoryAsync(companyId, plateNumber, vehicleInfo, products.Count, userId);

            return MapToResult(plateNumber, country, vehicleInfo, products);
        }

        public async Task<PlateScanResultDto> SearchByVinAsync(string companyId, string vin, string? userId)
        {
            // Étape 1: Décode VIN
            var vehicleInfo = await DecodeVinAsync(vin);

            // Étape 2: Cherche les pièces compatibles
            var products = await FindCompatibleProductsAsync(vehicleInfo);

            // Étape 3: Sauvegarde
            await SaveHistoryAsync(companyId, vin, vehicleInfo, products.Count, userId);

            return MapToResult(vin, null, vehicleInfo, products);
        }

        public async Task<List<PlateHistoryDto>> GetHistoryAsync(string companyId, int limit = 20)
        {
            return await _db.Set<ErpPlateHistory>()
                .Where(h => h.CompanyId == companyId)
                .OrderByDescending(h => h.SearchedAt)
                .Take(limit)
                .Select(h => new PlateHistoryDto(
                    h.Id.ToString(),
                    h.PlateNumber,
                    h.Vin,
                    h.Make,
                    h.Model,
                    h.Year,
                    h.ProductsFound,
                    h.SearchedAt
                ))
                .ToListAsync();
        }

        // ── Méthodes privées ──

        private async Task<string?> ExtractPlateFromImageAsync(IFormFile image)
        {
            // Option A: API Afteriize (recommandé, Europe)
            // Option B: OpenALPR (self-hosted ou cloud)
            // Option C: Azure Computer Vision / AWS Rekognition

            var client = _httpFactory.CreateClient();
            using var content = new MultipartFormDataContent();
            await using var stream = image.OpenReadStream();
            content.Add(new StreamContent(stream), "image", image.FileName);

            // Exemple avec Afteriize (adapter selon l'API réelle)
            // var response = await client.PostAsync($"{AFTERIIZE_BASE}/plate/scan", content);
            // var result = await response.Content.ReadFromJsonAsync<AfteriizePlateResponse>();
            // return result?.PlateNumber;

            // SIMULATION pour le développement
            await Task.Delay(500);
            return "AB-123-CD";  // ← À remplacer par l'appel API réel
        }

        private async Task<VehicleInfo> DecodePlateAsync(string plateNumber, string? country)
        {
            // Option A: Afteriize API (Europe, très complet)
            // Option B: API gouvernementale (ex: SIV en France)
            // Option C: Base de données interne

            var client = _httpFactory.CreateClient();

            // Exemple Afteriize
            // var response = await client.GetAsync($"{AFTERIIZE_BASE}/plate/{plateNumber}?country={country}");
            // var data = await response.Content.ReadFromJsonAsync<AfteriizeVehicleResponse>();

            // SIMULATION
            await Task.Delay(300);
            return new VehicleInfo
            {
                Vin = "VF1BZ0L0632345678",
                Make = "Renault",
                Model = "Clio",
                Year = 2018,
                EngineCode = "K9K",
                FuelType = "Diesel",
                PowerHP = 90
            };
        }

        private async Task<VehicleInfo> DecodeVinAsync(string vin)
        {
            // NHTSA = gratuit, couvre USA + beaucoup de véhicules globaux
            var client = _httpFactory.CreateClient();
            var response = await client.GetAsync($"{NHTSA_VIN_URL}/{vin}?format=json");
            response.EnsureSuccessStatusCode();

            var data = await response.Content.ReadFromJsonAsync<NhtsaVinResponse>();
            var result = data?.Results?.FirstOrDefault();

            if (result == null) throw new InvalidOperationException("VIN non reconnu");

            return new VehicleInfo
            {
                Vin = vin,
                Make = result.Make,
                Model = result.Model,
                Year = int.TryParse(result.ModelYear, out var y) ? y : null,
                EngineCode = result.EngineCode,
                FuelType = result.FuelTypePrimary,
                PowerHP = null  // NHTSA ne fournit pas toujours la puissance
            };
        }

        private async Task<List<PlateCompatibleProductDto>> FindCompatibleProductsAsync(VehicleInfo vehicle)
        {
            // Recherche dans erpproductvehicles + jointure erpproducts
            var query = from v in _db.ProductVehicles
                        join p in _db.Products on v.ProductId equals p.Id
                        where v.Make == vehicle.Make && v.Model == vehicle.Model
                        where !vehicle.Year.HasValue || (v.YearFrom <= vehicle.Year && v.YearTo >= vehicle.Year)
                        where string.IsNullOrEmpty(vehicle.EngineCode) || v.EngineCode == vehicle.EngineCode
                        select new PlateCompatibleProductDto(
                            p.Id,
                            p.ErpProductId,
                            p.Name,
                            p.Reference,
                            p.Brand,
                            p.PriceHT,
                            p.StockQuantity,
                            p.Images!.Where(i => i.IsMain).Select(i => i.Url).FirstOrDefault(),
                            p.TypeName
                        );

            return await query.Take(50).ToListAsync();
        }

        private async Task SaveHistoryAsync(string companyId, string plateNumber, VehicleInfo vehicle, int productsFound, string? userId)
        {
            var history = new ErpPlateHistory
            {
                CompanyId = companyId,
                PlateNumber = plateNumber,
                Vin = vehicle.Vin,
                Make = vehicle.Make,
                Model = vehicle.Model,
                Year = vehicle.Year,
                EngineCode = vehicle.EngineCode,
                FuelType = vehicle.FuelType,
                PowerHP = vehicle.PowerHP,
                ProductsFound = productsFound,
                SearchedBy = userId
            };

            _db.Set<ErpPlateHistory>().Add(history);
            await _db.SaveChangesAsync();
        }

        private static PlateScanResultDto MapToResult(string plateNumber, string? country, VehicleInfo vehicle, List<PlateCompatibleProductDto> products)
        {
            return new PlateScanResultDto(
                plateNumber,
                country,
                vehicle.Vin,
                vehicle.Make,
                vehicle.Model,
                vehicle.Year,
                vehicle.EngineCode,
                vehicle.FuelType,
                vehicle.PowerHP,
                products
            );
        }

        // ── Classes internes ──
        private class VehicleInfo
        {
            public string? Vin { get; set; }
            public string? Make { get; set; }
            public string? Model { get; set; }
            public int? Year { get; set; }
            public string? EngineCode { get; set; }
            public string? FuelType { get; set; }
            public int? PowerHP { get; set; }
        }

        private class NhtsaVinResponse
        {
            public List<NhtsaResult>? Results { get; set; }
        }

        private class NhtsaResult
        {
            public string? Make { get; set; }
            public string? Model { get; set; }
            public string? ModelYear { get; set; }
            public string? EngineCode { get; set; }
            public string? FuelTypePrimary { get; set; }
        }
    }
}

// ── 4. CONTROLLER ──

namespace MyErp.Controllers
{
    using Microsoft.AspNetCore.Mvc;
    using MyErp.Infrastructure.Attributes;
    using MyErp.Services.AutoParts;

    [ApiController]
    [Route("api/autoparts/plate")]
    [RequireModule("auto_parts")]
    public class PlateScanController : ControllerBase
    {
        private readonly IPlateScanService _plateService;
        private readonly ILogger<PlateScanController> _logger;

        public PlateScanController(IPlateScanService plateService, ILogger<PlateScanController> logger)
        {
            _plateService = plateService;
            _logger = logger;
        }

        /// <summary>
        /// Analyse une image de plaque d'immatriculation
        /// </summary>
        [HttpPost("scan")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> ScanPlate(IFormFile image)
        {
            if (image == null || image.Length == 0)
                return BadRequest(new { message = "Image requise" });

            var companyId = GetCompanyId();
            var userId = GetUserId();

            var result = await _plateService.ScanPlateAsync(companyId, image, userId);
            return Ok(result);
        }

        /// <summary>
        /// Recherche par numéro de plaque (texte)
        /// </summary>
        [HttpGet("search")]
        public async Task<IActionResult> SearchByPlate([FromQuery] string plate, [FromQuery] string? country)
        {
            if (string.IsNullOrWhiteSpace(plate))
                return BadRequest(new { message = "Numéro de plaque requis" });

            var companyId = GetCompanyId();
            var userId = GetUserId();

            var result = await _plateService.SearchByPlateAsync(companyId, plate, country, userId);
            return Ok(result);
        }

        /// <summary>
        /// Recherche par VIN
        /// </summary>
        [HttpGet("vin/{vin}")]
        public async Task<IActionResult> SearchByVin(string vin)
        {
            if (string.IsNullOrWhiteSpace(vin) || vin.Length != 17)
                return BadRequest(new { message = "VIN invalide (17 caractères requis)" });

            var companyId = GetCompanyId();
            var userId = GetUserId();

            var result = await _plateService.SearchByVinAsync(companyId, vin, userId);
            return Ok(result);
        }

        /// <summary>
        /// Historique des recherches par plaque
        /// </summary>
        [HttpGet("history")]
        public async Task<IActionResult> GetHistory([FromQuery] int limit = 20)
        {
            var companyId = GetCompanyId();
            var history = await _plateService.GetHistoryAsync(companyId, limit);
            return Ok(history);
        }

        private string GetCompanyId()
        {
            return User.FindFirst("company_id")?.Value
                ?? Request.Headers["X-Company-Id"].FirstOrDefault()
                ?? throw new UnauthorizedAccessException("CompanyId manquant");
        }

        private string? GetUserId()
        {
            return User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        }
    }
}
