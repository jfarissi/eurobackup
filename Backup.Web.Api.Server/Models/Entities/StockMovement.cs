using System;

namespace Backup.Web.Api.Server.Models.Entities
{
using Backup.Web.Api.Server.Services.Audit;
using Backup.Web.Api.Server.Services.Tenancy;

    public class StockMovement : IHasCompanyId, IHasAuditTrail
    {
        public int Id { get; set; }
        public string ProductKey { get; set; } = string.Empty;
        public string MovementType { get; set; } = "In"; // In, Out, Adjustment, Transfer
        public decimal Quantity { get; set; }
        /// <summary>Coût unitaire appliqué (entrée = prix d'achat ; sortie = CMUP au moment du mouvement).</summary>
        public decimal? UnitCost { get; set; }
        /// <summary>|Quantity| × UnitCost — valorisation du mouvement.</summary>
        public decimal? StockValue { get; set; }
        public string? Reason { get; set; }
        public string? ReferenceDocument { get; set; }
        public string? CompanyId { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public string? UpdatedBy { get; set; }
    }
}
