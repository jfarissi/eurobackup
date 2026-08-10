using System;

namespace Backup.Web.Api.Server.Models.Catalog
{
    /// <summary>Compatibilité véhicule d'une pièce (marque / modèle / années + specs moteur).</summary>
    public class ErpProductVehicle
    {
        public Guid Id { get; set; }
        public int ProductId { get; set; }
        public string Make { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        /// <summary>Libellé type / variante TecDoc (ex. typeName).</summary>
        public string? TypeName { get; set; }
        public int? YearFrom { get; set; }
        public int? YearTo { get; set; }
        public string? EngineCode { get; set; }
        /// <summary>vehicleId TecDoc / KType.</summary>
        public string? KType { get; set; }
        public string? ExternalManufacturerId { get; set; }
        public string? ExternalModelId { get; set; }
        public string? BodyType { get; set; }
        public string? FuelType { get; set; }
        public string? DriveType { get; set; }
        public string? Transmission { get; set; }
        public int? PowerKW { get; set; }
        public int? PowerHP { get; set; }
        public int? Ccm { get; set; }
        public int? Cylinders { get; set; }
        public int? Valves { get; set; }
        /// <summary>Payload véhicule brut RapidAPI/TecDoc (ne rien perdre).</summary>
        public string? RawJson { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ErpProduct? Product { get; set; }
    }
}
