using System;
using Backup.Web.Api.Server.Services.Tenancy;

namespace Backup.Web.Api.Server.Models.Catalog
{
    /// <summary>
    /// Cache local VIN → véhicule (évite de rappeler RapidAPI / NHTSA).
    /// Une pièce n'a pas de VIN ; le VIN identifie un véhicule pour la recherche fitment.
    /// </summary>
    public class ErpVinVehicle : IHasCompanyId
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        /// <summary>Null = cache partagé société / tenant (lookup global catalogue).</summary>
        public string? CompanyId { get; set; }
        public string Vin { get; set; } = string.Empty;
        public string? Make { get; set; }
        public string? Model { get; set; }
        public int? Year { get; set; }
        public string? EngineCode { get; set; }
        public string? FuelType { get; set; }
        public int? PowerHP { get; set; }
        /// <summary>ID véhicule fournisseur (TecDoc / RapidAPI vehicleId).</summary>
        public string? ExternalVehicleId { get; set; }
        public string? ExternalModelId { get; set; }
        public string? ExternalManufacturerId { get; set; }
        /// <summary>RapidApi | Nhtsa | Demo | Manual</summary>
        public string Source { get; set; } = "RapidApi";
        public string? RawJson { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public int HitCount { get; set; }
        public DateTime? LastHitAt { get; set; }
    }
}
