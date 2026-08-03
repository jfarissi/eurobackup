using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Models.Entities;
using Backup.Web.Api.Server.Services.Numbering;
using Backup.Web.Api.Server.Services.Tenancy;

namespace Backup.Web.Api.Server.Services.Stock
{
    /// <summary>P4 — propose des BA Draft si stock dispo &lt; MinStock après sortie.</summary>
    public static class StockReorderHelper
    {
        public static async Task<List<string>> SuggestDraftPurchaseOrdersAsync(
            IStorageBroker storage,
            INumberingSequenceService numbering,
            string? companyId,
            IEnumerable<string> productKeys)
        {
            var messages = new List<string>();
            var keys = productKeys
                .Where(k => !string.IsNullOrWhiteSpace(k))
                .Select(k => k.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var productKey in keys)
            {
                var item = StockLedger.FindStockItem(storage, companyId, productKey);
                if (item == null || item.MinStock <= 0) continue;

                var available = Math.Max(0m, item.QuantityOnHand - item.ReservedQuantity);
                if (available + 0.0001m >= item.MinStock) continue;

                var qty = item.MinStock - available;
                if (qty <= 0.0001m) continue;

                // Éviter doublon BA Draft ouvert pour ce produit
                var hasOpenPo = storage.SelectAllPurchaseOrders()
                    .ForCompany(companyId)
                    .AsEnumerable()
                    .Where(p => string.Equals(p.Status, "Draft", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(p.Status, "Sent", StringComparison.OrdinalIgnoreCase))
                    .SelectMany(p => p.Lines ?? new List<PurchaseOrderLine>())
                    .Any(l => string.Equals((l.ProductKey ?? "").Trim(), item.ProductKey, StringComparison.OrdinalIgnoreCase));
                if (hasOpenPo)
                {
                    messages.Add($"Réappro : BA déjà ouvert pour {item.ProductKey} (dispo {available:0.####} < min {item.MinStock:0.####}).");
                    continue;
                }

                var supplier = ResolveSupplier(storage, companyId, item.Supplier);
                if (supplier == null)
                {
                    messages.Add($"Réappro suggérée pour {item.ProductKey} (qté {qty:0.####}) : fournisseur non résolu ({item.Supplier ?? "n/a"}).");
                    continue;
                }

                var po = new PurchaseOrder
                {
                    OrderNumber = await numbering.GetNextNumberAsync("PurchaseOrder", companyId),
                    SupplierId = supplier.Id,
                    Date = DateTime.UtcNow,
                    Status = "Draft",
                    Notes = $"Réappro auto — stock {item.ProductKey} sous seuil (dispo {available:0.####} / min {item.MinStock:0.####})",
                    CompanyId = companyId,
                    CreatedAt = DateTime.UtcNow,
                    Lines = new List<PurchaseOrderLine>
                    {
                        new PurchaseOrderLine
                        {
                            ProductKey = item.ProductKey,
                            Description = item.Description ?? item.ProductKey,
                            Quantity = qty,
                            UnitPrice = 0m,
                            VatRate = 21m,
                            TotalHT = 0m,
                            TotalTTC = 0m,
                            LineNumber = 1
                        }
                    }
                };
                po.TotalHT = 0m;
                po.TotalVat = 0m;
                po.TotalTTC = 0m;

                var created = await storage.InsertPurchaseOrderAsync(po);
                messages.Add($"Réappro auto : BA Draft {created.OrderNumber} créé pour {item.ProductKey} (qté {qty:0.####}).");
            }

            return messages;
        }

        private static Supplier? ResolveSupplier(IStorageBroker storage, string? companyId, string? supplierName)
        {
            if (string.IsNullOrWhiteSpace(supplierName)) return null;
            var name = supplierName.Trim();
            return storage.SelectAllSuppliers()
                .ForCompany(companyId)
                .AsEnumerable()
                .FirstOrDefault(s =>
                    string.Equals(s.Name?.Trim(), name, StringComparison.OrdinalIgnoreCase)
                    || (!string.IsNullOrWhiteSpace(s.Name) && s.Name.Contains(name, StringComparison.OrdinalIgnoreCase)));
        }
    }
}
