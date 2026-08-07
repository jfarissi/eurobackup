using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Models;
using Backup.Web.Api.Server.Models.Entities;
using Backup.Web.Api.Server.Services.Documents.Parsing;
using Backup.Web.Api.Server.Services.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Backup.Web.Api.Server.Services.Stock
{
    /// <summary>Applique un mouvement et met à jour QuantityOnHand / réservations.</summary>
    public static class StockLedger
    {
        /// <summary>RG-FA1–4 lite : clés de ligne "frais de port" traitées comme des lignes de service, sans mouvement de stock.</summary>
        private static readonly HashSet<string> ShippingFeeKeys = new(StringComparer.OrdinalIgnoreCase) { "FDP", "SHIPPING" };

        public static bool IsShippingFeeKey(string? productKey) =>
            !string.IsNullOrWhiteSpace(productKey) && ShippingFeeKeys.Contains(productKey.Trim());

        public static async Task<StockMovement?> ApplyAsync(
            IStorageBroker storage,
            string? companyId,
            string productKey,
            string movementType,
            decimal quantity,
            string? referenceDocument,
            string? reason,
            string? createdBy,
            decimal? unitCost = null)
        {
            if (string.IsNullOrWhiteSpace(productKey) || quantity == 0 || IsShippingFeeKey(productKey)) return null;

            var resolvedKey = ResolveStockProductKey(storage, companyId, productKey);
            var allowNegative = await IsNegativeStockAllowedAsync(storage, companyId);

            decimal delta = movementType switch
            {
                "In" => Math.Abs(quantity),
                "Out" => -Math.Abs(quantity),
                "Adjustment" => quantity,
                "Transfer" => -Math.Abs(quantity),
                _ => quantity
            };

            var existing = FindStockItem(storage, companyId, productKey);
            var qtyBefore = existing?.QuantityOnHand ?? 0m;
            var avgBefore = existing?.AverageCost ?? 0m;
            if (avgBefore <= 0.0001m)
            {
                var catalogCost = ResolveCatalogUnitCost(storage, productKey);
                if (catalogCost.HasValue)
                    avgBefore = catalogCost.Value;
            }

            // Valorisation : sortie / ajustement négatif → CMUP courant ; entrée → coût fourni (sinon CMUP / catalogue).
            decimal? appliedUnitCost = null;
            decimal newAverage = avgBefore;

            if (delta > 0.0001m)
            {
                // Entrée physique
                var inboundCost = unitCost
                    ?? (avgBefore > 0 ? avgBefore : (decimal?)null)
                    ?? ResolveCatalogUnitCost(storage, productKey);
                if (inboundCost.HasValue && inboundCost.Value >= 0)
                {
                    appliedUnitCost = CmupCalculator.Round(inboundCost.Value);
                    newAverage = CmupCalculator.AfterInbound(qtyBefore, existing?.AverageCost ?? 0m, delta, appliedUnitCost.Value);
                }
            }
            else if (delta < -0.0001m)
            {
                // Sortie : valoriser au CMUP, ne pas recalculer
                appliedUnitCost = avgBefore > 0 ? CmupCalculator.Round(avgBefore) : unitCost.HasValue ? CmupCalculator.Round(unitCost.Value) : null;
            }

            var absQty = Math.Abs(delta);
            var movement = new StockMovement
            {
                ProductKey = resolvedKey,
                MovementType = movementType,
                Quantity = absQty,
                UnitCost = appliedUnitCost,
                StockValue = appliedUnitCost.HasValue ? CmupCalculator.Round(absQty * appliedUnitCost.Value) : null,
                ReferenceDocument = referenceDocument,
                Reason = reason,
                CompanyId = companyId,
                CreatedBy = createdBy ?? "System",
                CreatedAt = DateTime.UtcNow
            };
            await storage.InsertStockMovementAsync(movement);

            if (existing != null)
            {
                existing.QuantityOnHand += delta;
                // RG-CS2 : clamp à 0 si stock négatif interdit.
                if (!allowNegative && existing.QuantityOnHand < 0)
                    existing.QuantityOnHand = 0;
                if (existing.ReservedQuantity > existing.QuantityOnHand && !allowNegative)
                    existing.ReservedQuantity = existing.QuantityOnHand;
                if (delta > 0.0001m && appliedUnitCost.HasValue)
                    existing.AverageCost = newAverage;
                else if (existing.AverageCost <= 0.0001m && avgBefore > 0.0001m)
                    existing.AverageCost = CmupCalculator.Round(avgBefore);
                if (string.IsNullOrWhiteSpace(existing.CompanyId) && !string.IsNullOrWhiteSpace(companyId))
                    existing.CompanyId = companyId;
                existing.LastUpdated = DateTime.UtcNow;
                await storage.UpdateStockAsync(existing);
            }
            else if (delta > 0.0001m)
            {
                // Entrée sans ligne existante → créer le stock.
                await storage.InsertStockAsync(new StockItem
                {
                    ProductKey = resolvedKey,
                    QuantityOnHand = delta,
                    ReservedQuantity = 0,
                    MinStock = 0,
                    AverageCost = appliedUnitCost.HasValue ? appliedUnitCost.Value : 0m,
                    Unit = "PCS",
                    LastUpdated = DateTime.UtcNow,
                    CompanyId = companyId
                });
            }
            // Sortie sans ligne trouvée : mouvement journalisé seulement (pas de ligne fantôme à qty 0).
            // Évite de laisser intacte une autre ligne stock avec clé "Marque CODE" pendant qu'on sort sur "CODE".

            return movement;
        }

        /// <summary>CMUP courant pour un produit (0 si inconnu).</summary>
        public static decimal GetAverageCost(IStorageBroker storage, string? companyId, string productKey)
        {
            if (string.IsNullOrWhiteSpace(productKey)) return 0m;
            var avg = FindStockItem(storage, companyId, productKey)?.AverageCost ?? 0m;
            if (avg > 0.0001m) return avg;
            return ResolveCatalogUnitCost(storage, productKey) ?? 0m;
        }

        /// <summary>Prix d'achat catalogue (CPrice puis UnitPrice) pour initialiser / fallback CMUP.</summary>
        public static decimal? ResolveCatalogUnitCost(IStorageBroker storage, string productKey)
        {
            var candidates = CandidateKeys(productKey).ToList();
            if (candidates.Count == 0) return null;

            // Filtre SQL (IN) — ne jamais charger tout ErpProducts en mémoire.
            var product = storage.SelectAllErpProducts()
                .AsNoTracking()
                .Where(p =>
                    (p.Reference != null && candidates.Contains(p.Reference)) ||
                    (p.Ean != null && candidates.Contains(p.Ean)) ||
                    (p.ErpProductId != null && candidates.Contains(p.ErpProductId)))
                .Select(p => new { p.CPrice, p.UnitPrice })
                .FirstOrDefault();

            if (product == null) return null;
            if (product.CPrice.HasValue && product.CPrice.Value > 0) return CmupCalculator.Round(product.CPrice.Value);
            if (product.UnitPrice.HasValue && product.UnitPrice.Value > 0) return CmupCalculator.Round(product.UnitPrice.Value);
            return null;
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

        /// <summary>True si les deux clés désignent le même article (égalité, dernier token, ou suffixe).</summary>
        public static bool ProductKeysMatch(string? a, string? b)
        {
            if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b)) return false;
            var ca = CandidateKeys(a).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var cb = CandidateKeys(b).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (ca.Overlaps(cb)) return true;

            var na = ProductKeyHelper.Normalize(a.Trim());
            var nb = ProductKeyHelper.Normalize(b.Trim());
            if (na.EndsWith(" " + nb, StringComparison.OrdinalIgnoreCase)
                || nb.EndsWith(" " + na, StringComparison.OrdinalIgnoreCase))
                return true;

            return false;
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
            var candidates = CandidateKeys(productKey).ToList();
            if (candidates.Count == 0) return null;

            // Requête ciblée sur les clés candidates (évite AsEnumerable sur tout le stock).
            var matches = storage.SelectAllStock()
                .ForCompany(companyId)
                .Where(s => s.ProductKey != null && candidates.Contains(s.ProductKey))
                .ToList();

            if (matches.Count == 0)
            {
                // Fallback rare : clés "Marque CODE" vs "CODE" non présentes à l'identique en SQL.
                var suffix = candidates[^1];
                matches = storage.SelectAllStock()
                    .ForCompany(companyId)
                    .Where(s => s.ProductKey != null &&
                                (s.ProductKey == suffix || s.ProductKey.EndsWith(" " + suffix)))
                    .AsEnumerable()
                    .Where(s => ProductKeysMatch(s.ProductKey, productKey))
                    .ToList();
            }

            if (matches.Count == 0) return null;

            // Préférer match exact de clé, puis société courante, puis legacy sans CompanyId.
            var exactKey = matches.FirstOrDefault(s =>
                candidates.Any(c => string.Equals(s.ProductKey, c, StringComparison.OrdinalIgnoreCase)));
            var pool = exactKey != null ? matches.Where(s =>
                candidates.Any(c => string.Equals(s.ProductKey, c, StringComparison.OrdinalIgnoreCase))).ToList() : matches;

            if (!string.IsNullOrWhiteSpace(companyId))
            {
                var exactCompany = pool.FirstOrDefault(s =>
                    string.Equals(s.CompanyId, companyId, StringComparison.OrdinalIgnoreCase));
                if (exactCompany != null) return exactCompany;
            }

            return pool.FirstOrDefault(s => string.IsNullOrWhiteSpace(s.CompanyId))
                ?? pool.OrderByDescending(s => s.QuantityOnHand).First();
        }

        /// <summary>
        /// Corrige les sorties BL journalisées sous une clé produit différente de la ligne stock
        /// (ex. mouvement "14293" alors que le stock est "FF Group 14293") : ces sorties n'ont
        /// jamais diminué QuantityOnHand. Applique Σ Out orphelines sur la ligne principale
        /// et remet les doublons fantômes à 0.
        /// </summary>
        public static async Task<(int FamiliesFixed, int RowsTouched)> ReconcileMismatchedOutsAsync(
            IStorageBroker storage,
            string? companyId)
        {
            var stocks = storage.SelectAllStock().ForCompany(companyId).ToList();
            var outs = storage.SelectAllStockMovements()
                .ForCompany(companyId)
                .AsEnumerable()
                .Where(m => string.Equals(m.MovementType, "Out", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (stocks.Count == 0 || outs.Count == 0) return (0, 0);

            var allowNegative = await IsNegativeStockAllowedAsync(storage, companyId);
            var visited = new HashSet<int>();
            var familiesFixed = 0;
            var rowsTouched = 0;

            foreach (var seed in stocks.OrderByDescending(s => s.QuantityOnHand))
            {
                if (visited.Contains(seed.Id)) continue;

                var family = stocks.Where(s => ProductKeysMatch(s.ProductKey, seed.ProductKey)).ToList();
                foreach (var f in family) visited.Add(f.Id);

                var primary = family
                    .OrderByDescending(s => s.QuantityOnHand)
                    .ThenByDescending(s => string.IsNullOrWhiteSpace(s.Description) ? 0 : 1)
                    .ThenByDescending(s => string.IsNullOrWhiteSpace(s.Supplier) ? 0 : 1)
                    .First();

                // Sorties dont la clé journalisée ≠ clé de la ligne principale → jamais appliquées sur primary.
                var orphanOuts = outs
                    .Where(m =>
                        ProductKeysMatch(m.ProductKey, primary.ProductKey)
                        && !string.Equals(m.ProductKey, primary.ProductKey, StringComparison.OrdinalIgnoreCase))
                    .Sum(m => m.Quantity);

                if (orphanOuts <= 0.0001m && family.Count < 2)
                    continue;

                if (orphanOuts > 0.0001m)
                {
                    var corrected = primary.QuantityOnHand - orphanOuts;
                    if (!allowNegative && corrected < 0) corrected = 0;

                    if (Math.Abs(corrected - primary.QuantityOnHand) > 0.0001m)
                    {
                        primary.QuantityOnHand = corrected;
                        if (primary.ReservedQuantity > primary.QuantityOnHand)
                            primary.ReservedQuantity = Math.Max(0m, primary.QuantityOnHand);
                        primary.LastUpdated = DateTime.UtcNow;
                        if (string.IsNullOrWhiteSpace(primary.CompanyId) && !string.IsNullOrWhiteSpace(companyId))
                            primary.CompanyId = companyId;
                        await storage.UpdateStockAsync(primary);
                        rowsTouched++;
                    }
                }

                foreach (var other in family.Where(f => f.Id != primary.Id))
                {
                    if (Math.Abs(other.QuantityOnHand) < 0.0001m && other.ReservedQuantity <= 0.0001m)
                        continue;
                    other.QuantityOnHand = 0;
                    other.ReservedQuantity = 0;
                    other.LastUpdated = DateTime.UtcNow;
                    await storage.UpdateStockAsync(other);
                    rowsTouched++;
                }

                if (orphanOuts > 0.0001m || family.Count >= 2)
                    familiesFixed++;
            }

            return (familiesFixed, rowsTouched);
        }
    }
}
