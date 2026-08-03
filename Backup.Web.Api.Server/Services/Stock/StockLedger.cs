using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Models;
using Backup.Web.Api.Server.Models.Entities;
using Backup.Web.Api.Server.Services.Documents.Parsing;
using Backup.Web.Api.Server.Services.Tenancy;

namespace Backup.Web.Api.Server.Services.Stock
{
    /// <summary>Applique un mouvement et met à jour QuantityOnHand / réservations.</summary>
    public static class StockLedger
    {
        /// <summary>RG-FA1–4 lite : clés de ligne "frais de port" traitées comme des lignes de service, sans mouvement de stock.</summary>
        private static readonly HashSet<string> ShippingFeeKeys = new(StringComparer.OrdinalIgnoreCase) { "FDP", "SHIPPING" };

        public static bool IsShippingFeeKey(string? productKey) =>
            !string.IsNullOrWhiteSpace(productKey) && ShippingFeeKeys.Contains(productKey.Trim());

        public static async Task ApplyAsync(
            IStorageBroker storage,
            string? companyId,
            string productKey,
            string movementType,
            decimal quantity,
            string? referenceDocument,
            string? reason,
            string? createdBy)
        {
            if (string.IsNullOrWhiteSpace(productKey) || quantity == 0 || IsShippingFeeKey(productKey)) return;

            var resolvedKey = ResolveStockProductKey(storage, companyId, productKey);
            var allowNegative = await IsNegativeStockAllowedAsync(storage, companyId);

            var movement = new StockMovement
            {
                ProductKey = resolvedKey,
                MovementType = movementType,
                Quantity = Math.Abs(quantity),
                ReferenceDocument = referenceDocument,
                Reason = reason,
                CompanyId = companyId,
                CreatedBy = createdBy ?? "System",
                CreatedAt = DateTime.UtcNow
            };
            await storage.InsertStockMovementAsync(movement);

            decimal delta = movementType switch
            {
                "In" => Math.Abs(quantity),
                "Out" => -Math.Abs(quantity),
                "Adjustment" => quantity,
                "Transfer" => -Math.Abs(quantity),
                _ => quantity
            };

            var existing = FindStockItem(storage, companyId, productKey);

            if (existing != null)
            {
                existing.QuantityOnHand += delta;
                // RG-CS2 : clamp à 0 si stock négatif interdit.
                if (!allowNegative && existing.QuantityOnHand < 0)
                    existing.QuantityOnHand = 0;
                if (existing.ReservedQuantity > existing.QuantityOnHand && !allowNegative)
                    existing.ReservedQuantity = existing.QuantityOnHand;
                existing.LastUpdated = DateTime.UtcNow;
                await storage.UpdateStockAsync(existing);
            }
            else
            {
                var onHand = delta;
                if (!allowNegative && onHand < 0) onHand = 0;
                await storage.InsertStockAsync(new StockItem
                {
                    ProductKey = resolvedKey,
                    QuantityOnHand = onHand,
                    ReservedQuantity = 0,
                    MinStock = 0,
                    Unit = "PCS",
                    LastUpdated = DateTime.UtcNow,
                    CompanyId = companyId
                });
            }
        }

        public static string? ValidateAvailable(
            IStorageBroker storage,
            string? companyId,
            string productKey,
            decimal requiredQty,
            decimal extraAvailableFromOwnReservation = 0m)
        {
            if (string.IsNullOrWhiteSpace(productKey) || requiredQty <= 0 || IsShippingFeeKey(productKey)) return null;

            // RG-CS2 : si stock négatif autorisé, pas de blocage ATP.
            if (IsNegativeStockAllowed(storage, companyId)) return null;

            var available = GetAvailable(storage, companyId, productKey) + Math.Max(0m, extraAvailableFromOwnReservation);
            var displayKey = ResolveStockProductKey(storage, companyId, productKey);

            if (available + 0.0001m < requiredQty)
                return $"Stock insuffisant pour '{displayKey}' (disponible {available:0.####}, requis {requiredQty:0.####}).";
            return null;
        }

        private static bool IsNegativeStockAllowed(IStorageBroker storage, string? companyId)
        {
            if (string.IsNullOrWhiteSpace(companyId)) return false;
            var company = storage.SelectAllCompanies().FirstOrDefault(c => c.Id == companyId);
            return company?.AllowNegativeStock == true;
        }

        private static async Task<bool> IsNegativeStockAllowedAsync(IStorageBroker storage, string? companyId)
        {
            if (string.IsNullOrWhiteSpace(companyId)) return false;
            var company = await storage.SelectCompanyByIdAsync(companyId);
            return company?.AllowNegativeStock == true;
        }

        public static decimal GetOnHand(IStorageBroker storage, string? companyId, string productKey)
        {
            if (string.IsNullOrWhiteSpace(productKey)) return 0m;
            return FindStockItem(storage, companyId, productKey)?.QuantityOnHand ?? 0m;
        }

        public static decimal GetReserved(IStorageBroker storage, string? companyId, string productKey)
        {
            if (string.IsNullOrWhiteSpace(productKey)) return 0m;
            return FindStockItem(storage, companyId, productKey)?.ReservedQuantity ?? 0m;
        }

