using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Models.Catalog;

namespace Backup.Web.Api.Server.Services.ErpSync
{
    public class CarApiVehicleBrand
    {
        public string Brand { get; set; } = string.Empty;
        public int ModelCount { get; set; }
    }

    public class CarApiVehicleModel
    {
        public string Name { get; set; } = string.Empty;
        public int GenerationCount { get; set; }
    }

    public class CarApiVehicleGeneration
    {
        public string Name { get; set; } = string.Empty;
        public int? YearFrom { get; set; }
        public int? YearTo { get; set; }
    }

    public interface ICarApiCatalogService
    {
        Task<IReadOnlyList<CarApiVehicleBrand>> GetBrandsAsync(CancellationToken ct = default);

        Task<IReadOnlyList<CarApiVehicleModel>> GetModelsAsync(string brand, CancellationToken ct = default);

        Task<IReadOnlyList<CarApiVehicleGeneration>> GetGenerationsAsync(
            string brand,
            string model,
            CancellationToken ct = default);

        /// <summary>Crée l'attribut <c>vehicle_compat</c> pour la société courante si absent.</summary>
        Task<ErpProductAttributeDefinition> EnsureVehicleCompatAttributeAsync(
            string companyId,
            string? userName,
            CancellationToken ct = default);
    }
}
