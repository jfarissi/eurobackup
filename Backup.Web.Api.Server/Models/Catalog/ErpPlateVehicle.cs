using System;
using Backup.Web.Api.Server.Services.Tenancy;

namespace Backup.Web.Api.Server.Models.Catalog
{
    /// <summary>
    /// Registre local plaque → véhicule (passerelle métier Maroc).
    /// Scénario A : plaque déjà connue → K-Type / Make-Model instantané.
    /// Scénario B : première visite → association VIN (une fois) puis réutilisation.
    /// </summary>
    public class ErpPlateVehicle : IHasCompanyId
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string? CompanyId { get; set; }
        /// <summary>Garage / client propriétaire (portail F5). Null = plaque non rattachée.</summary>
        public int? CustomerId { get; set; }
        public string PlateNumber { get; set; } = string.Empty;
        public string Country { get; set; } = "MA";
        public string? Vin { get; set; }
        /// <summary>K-Type / TecDoc type id quand disponible.</summary>
        public string? KType { get; set; }
        public string? Make { get; set; }
        public string? Model { get; set; }
        public int? Year { get; set; }
        public string? EngineCode { get; set; }
        public string? FuelType { get; set; }
        public int? PowerHP { get; set; }
        /// <summary>VinLink | PlateProvider | Manual</summary>
        public string Source { get; set; } = "VinLink";
        public int HitCount { get; set; }
        public DateTime? LastHitAt { get; set; }
        public string? CreatedBy { get; set; }
        public string? UpdatedBy { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