        /// <summary>ATP = OnHand − Reserved.</summary>
        public static decimal GetAvailable(IStorageBroker storage, string? companyId, string productKey)
        {
            var item = FindStockItem(storage, companyId, productKey);
            if (item == null) return 0m;
            return Math.Max(0m, item.QuantityOnHand - item.ReservedQuantity);
        }

        public static async Task<decimal> ReserveAsync(
            IStorageBroker storage,
            string? companyId,
            string productKey,
            decimal quantity,
            string? reason)
        {
            if (string.IsNullOrWhiteSpace(productKey) || quantity <= 0 || IsShippingFeeKey(productKey)) return 0m;

            var available = GetAvailable(storage, companyId, productKey);
            var toReserve = Math.Min(quantity, available);
            if (toReserve <= 0.0001m) return 0m;

            var item = FindStockItem(storage, companyId, productKey);
            var resolvedKey = ResolveStockProductKey(storage, companyId, productKey);
            if (item == null)
            {
                // Rien à réserver sans stock physique
                return 0m;
            }

            item.ReservedQuantity += toReserve;
            item.LastUpdated = DateTime.UtcNow;
            await storage.UpdateStockAsync(item);

            await storage.InsertStockMovementAsync(new StockMovement
            {
                ProductKey = resolvedKey,
                MovementType = "Adjustment",
                Quantity = toReserve,
                ReferenceDocument = "RESERVE",
                Reason = reason ?? "Réservation commande",
                CompanyId = companyId,
                CreatedBy = "System",
                CreatedAt = DateTime.UtcNow
            });

            return toReserve;
        }

        public static async Task ReleaseAsync(
            IStorageBroker storage,
            string? companyId,
            string productKey,
            decimal quantity,
            string? reason)
        {
            if (string.IsNullOrWhiteSpace(productKey) || quantity <= 0 || IsShippingFeeKey(productKey)) return;

            var item = FindStockItem(storage, companyId, productKey);
            if (item == null) return;

            var toRelease = Math.Min(quantity, item.ReservedQuantity);
            if (toRelease <= 0.0001m) return;

            item.ReservedQuantity -= toRelease;
            if (item.ReservedQuantity < 0) item.ReservedQuantity = 0;
            item.LastUpdated = DateTime.UtcNow;
            await storage.UpdateStockAsync(item);

            await storage.InsertStockMovementAsync(new StockMovement
            {
                ProductKey = item.ProductKey,
                MovementType = "Adjustment",
                Quantity = toRelease,
                ReferenceDocument = "RELEASE",
                Reason = reason ?? "Libération réservation",
                CompanyId = companyId,
                CreatedBy = "System",
                CreatedAt = DateTime.UtcNow
            });
        }

        /// <summary>Réserve le reliquat ouvert d'une commande (Confirm/Approve).</summary>
        public static async Task ReserveOrderAsync(IStorageBroker storage, SalesOrder order)
        {
            var companyId = order.CompanyId;
            foreach (var line in order.Lines ?? new List<SalesOrderLine>())
            {
                var need = Math.Max(0m, line.Quantity - line.DeliveredQuantity - line.ReservedQuantity);
                if (need <= 0.0001m || string.IsNullOrWhiteSpace(line.ProductKey)) continue;

                var reserved = await ReserveAsync(
                    storage,
                    companyId,
                    line.ProductKey,
                    need,
                    $"Réservation {order.OrderNumber}");
                line.ReservedQuantity += reserved;
            }
        }

        /// <summary>Libère toute la réservation restante d'une commande.</summary>
        public static async Task ReleaseOrderAsync(IStorageBroker storage, SalesOrder order, string? reason = null)
        {
            var companyId = order.CompanyId;
            foreach (var line in order.Lines ?? new List<SalesOrderLine>())
            {
                if (line.ReservedQuantity <= 0.0001m || string.IsNullOrWhiteSpace(line.ProductKey)) continue;
                await ReleaseAsync(
                    storage,
                    companyId,
                    line.ProductKey,
                    line.ReservedQuantity,
                    reason ?? $"Libération {order.OrderNumber}");
                line.ReservedQuantity = 0m;
            }
        }

        /// <summary>
        /// ERP refs are often "Brand Code" (e.g. "FF Group 14293") while Stock uses "14293".
        /// </summary>
        public static IEnumerable<string> CandidateKeys(string productKey)
        {
            var key = ProductKeyHelper.Normalize(productKey?.Trim() ?? string.Empty);
            if (string.IsNullOrWhiteSpace(key)) yield break;

            yield return key;

            var parts = key.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                var last = parts[^1];
                if (!string.Equals(last, key, StringComparison.OrdinalIgnoreCase))
                    yield return last;
            }
        }

        public static string ResolveStockProductKey(IStorageBroker storage, string? companyId, string productKey)
        {
            var existing = FindStockItem(storage, companyId, productKey);
            if (existing != null) return existing.ProductKey;

            var candidates = CandidateKeys(productKey).ToList();
            return candidates.Count > 0 ? candidates[^1] : productKey.Trim();
        }

        public static StockItem? FindStockItem(IStorageBroker storage, string? companyId, string productKey)
        {
            var candidates = CandidateKeys(productKey).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (candidates.Count == 0) return null;

            return storage.SelectAllStock()
                .ForCompany(companyId)
                .AsEnumerable()
                .FirstOrDefault(s => s.ProductKey != null && candidates.Contains(s.ProductKey));
        }
    }
}
