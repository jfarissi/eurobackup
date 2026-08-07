using System;

namespace Backup.Web.Api.Server.Models.Catalog
{
    /// <summary>Compatibilité véhicule d'une pièce (marque / modèle / années).</summary>
    public class ErpProductVehicle
    {
        public Guid Id { get; set; }
        public int ProductId { get; set; }
        public string Make { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public int? YearFrom { get; set; }
        public int? YearTo { get; set; }
        public string? EngineCode { get; set; }
        public string? KType { get; set; }
        public string? BodyType { get; set; }
        public string? FuelType { get; set; }
        public int? PowerKW { get; set; }
        public int? PowerHP { get; set; }
        public int? Ccm { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ErpProduct? Product { get; set; }
    }
}
