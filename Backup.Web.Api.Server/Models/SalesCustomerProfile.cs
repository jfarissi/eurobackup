using System;

namespace Backup.Web.Api.Server.Models
{
    public class SalesCustomerProfile
    {
        public int Id { get; set; }
        public string CustomerId { get; set; } = string.Empty;
        public string? PreferredBrandsJson { get; set; }
        public decimal? AverageBudget { get; set; }
        public string? Notes { get; set; }
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
