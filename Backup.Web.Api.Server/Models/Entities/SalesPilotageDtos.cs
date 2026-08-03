using System.Collections.Generic;

namespace Backup.Web.Api.Server.Models.Entities
{
    public class SalesPilotageDto
    {
        public int PendingCount { get; set; }
        public int BackorderLineCount { get; set; }
        public int StockoutLineCount { get; set; }
        public List<SalesOrder> PendingOrders { get; set; } = new();
        public List<SalesBackorderLineDto> BackorderLines { get; set; } = new();
        public List<SalesBackorderLineDto> StockoutLines { get; set; } = new();
    }

    public class SalesBackorderLineDto
    {
        public int OrderId { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public string OrderStatus { get; set; } = string.Empty;
        public int CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string ProductKey { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal OrderedQuantity { get; set; }
        public decimal DeliveredQuantity { get; set; }
        public decimal RemainingQuantity { get; set; }
        public decimal StockOnHand { get; set; }
        public bool IsStockout { get; set; }
    }
}
