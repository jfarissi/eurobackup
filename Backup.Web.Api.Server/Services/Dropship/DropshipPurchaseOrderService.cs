using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Models;
using Backup.Web.Api.Server.Models.Entities;
using Backup.Web.Api.Server.Services.Numbering;
using Backup.Web.Api.Server.Services.Sales;
using Backup.Web.Api.Server.Services.Stock;
using Backup.Web.Api.Server.Services.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Backup.Web.Api.Server.Services.Dropship
{
    /// <summary>F8 tranche 1 : CDF auto à la confirmation d'une commande vente (Demo / flag produit).</summary>
    public sealed class DropshipPurchaseOrderService : IDropshipPurchaseOrderService
    {
        private readonly IStorageBroker storage;
        private readonly INumberingSequenceService numbering;
        private readonly ILogger<DropshipPurchaseOrderService> logger;

        public DropshipPurchaseOrderService(
            IStorageBroker storage,
            INumberingSequenceService numbering,
            ILogger<DropshipPurchaseOrderService> logger)
        {
            this.storage = storage;
            this.numbering = numbering;
            this.logger = logger;
        }

        public async Task<IReadOnlyList<PurchaseOrder>> ListForSalesOrderAsync(int salesOrderId, string? companyId)
        {
            var rows = await this.storage.SelectAllPurchaseOrders()
                .ForCompany(companyId)
                .Where(p => p.SalesOrderId == salesOrderId)
                .OrderBy(p => p.Id)
                .ToListAsync();

            return rows
                .Where(p => !string.Equals(p.Status, "Cancelled", StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        public async Task<DropshipEnsureResult> EnsureForConfirmedOrderAsync(SalesOrder order)
        {
            if (order.Id <= 0
                || !string.Equals(order.Status, "Confirmed", StringComparison.OrdinalIgnoreCase))
            {
                return new DropshipEnsureResult();
            }

            var existing = await this.ListForSalesOrderAsync(order.Id, order.CompanyId);
            if (existing.Count > 0)
            {
                return new DropshipEnsureResult { PurchaseOrders = existing };
            }

            var dropshipProducts = await this.storage.SelectAllErpProducts()
                .Where(p => p.IsDropship)
                .ToListAsync();

            if (dropshipProducts.Count == 0)
                return new DropshipEnsureResult();

            var notes = new List<string>();
            var grouped = new Dictionary<int, List<(SalesOrderLine Line, ErpProduct Product)>>();

            foreach (var line in order.Lines ?? new List<SalesOrderLine>())
            {
                if (string.IsNullOrWhiteSpace(line.ProductKey) || line.Quantity <= 0.0001m)
                    continue;

                var product = dropshipProducts.FirstOrDefault(p => MatchesLine(line, p));
                if (product == null) continue;

                var supplierId = product.DropshipSupplierId ?? line.SupplierId;
                if (supplierId is not > 0)
                {
                    notes.Add($"Dropship : pas de fournisseur pour {line.ProductKey}.");
                    continue;
                }

                var supplier = await this.storage.SelectSupplierByIdAsync(supplierId.Value);
                if (supplier == null
                    || !supplier.BelongsToCompany(order.CompanyId)
                    || !supplier.IsActive)
                {
                    notes.Add($"Dropship : fournisseur #{supplierId} invalide pour {line.ProductKey}.");
                    continue;
                }

                if (!grouped.TryGetValue(supplier.Id, out var bucket))
                {
                    bucket = new List<(SalesOrderLine, ErpProduct)>();
                    grouped[supplier.Id] = bucket;
                }
                bucket.Add((line, product));
            }

            if (grouped.Count == 0)
            {
                return new DropshipEnsureResult { Notes = notes };
            }

            var currency = await SalesBusinessRules.ResolveCompanyCurrencyAsync(this.storage, order.CompanyId);
            var created = new List<PurchaseOrder>();

            foreach (var (supplierId, items) in grouped)
            {
                var supplier = await this.storage.SelectSupplierByIdAsync(supplierId);
                var po = new PurchaseOrder
                {
                    OrderNumber = await this.numbering.GetNextNumberAsync("PurchaseOrder", order.CompanyId),
                    SupplierId = supplierId,
                    SalesOrderId = order.Id,
                    Date = DateTime.UtcNow,
                    ExpectedDeliveryDate = supplier is { LeadTimeDays: > 0 }
                        ? DateTime.UtcNow.Date.AddDays(supplier.LeadTimeDays)
                        : null,
                    Status = "Draft",
                    Notes = $"Dropship SO {order.OrderNumber}",
                    CompanyId = order.CompanyId,
                    CurrencyCode = currency,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    CreatedBy = "dropship",
                    UpdatedBy = "dropship",
                    Lines = new List<PurchaseOrderLine>()
                };

                var lineNumber = 1;
                foreach (var (line, product) in items)
                {
                    var unitPrice = product.CPrice ?? 0m;
                    var vatRate = product.TypeVatPerc ?? (line.VatRate > 0 ? line.VatRate : 21m);
                    po.Lines.Add(new PurchaseOrderLine
                    {
                        ProductKey = line.ProductKey,
                        Description = string.IsNullOrWhiteSpace(line.Description)
                            ? (product.Name ?? line.ProductKey)
                            : line.Description,
                        Quantity = line.Quantity,
                        UnitPrice = unitPrice,
                        VatRate = vatRate,
                        LineNumber = lineNumber++,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                        CreatedBy = "dropship"
                    });
                }

                SalesBusinessRules.RecalculatePurchaseOrderTotals(po);
                var inserted = await this.storage.InsertPurchaseOrderAsync(po);
                created.Add(inserted);
                this.logger.LogInformation(
                    "Dropship CDF {PoNumber} created for sales order {SoNumber} (supplier {SupplierId})",
                    inserted.OrderNumber, order.OrderNumber, supplierId);
            }

            if (created.Count > 0)
            {
                notes.Add($"Dropship CDF {string.Join(", ", created.Select(p => p.OrderNumber))}");
            }

            return new DropshipEnsureResult { PurchaseOrders = created, Notes = notes };
        }

        private static bool MatchesLine(SalesOrderLine line, ErpProduct product) =>
            StockLedger.ProductKeysMatch(line.ProductKey, product.Reference)
            || StockLedger.ProductKeysMatch(line.ProductKey, product.ErpProductId)
            || StockLedger.ProductKeysMatch(line.ProductKey, product.Ean);
    }
}
