using System;
using Backup.Web.Api.Server.Services.Tenancy;

namespace Backup.Web.Api.Server.Models.Catalog
{
    /// <summary>Historique des recherches plaque / VIN (module auto_parts).</summary>
    public class ErpPlateHistory : IHasCompanyId
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string? CompanyId { get; set; }
        public string PlateNumber { get; set; } = string.Empty;
        public string? Country { get; set; }
        public string? Vin { get; set; }
        public string? Make { get; set; }
        public string? Model { get; set; }
        public int? Year { get; set; }
        public string? EngineCode { get; set; }
        public string? FuelType { get; set; }
        public int? PowerHP { get; set; }
        public int ProductsFound { get; set; }
        public string? SearchedBy { get; set; }
        public DateTime SearchedAt { get; set; } = DateTime.UtcNow;
    }
}
