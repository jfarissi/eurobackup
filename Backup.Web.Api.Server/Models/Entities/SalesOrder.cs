using System;
using System.Collections.Generic;

namespace Backup.Web.Api.Server.Models.Entities
{
using Backup.Web.Api.Server.Services.Tenancy;

    public class SalesOrder : IHasCompanyId
    {
        public int Id { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public int CustomerId { get; set; }
        public Customer? Customer { get; set; }
        public int? QuoteId { get; set; }
        public DateTime Date { get; set; } = DateTime.UtcNow;
        public string Status { get; set; } = "Draft"; // Draft, Confirmed, Delivered, Invoiced, Cancelled
        public decimal TotalHT { get; set; }
        public decimal TotalVat { get; set; }
        public decimal TotalTTC { get; set; }
        public string? Notes { get; set; }
        public string? CompanyId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public List<SalesOrderLine> Lines { get; set; } = new();
    }

    public class SalesOrderLine
    {
        public int Id { get; set; }
        public int SalesOrderId { get; set; }
        public SalesOrder? SalesOrder { get; set; }
        public string ProductKey { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal DeliveredQuantity { get; set; } = 0m;
        public decimal UnitPrice { get; set; }
        public decimal VatRate { get; set; } = 21.0m;
        public decimal TotalHT { get; set; }
        public decimal TotalTTC { get; set; }
        public int LineNumber { get; set; }
    }
}
