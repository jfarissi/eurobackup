using System.Collections.Generic;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Models.Entities;

namespace Backup.Web.Api.Server.Services.Dropship
{
    public interface IDropshipPurchaseOrderService
    {
        /// <summary>
        /// Crée des CDF Draft (1 par fournisseur) pour les lignes dropship d'une commande Confirmée.
        /// Idempotent : si un CDF non annulé est déjà lié, le réutilise.
        /// </summary>
        Task<DropshipEnsureResult> EnsureForConfirmedOrderAsync(SalesOrder order);

        Task<IReadOnlyList<PurchaseOrder>> ListForSalesOrderAsync(int salesOrderId, string? companyId);
    }

    public sealed class DropshipEnsureResult
    {
        public IReadOnlyList<PurchaseOrder> PurchaseOrders { get; init; } = System.Array.Empty<PurchaseOrder>();
        public IReadOnlyList<string> Notes { get; init; } = System.Array.Empty<string>();
    }

    public sealed class DropshipPurchaseOrderDto
    {
        public int Id { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public int SupplierId { get; set; }
        public string? SupplierName { get; set; }
        public string Status { get; set; } = string.Empty;
        public decimal TotalTTC { get; set; }
        public int? SalesOrderId { get; set; }

        public static DropshipPurchaseOrderDto From(PurchaseOrder po) => new()
        {
            Id = po.Id,
            OrderNumber = po.OrderNumber,
            SupplierId = po.SupplierId,
            SupplierName = po.Supplier?.Name,
            Status = po.Status,
            TotalTTC = po.TotalTTC,
            SalesOrderId = po.SalesOrderId
        };
    }
}
