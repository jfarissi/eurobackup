using System;
using System.Collections.Generic;

namespace Backup.Web.Api.Server.Services.Garage
{
    public sealed class GarageMeDto
    {
        public int CustomerId { get; set; }
        public string CustomerCode { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public decimal Balance { get; set; }
        public decimal CreditLimit { get; set; }
    }

    public class GarageOrderDto
    {
        public int Id { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public string Status { get; set; } = string.Empty;
        public decimal TotalTTC { get; set; }
        public int LineCount { get; set; }
    }

    public sealed class GarageOrderDetailDto : GarageOrderDto
    {
        public decimal TotalHT { get; set; }
        public List<GarageOrderLineDto> Lines { get; set; } = new();
    }

    public sealed class GarageOrderLineDto
    {
        public int LineNumber { get; set; }
        public string ProductKey { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalTTC { get; set; }
    }

    public sealed class GarageVehicleDto
    {
        public Guid Id { get; set; }
        public string PlateNumber { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        public string? Vin { get; set; }
        public string? KType { get; set; }
        public string? Make { get; set; }
        public string? Model { get; set; }
        public int? Year { get; set; }
    }
}
