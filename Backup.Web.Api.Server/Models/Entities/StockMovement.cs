using System;

namespace Backup.Web.Api.Server.Models.Entities
{
using Backup.Web.Api.Server.Services.Tenancy;

    public class StockMovement : IHasCompanyId
    {
        public int Id { get; set; }
        public string ProductKey { get; set; } = string.Empty;
        public string MovementType { get; set; } = "In"; // In, Out, Adjustment, Transfer
        public decimal Quantity { get; set; }
        public string? Reason { get; set; }
        public string? ReferenceDocument { get; set; }
        public string? CompanyId { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
