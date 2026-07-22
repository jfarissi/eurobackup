using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Backup.Web.Api.Server.Services.ErpSync
{
    public class ErpProductSyncService : IErpProductSyncService
    {
        private static readonly ConcurrentDictionary<string, CancellationTokenSource> ActiveJobs = new();

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        /// <summary>
        /// Champs Excel — non écrasés s'ils sont déjà renseignés.
        /// Mapping prix :
        /// - Excel « prix achat » → CPrice
        /// - Excel « prix vente » (TTC) → UnitPrice / RPrice
        /// - ERP PriceHT = prix de vente HT (jamais rempli depuis Excel)
        /// Les prix ERP écrasent les prix locaux au sync (avec historique PriceChanged).
        /// </summary>
        private static readonly HashSet<string> ExcelProtectedFields = new(StringComparer.Ordinal)
        {
            nameof(ErpProduct.Name),
            nameof(ErpProduct.Reference),
            nameof(ErpProduct.Ean),
            nameof(ErpProduct.Brand),
            nameof(ErpProduct.Comment)
        };

        private static readonly HashSet<string> DecimalTrackedFields = new(StringComparer.Ordinal)
        {
            nameof(ErpProduct.UnitPrice),
            nameof(ErpProduct.PriceHT),
            nameof(ErpProduct.CPrice),
            nameof(ErpProduct.RPrice),
            nameof(ErpProduct.DiscountPrice),
            nameof(ErpProduct.StockQuantity),
            nameof(ErpProduct.PromoPrice)
        };

        private static readonly string[] TrackedFields =
        {
            nameof(ErpProduct.Name),
            nameof(ErpProduct.Name2),
            nameof(ErpProduct.Reference),
            nameof(ErpProduct.Ean),
            nameof(ErpProduct.Brand),
            nameof(ErpProduct.UnitPrice),
            nameof(ErpProduct.PriceHT),
            nameof(ErpProduct.CPrice),
            nameof(ErpProduct.RPrice),
            nameof(ErpProduct.DiscountPrice),
            nameof(ErpProduct.StockQuantity),
            nameof(ErpProduct.Comment),
            nameof(ErpProduct.TypeName),
            nameof(ErpProduct.SubTypeName),
            nameof(ErpProduct.MainTypeName),
            nameof(ErpProduct.PromoActive),
            nameof(ErpProduct.PromoPrice),
            nameof(ErpProduct.Archived),
            nameof(ErpProduct.ErpProductId)
        };

        private readonly HttpClient _httpClient;
        private readonly ErpSyncOptions _options;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<ErpProductSyncService> _logger;

        public ErpProductSyncService(
            HttpClient httpClient,
            IOptions<ErpSyncOptions> options,
            IServiceScopeFactory scopeFactory,
            ILogger<ErpProductSyncService> logger)
        {
            _httpClient = httpClient;
            _options = options.Value ?? new ErpSyncOptions();
            _scopeFactory = scopeFactory;
            _logger = logger;

            if (_httpClient.Timeout == Timeout.InfiniteTimeSpan || _httpClient.Timeout.TotalSeconds < 1)
                _httpClient.Timeout = TimeSpan.FromSeconds(Math.Max(5, _options.TimeoutSeconds));
        }

        public Task<ErpSyncLog> SyncAllProductsAsync(CancellationToken ct = default) =>
            SyncAllProductsAsync(modeOverride: null, ct);

        public async Task<ErpSyncLog> SyncAllProductsAsync(string? modeOverride, CancellationToken ct = default)
        {
            var mode = (modeOverride ?? _options.SyncMode ?? "LocalEnrich").Trim();
            if (mode.Equals("FullCatalog", StringComparison.OrdinalIgnoreCase))
                return await SyncFullCatalogAsync(ct);
            return await SyncLocalEnrichAsync(ct);
        }

        public async Task<ErpSyncLog> StartSyncAllAsync(CancellationToken ct = default)
        {
            using var scope = _scopeFactory.CreateScope();
            var storage = scope.ServiceProvider.GetRequiredService<IStorageBroker>();

            // Toujours purger les jobs Running (fantômes après restart Docker inclus)
            // avant d'en démarrer un nouveau — sinon l'UI reprend un compteur figé.
            await CancelAllRunningSyncsInternalAsync(storage, ct);

            var mode = (_options.SyncMode ?? "LocalEnrich").Trim();
            var jobId = Guid.NewGuid().ToString("N");
            var syncLog = await storage.InsertErpSyncLogAsync(new ErpSyncLog
            {
                JobId = jobId,
                Status = "Running",
                StartedAt = DateTime.UtcNow,
                ProcessedProducts = 0,
                TotalProducts = 0,
                Details = JsonSerializer.Serialize(new
                {
                    mode,
                    phase = "starting"
                })
            });

            var jobCts = new CancellationTokenSource();
            ActiveJobs[jobId] = jobCts;

            _ = Task.Run(async () =>
            {
                try
                {
                    if (mode.Equals("FullCatalog", StringComparison.OrdinalIgnoreCase))
                        await SyncFullCatalogAsync(jobCts.Token, jobId);
                    else
                        await SyncLocalEnrichAsync(jobCts.Token, jobId);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogWarning("Background ERP sync {JobId} cancelled", jobId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Background ERP sync {JobId} crashed", jobId);
                    try
                    {
                        using var failScope = _scopeFactory.CreateScope();
                        var failStorage = failScope.ServiceProvider.GetRequiredService<IStorageBroker>();
                        var log = await failStorage.SelectAllErpSyncLogs()
                            .FirstOrDefaultAsync(s => s.JobId == jobId);
                        if (log != null && log.Status == "Running")
                        {
                            log.Status = "Failed";
                            log.ErrorMessage = ex.Message;
                            log.CompletedAt = DateTime.UtcNow;
                            await failStorage.UpdateErpSyncLogAsync(log);
                        }
                    }
                    catch (Exception updateEx)
                    {
                        _logger.LogError(updateEx, "Failed to mark sync {JobId} as Failed", jobId);
                    }
                }
                finally
                {
                    ActiveJobs.TryRemove(jobId, out var cts);
                    cts?.Dispose();
                }
            });

            return syncLog;
        }

        public Task<ErpSyncLog> SyncCatalogAsync(ErpCatalogSyncFilter filter, CancellationToken ct = default) =>
            SyncCatalogAsync(filter, existingJobId: null, ct);

        public async Task<ErpSyncLog> StartSyncCatalogAsync(
            ErpCatalogSyncFilter filter,
            bool cancelPrevious = true,
            CancellationToken ct = default)
        {
            if (filter == null || !filter.HasAnyFilter)
                throw new ArgumentException("Au moins un filtre requis (brand, mainTypeId, typeId ou subTypeId).");

            using var scope = _scopeFactory.CreateScope();
            var storage = scope.ServiceProvider.GetRequiredService<IStorageBroker>();

            if (cancelPrevious)
                await CancelRunningSyncInternalAsync(storage, ct);
            else
            {
                var existing = await GetRunningSyncLogAsync(storage, ct);
                if (existing != null)
                {
                    throw new InvalidOperationException(
                        $"Une sync est déjà en cours ({existing.ProcessedProducts}/{existing.TotalProducts}). "
                        + "Annulez-la ou relancez avec cancelPrevious=true.");
                }
            }

            var localCount = await ApplyCatalogFilter(storage.SelectAllErpProducts(), filter)
                .CountAsync(ct);

            var jobId = Guid.NewGuid().ToString("N");
            var syncLog = await storage.InsertErpSyncLogAsync(new ErpSyncLog
            {
                JobId = jobId,
                Status = "Running",
                StartedAt = DateTime.UtcNow,
                TotalProducts = localCount,
                ProcessedProducts = 0,
                Details = JsonSerializer.Serialize(new
                {
                    mode = "FilteredLocal",
                    phase = "starting",
                    filter = new
                    {
                        filter.MainTypeId,
                        filter.TypeId,
                        filter.SubTypeId,
                        filter.Brand
                    },
                    localMatches = localCount
                })
            });

            var capturedFilter = new ErpCatalogSyncFilter
            {
                MainTypeId = filter.MainTypeId,
                TypeId = filter.TypeId,
                SubTypeId = filter.SubTypeId,
                Brand = filter.Brand
            };

            var jobCts = new CancellationTokenSource();
            ActiveJobs[jobId] = jobCts;

            _ = Task.Run(async () =>
            {
                try
                {
                    await SyncCatalogAsync(capturedFilter, jobId, jobCts.Token);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogWarning("ERP filtered sync {JobId} cancelled", jobId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Background ERP filtered sync {JobId} crashed", jobId);
                    try
                    {
                        using var failScope = _scopeFactory.CreateScope();
                        var failStorage = failScope.ServiceProvider.GetRequiredService<IStorageBroker>();
                        var log = await failStorage.SelectAllErpSyncLogs()
                            .FirstOrDefaultAsync(s => s.JobId == jobId);
                        if (log != null && log.Status == "Running")
                        {
                            log.Status = "Failed";
                            log.ErrorMessage = ex.Message;
                            log.CompletedAt = DateTime.UtcNow;
                            await failStorage.UpdateErpSyncLogAsync(log);
                        }
                    }
                    catch (Exception updateEx)
                    {
                        _logger.LogError(updateEx, "Failed to mark filtered sync {JobId} as Failed", jobId);
                    }
                }
                finally
                {
                    ActiveJobs.TryRemove(jobId, out var cts);
                    cts?.Dispose();
                }
            });

            return syncLog;
        }

        public async Task<ErpSyncLog?> CancelRunningSyncAsync(CancellationToken ct = default)
        {
            using var scope = _scopeFactory.CreateScope();
            var storage = scope.ServiceProvider.GetRequiredService<IStorageBroker>();
            return await CancelRunningSyncInternalAsync(storage, ct);
        }

        private static async Task<ErpSyncLog?> GetRunningSyncLogAsync(IStorageBroker storage, CancellationToken ct)
        {
            return await storage.SelectAllErpSyncLogs()
                .AsNoTracking()
                .Where(s => s.Status == "Running")
                .OrderByDescending(s => s.StartedAt)
                .FirstOrDefaultAsync(ct);
        }

        private async Task<ErpSyncLog?> CancelRunningSyncInternalAsync(IStorageBroker storage, CancellationToken ct)
        {
            var cancelled = await CancelAllRunningSyncsInternalAsync(storage, ct);
            return cancelled.FirstOrDefault();
        }

        /// <summary>
        /// Annule tous les jobs Running (y compris fantômes sans ActiveJobs après restart).
        /// </summary>
        private async Task<List<ErpSyncLog>> CancelAllRunningSyncsInternalAsync(
            IStorageBroker storage,
            CancellationToken ct)
        {
            var runningJobs = await storage.SelectAllErpSyncLogs()
                .Where(s => s.Status == "Running")
                .OrderByDescending(s => s.StartedAt)
                .ToListAsync(ct);

            if (runningJobs.Count == 0)
                return runningJobs;

            foreach (var running in runningJobs)
            {
                if (ActiveJobs.TryRemove(running.JobId, out var cts))
                {
                    cts.Cancel();
                    cts.Dispose();
                }

                running.Status = "Cancelled";
                running.CompletedAt = DateTime.UtcNow;
                running.ErrorMessage = string.IsNullOrWhiteSpace(running.ErrorMessage)
                    ? "Annulé"
                    : running.ErrorMessage;
                await storage.UpdateErpSyncLogAsync(running);
                _logger.LogWarning("ERP sync {JobId} cancelled (processed={Processed})",
                    running.JobId, running.ProcessedProducts);
            }

            return runningJobs;
        }

        public async Task<ErpSyncLog?> GetSyncLogByJobIdAsync(string jobId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(jobId))
                return null;

            using var scope = _scopeFactory.CreateScope();
            var storage = scope.ServiceProvider.GetRequiredService<IStorageBroker>();
            return await storage.SelectErpSyncLogByJobIdAsync(jobId.Trim());
        }

        public async Task<ErpProduct?> SyncProductByIdAsync(string erpProductId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(erpProductId))
                return null;

            var id = erpProductId.Trim();
            using var scope = _scopeFactory.CreateScope();
            var storage = scope.ServiceProvider.GetRequiredService<IStorageBroker>();
            var jobId = $"manual-{Guid.NewGuid():N}";

            // A) Fiche locale déjà connue par ErpProductId (prioritaire sur la PK locale)
            var localByErpId = await storage.SelectAllErpProducts()
                .FirstOrDefaultAsync(p => p.ErpProductId == id, ct);
            if (localByErpId != null)
                return await EnrichOrFetchForLocalAsync(storage, localByErpId, jobId, ct, preferredErpId: id);

            // B) PK locale uniquement si aucune fiche avec cet ErpProductId
            //    (évite de confondre Id MySQL 375765 avec ERP 375765)
            if (int.TryParse(id, out var localPk))
            {
                var byPk = await storage.SelectAllErpProducts()
                    .FirstOrDefaultAsync(p => p.Id == localPk, ct);
                if (byPk != null
                    && (byPk.ErpProductId.StartsWith("XLS-", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(byPk.ErpProductId, id, StringComparison.OrdinalIgnoreCase)))
                {
                    return await EnrichOrFetchForLocalAsync(storage, byPk, jobId, ct, preferredErpId: id);
                }
            }

            // C) Pas de fiche locale : fetch ERP direct (comme Postman getProductByID/{id})
            var remote = await FetchProductByIdAsync(id, ct)
                ?? throw new InvalidOperationException(
                    $"Produit ERP {id} introuvable via getProductByID.");

            return await UpsertFromRemoteAsync(storage, remote, jobId, ct);
        }

        /// <summary>Sync par PK MySQL (bouton UI Produits).</summary>
        public async Task<ErpProduct?> SyncLocalProductByIdAsync(int localId, CancellationToken ct = default)
        {
            using var scope = _scopeFactory.CreateScope();
            var storage = scope.ServiceProvider.GetRequiredService<IStorageBroker>();
            var jobId = $"manual-{Guid.NewGuid():N}";

            var local = await storage.SelectAllErpProducts()
                .FirstOrDefaultAsync(p => p.Id == localId, ct);
            if (local == null)
                return null;

            return await EnrichOrFetchForLocalAsync(storage, local, jobId, ct);
        }

        private async Task<ErpProduct> EnrichOrFetchForLocalAsync(
            IStorageBroker storage,
            ErpProduct local,
            string jobId,
            CancellationToken ct,
            string? preferredErpId = null)
        {
            ErpProductDto? remote = null;

            // Priorité : ID ERP réel demandé / stocké
            var erpId = preferredErpId;
            if (string.IsNullOrWhiteSpace(erpId)
                || erpId.StartsWith("XLS-", StringComparison.OrdinalIgnoreCase))
            {
                erpId = local.ErpProductId;
            }

            if (!string.IsNullOrWhiteSpace(erpId)
                && !erpId.StartsWith("XLS-", StringComparison.OrdinalIgnoreCase))
            {
                remote = await FetchProductByIdAsync(erpId, ct);
            }

            remote ??= await ResolveRemoteProductAsync(local, ct);

            if (remote == null || string.IsNullOrWhiteSpace(remote.Id))
            {
                throw new InvalidOperationException(
                    $"Aucune fiche ERP trouvée pour le produit local #{local.Id} " +
                    $"(ErpId={local.ErpProductId}, Ref={local.Reference}, Ean={local.Ean}).");
            }

            return await MergeRemoteIntoLocalAsync(storage, local, remote, jobId, ct);
        }

        private async Task<ErpProduct> UpsertFromRemoteAsync(
            IStorageBroker storage,
            ErpProductDto remote,
            string jobId,
            CancellationToken ct)
        {
            var mapped = MapDto(remote);
            mapped.LastSyncAt = DateTime.UtcNow;

            var existing = await FindLocalProductForRemoteAsync(storage, mapped, ct);

            if (existing == null)
            {
                mapped.CreatedAt = DateTime.UtcNow;
                mapped.UpdatedAt = DateTime.UtcNow;
                mapped.DataSource = "Erp";
                mapped.FromExcel = false;
                var inserted = await storage.InsertErpProductAsync(mapped);
                await storage.InsertErpProductChangeLogAsync(new ErpProductChangeLog
                {
                    ErpProductId = inserted.Id,
                    ChangeType = "Created",
                    FieldName = "*",
                    NewValue = mapped.Name ?? mapped.Reference ?? mapped.ErpProductId,
                    DetectedAt = DateTime.UtcNow,
                    SyncJobId = jobId,
                    IsRead = false
                });
                return await storage.SelectAllErpProducts().AsNoTracking().FirstAsync(p => p.Id == inserted.Id, ct);
            }

            return await MergeRemoteIntoLocalAsync(storage, existing, remote, jobId, ct);
        }

        private async Task<ErpProduct> MergeRemoteIntoLocalAsync(
            IStorageBroker storage,
            ErpProduct local,
            ErpProductDto remote,
            string jobId,
            CancellationToken ct)
        {
            var mapped = MapDto(remote);
            mapped.LastSyncAt = DateTime.UtcNow;

            if (!string.Equals(local.ErpProductId, mapped.ErpProductId, StringComparison.OrdinalIgnoreCase))
            {
                var taken = await storage.SelectAllErpProducts()
                    .AnyAsync(p => p.ErpProductId == mapped.ErpProductId && p.Id != local.Id, ct);
                if (!taken)
                    local.ErpProductId = mapped.ErpProductId;
            }

            var changes = DetectChanges(local, mapped, jobId, local.FromExcel);
            ApplyMappedValues(local, mapped, preserveExcelFields: local.FromExcel);
            local.DataSource = local.FromExcel ? "Merged" : (local.DataSource ?? "Erp");
            local.UpdatedAt = DateTime.UtcNow;
            local.LastSyncAt = mapped.LastSyncAt;
            await storage.UpdateErpProductAsync(local);

            if (changes.Count > 0)
            {
                foreach (var change in changes)
                    change.ErpProductId = local.Id;
                await storage.InsertErpProductChangeLogsAsync(changes);
            }

            return await storage.SelectAllErpProducts().AsNoTracking().FirstAsync(p => p.Id == local.Id, ct);
        }

        public async Task<List<ErpProductChangeLog>> GetUnreadChangesAsync(CancellationToken ct = default)
        {
            using var scope = _scopeFactory.CreateScope();
            var storage = scope.ServiceProvider.GetRequiredService<IStorageBroker>();
            return await storage.SelectAllErpProductChangeLogs()
                .AsNoTracking()
                .Where(c => !c.IsRead)
                .OrderByDescending(c => c.DetectedAt)
                .Take(500)
                .ToListAsync(ct);
        }

        public async Task MarkChangesAsReadAsync(List<int> changeLogIds, CancellationToken ct = default)
        {
            if (changeLogIds == null || changeLogIds.Count == 0)
                return;

            using var scope = _scopeFactory.CreateScope();
            var storage = scope.ServiceProvider.GetRequiredService<IStorageBroker>();
            await storage.MarkErpProductChangeLogsAsReadAsync(changeLogIds);
        }

        public async Task<int> DeleteChangesAsync(List<int> changeLogIds, CancellationToken ct = default)
        {
            if (changeLogIds == null || changeLogIds.Count == 0)
                return 0;

            using var scope = _scopeFactory.CreateScope();
            var storage = scope.ServiceProvider.GetRequiredService<IStorageBroker>();
            return await storage.DeleteErpProductChangeLogsAsync(changeLogIds);
        }

        public async Task<int> CleanupFormattingFalsePositivesAsync(CancellationToken ct = default)
        {
            using var scope = _scopeFactory.CreateScope();
            var storage = scope.ServiceProvider.GetRequiredService<IStorageBroker>();

            var products = await storage.SelectAllErpProducts().ToListAsync(ct);
            var byReference = products
                .Where(p => !string.IsNullOrWhiteSpace(p.Reference))
                .GroupBy(p => p.Reference!, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

            // Charger en mémoire (évite les soucis de traduction EF sur HashSet.Contains).
            var priceFields = new HashSet<string>(DecimalTrackedFields, StringComparer.Ordinal);
            var logs = await storage.SelectAllErpProductChangeLogs()
                .Include(c => c.ErpProduct)
                .AsNoTracking()
                .ToListAsync(ct);

            var priceLogs = logs.Where(c => priceFields.Contains(c.FieldName)).ToList();

            var idsToDelete = priceLogs
                .Where(c =>
                    DecimalValuesEquivalent(c.OldValue, c.NewValue)
                    || IsEmptyOrZeroDecimalFalsePositive(c)
                    || IsExcelCostVsSaleHtFalsePositive(c)
                    || IsPackVsUnitPriceFalsePositive(c, byReference))
                .Select(c => c.Id)
                .Distinct()
                .ToList();

            // Purge aussi les EAN contaminés (pack qui porte l'EAN de l'unité).
            var eanFixes = 0;
            foreach (var product in products)
            {
                if (!IsPackProductName(product.Name, product.Name2)
                    || string.IsNullOrWhiteSpace(product.Ean)
                    || string.IsNullOrWhiteSpace(product.Reference)
                    || !byReference.TryGetValue(product.Reference, out var siblings))
                {
                    continue;
                }

                var unitOwnsEan = siblings.Any(p =>
                    p.Id != product.Id
                    && !IsPackProductName(p.Name, p.Name2)
                    && string.Equals(p.Ean, product.Ean, StringComparison.OrdinalIgnoreCase));

                if (!unitOwnsEan)
                    continue;

                product.Ean = null;
                product.UpdatedAt = DateTime.UtcNow;
                await storage.UpdateErpProductAsync(product);
                eanFixes++;
            }

            if (idsToDelete.Count == 0 && eanFixes == 0)
                return 0;

            var deleted = idsToDelete.Count == 0
                ? 0
                : await storage.DeleteErpProductChangeLogsAsync(idsToDelete);

            _logger.LogInformation(
                "ERP changes cleanup: deleted {Deleted} logs, fixed {EanFixes} pack EAN contaminations",
                deleted,
                eanFixes);
            return deleted;
        }

        /// <summary>
        /// Faux positif historique : OldValue = prix d'achat Excel (CPrice),
        /// NewValue = PriceHT ERP (vente HT).
        /// </summary>
        private static bool IsExcelCostVsSaleHtFalsePositive(ErpProductChangeLog change)
        {
            if (!string.Equals(change.FieldName, nameof(ErpProduct.PriceHT), StringComparison.Ordinal))
                return false;

            var oldVal = ParseDecimal(change.OldValue);
            var newVal = ParseDecimal(change.NewValue);
            if (!oldVal.HasValue || !newVal.HasValue || oldVal == newVal)
                return false;

            var cost = change.ErpProduct?.CPrice;
            return cost.HasValue && cost.Value == oldVal.Value;
        }

        /// <summary>
        /// Faux positif unité ↔ pack (même ref) : ex. 66.97 (pack 50) ↔ 2.10 (unité).
        /// </summary>
        private static bool IsPackVsUnitPriceFalsePositive(
            ErpProductChangeLog change,
            IReadOnlyDictionary<string, List<ErpProduct>> byReference)
        {
            if (change.FieldName is not (
                nameof(ErpProduct.UnitPrice)
                or nameof(ErpProduct.PriceHT)
                or nameof(ErpProduct.RPrice)
                or nameof(ErpProduct.CPrice)))
            {
                return false;
            }

            var oldVal = ParseDecimal(change.OldValue);
            var newVal = ParseDecimal(change.NewValue);
            if (!oldVal.HasValue || !newVal.HasValue)
                return false;

            var min = Math.Min(Math.Abs(oldVal.Value), Math.Abs(newVal.Value));
            var max = Math.Max(Math.Abs(oldVal.Value), Math.Abs(newVal.Value));
            if (min <= 0)
                return false;

            var ratio = max / min;
            if (ratio < 5m)
                return false;

            var product = change.ErpProduct;
            var nameIsPack = IsPackProductName(product?.Name, product?.Name2);

            // Gros écart + nom pack (ex. verpakking 50) → faux match unité/pack.
            if (nameIsPack)
                return true;

            // Même sans nom pack : deux fiches même ref (unité + pack) + écart de prix ×5+.
            if (product != null
                && !string.IsNullOrWhiteSpace(product.Reference)
                && byReference.TryGetValue(product.Reference, out var siblings)
                && siblings.Count >= 2
                && siblings.Any(p => IsPackProductName(p.Name, p.Name2))
                && siblings.Any(p => !IsPackProductName(p.Name, p.Name2)))
            {
                return true;
            }

            return false;
        }

        private async Task<ErpSyncLog> SyncLocalEnrichAsync(CancellationToken ct, string? existingJobId = null)
        {
            var jobId = existingJobId ?? Guid.NewGuid().ToString("N");
            using var scope = _scopeFactory.CreateScope();
            var storage = scope.ServiceProvider.GetRequiredService<IStorageBroker>();

            ErpSyncLog syncLog;
            if (!string.IsNullOrWhiteSpace(existingJobId))
            {
                syncLog = await storage.SelectAllErpSyncLogs()
                    .FirstAsync(s => s.JobId == jobId, ct);
            }
            else
            {
                syncLog = await storage.InsertErpSyncLogAsync(new ErpSyncLog
                {
                    JobId = jobId,
                    Status = "Running",
                    StartedAt = DateTime.UtcNow
                });
            }

            try
            {
                var locals = await storage.SelectAllErpProducts()
                    .OrderBy(p => p.Id)
                    .ToListAsync(ct);

                syncLog.TotalProducts = locals.Count;
                syncLog.ProcessedProducts = 0;
                syncLog.Details = JsonSerializer.Serialize(new { mode = "LocalEnrich", phase = "enriching" });
                await storage.UpdateErpSyncLogAsync(syncLog);

                _logger.LogInformation(
                    "ERP LocalEnrich sync {JobId}: {Count} local products",
                    jobId,
                    locals.Count);

                var processed = 0;
                foreach (var batch in locals.Chunk(Math.Max(1, _options.BatchSize)))
                {
                    foreach (var local in batch)
                    {
                        ct.ThrowIfCancellationRequested();
                        try
                        {
                            var result = await EnrichLocalProductAsync(storage, local, jobId, ct);
                            if (result == UpsertResult.Created)
                                syncLog.NewProducts++;
                            else if (result == UpsertResult.Updated)
                                syncLog.UpdatedProducts++;
                        }
                        catch (Exception ex)
                        {
                            syncLog.FailedProducts++;
                            _logger.LogWarning(ex, "ERP sync {JobId}: enrich failed for local Id={Id} Ref={Ref}",
                                jobId, local.Id, local.Reference);
                        }

                        processed++;
                        syncLog.ProcessedProducts = processed;
                    }

                    await storage.UpdateErpSyncLogAsync(syncLog);
                }

                return await FinalizeSyncLogAsync(storage, syncLog, jobId, new
                {
                    mode = "LocalEnrich",
                    processed
                }, ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return await FailSyncLogAsync(storage, syncLog, jobId, ex);
            }
        }

        private async Task<ErpSyncLog> SyncFullCatalogAsync(CancellationToken ct, string? existingJobId = null)
        {
            var jobId = existingJobId ?? Guid.NewGuid().ToString("N");
            using var scope = _scopeFactory.CreateScope();
            var storage = scope.ServiceProvider.GetRequiredService<IStorageBroker>();

            ErpSyncLog syncLog;
            if (!string.IsNullOrWhiteSpace(existingJobId))
            {
                syncLog = await storage.SelectAllErpSyncLogs()
                    .FirstAsync(s => s.JobId == jobId, ct);
            }
            else
            {
                syncLog = await storage.InsertErpSyncLogAsync(new ErpSyncLog
                {
                    JobId = jobId,
                    Status = "Running",
                    StartedAt = DateTime.UtcNow
                });
            }

            try
            {
                syncLog.Details = JsonSerializer.Serialize(new { mode = "FullCatalog", phase = "collecting" });
                await storage.UpdateErpSyncLogAsync(syncLog);

                var productIds = await CollectProductIdsAsync(filter: null, ct);
                return await UpsertCollectedProductIdsAsync(
                    storage, syncLog, jobId, productIds, ct,
                    finalizeDetails: processed => new
                    {
                        mode = "FullCatalog",
                        productIdsCollected = productIds.Count,
                        processed
                    });
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return await FailSyncLogAsync(storage, syncLog, jobId, ex);
            }
        }

        private async Task<ErpSyncLog> SyncCatalogAsync(
            ErpCatalogSyncFilter filter,
            string? existingJobId,
            CancellationToken ct)
        {
            if (filter == null || !filter.HasAnyFilter)
                throw new ArgumentException("Au moins un filtre requis (brand, mainTypeId, typeId ou subTypeId).");

            var jobId = existingJobId ?? Guid.NewGuid().ToString("N");
            using var scope = _scopeFactory.CreateScope();
            var storage = scope.ServiceProvider.GetRequiredService<IStorageBroker>();

            ErpSyncLog syncLog;
            if (!string.IsNullOrWhiteSpace(existingJobId))
            {
                syncLog = await storage.SelectAllErpSyncLogs()
                    .FirstAsync(s => s.JobId == jobId, ct);
            }
            else
            {
                syncLog = await storage.InsertErpSyncLogAsync(new ErpSyncLog
                {
                    JobId = jobId,
                    Status = "Running",
                    StartedAt = DateTime.UtcNow
                });
            }

            try
            {
                var locals = await ApplyCatalogFilter(
                        storage.SelectAllErpProducts(),
                        filter)
                    .OrderBy(p => p.Id)
                    .ToListAsync(ct);

                syncLog.TotalProducts = locals.Count;
                syncLog.ProcessedProducts = 0;
                syncLog.Details = JsonSerializer.Serialize(new
                {
                    mode = "FilteredLocal",
                    phase = "enriching",
                    filter = new
                    {
                        filter.MainTypeId,
                        filter.TypeId,
                        filter.SubTypeId,
                        filter.Brand
                    },
                    localMatches = locals.Count
                });
                await storage.UpdateErpSyncLogAsync(syncLog);

                _logger.LogInformation(
                    "ERP filtered sync {JobId}: {Count} local products match filter",
                    jobId,
                    locals.Count);

                var processed = 0;
                foreach (var batch in locals.Chunk(Math.Max(1, _options.BatchSize)))
                {
                    foreach (var local in batch)
                    {
                        ct.ThrowIfCancellationRequested();
                        try
                        {
                            var result = await EnrichLocalProductAsync(storage, local, jobId, ct);
                            if (result == UpsertResult.Created)
                                syncLog.NewProducts++;
                            else if (result == UpsertResult.Updated)
                                syncLog.UpdatedProducts++;
                        }
                        catch (Exception ex)
                        {
                            syncLog.FailedProducts++;
                            _logger.LogWarning(ex, "ERP sync {JobId}: enrich failed for local Id={Id} Ref={Ref}",
                                jobId, local.Id, local.Reference);
                        }

                        processed++;
                        syncLog.ProcessedProducts = processed;
                    }

                    await storage.UpdateErpSyncLogAsync(syncLog);
                }

                using var catalogScope = _scopeFactory.CreateScope();
                var catalogSync = catalogScope.ServiceProvider.GetRequiredService<IErpCatalogSyncService>();
                var catalogResult = await catalogSync.RebuildFromProductsAsync(ct);

                return await FinalizeSyncLogAsync(storage, syncLog, jobId, new
                {
                    mode = "FilteredLocal",
                    filter = new
                    {
                        filter.MainTypeId,
                        filter.TypeId,
                        filter.SubTypeId,
                        filter.Brand
                    },
                    localMatches = locals.Count,
                    processed,
                    catalog = catalogResult
                }, ct);
            }
            catch (Exception ex)
            {
                return await FailSyncLogAsync(storage, syncLog, jobId, ex);
            }
        }

        private static IQueryable<ErpProduct> ApplyCatalogFilter(
            IQueryable<ErpProduct> query,
            ErpCatalogSyncFilter filter)
        {
            if (!string.IsNullOrWhiteSpace(filter.Brand))
            {
                var brandTerm = filter.Brand.Trim().ToLowerInvariant();
                query = query.Where(p => p.Brand != null && p.Brand.ToLower() == brandTerm);
            }

            if (!string.IsNullOrWhiteSpace(filter.SubTypeId))
                query = query.Where(p => p.SubTypeID == filter.SubTypeId);
            else if (!string.IsNullOrWhiteSpace(filter.TypeId))
                query = query.Where(p => p.TypeID == filter.TypeId);
            else if (!string.IsNullOrWhiteSpace(filter.MainTypeId))
                query = query.Where(p => p.MainTypeID == filter.MainTypeId);

            return query;
        }

        private async Task<ErpSyncLog> UpsertCollectedProductIdsAsync(
            IStorageBroker storage,
            ErpSyncLog syncLog,
            string jobId,
            HashSet<string> productIds,
            CancellationToken ct,
            bool rebuildCatalog = false,
            ErpCatalogSyncFilter? catalogFilter = null,
            Func<int, object>? finalizeDetails = null)
        {
            syncLog.TotalProducts = productIds.Count;
            syncLog.ProcessedProducts = 0;
            syncLog.Details = JsonSerializer.Serialize(new { phase = "upserting", total = productIds.Count });
            await storage.UpdateErpSyncLogAsync(syncLog);

            var processed = 0;
            foreach (var batch in productIds.Chunk(Math.Max(1, _options.BatchSize)))
            {
                foreach (var erpId in batch)
                {
                    ct.ThrowIfCancellationRequested();
                    try
                    {
                        var result = await UpsertProductByErpIdAsync(storage, erpId, jobId, preserveExcelFields: true, ct);
                        if (result == UpsertResult.Created)
                            syncLog.NewProducts++;
                        else if (result == UpsertResult.Updated)
                            syncLog.UpdatedProducts++;
                    }
                    catch (Exception ex)
                    {
                        syncLog.FailedProducts++;
                        _logger.LogWarning(ex, "ERP sync {JobId}: failed product {ErpId}", jobId, erpId);
                    }

                    processed++;
                    syncLog.ProcessedProducts = processed;
                }

                await storage.UpdateErpSyncLogAsync(syncLog);
            }

            object details;
            if (rebuildCatalog && catalogFilter != null)
            {
                using var catalogScope = _scopeFactory.CreateScope();
                var catalogSync = catalogScope.ServiceProvider.GetRequiredService<IErpCatalogSyncService>();
                var catalogResult = await catalogSync.RebuildFromProductsAsync(ct);
                details = new
                {
                    mode = "CatalogFilter",
                    filter = new
                    {
                        catalogFilter.MainTypeId,
                        catalogFilter.TypeId,
                        catalogFilter.SubTypeId,
                        catalogFilter.Brand
                    },
                    productIdsCollected = productIds.Count,
                    processed,
                    catalog = catalogResult
                };
            }
            else
            {
                details = finalizeDetails?.Invoke(processed) ?? new { processed };
            }

            return await FinalizeSyncLogAsync(storage, syncLog, jobId, details, ct);
        }

        private async Task<UpsertResult> EnrichLocalProductAsync(
            IStorageBroker storage,
            ErpProduct local,
            string jobId,
            CancellationToken ct)
        {
            var remote = await ResolveRemoteProductAsync(local, ct);
            if (remote == null || string.IsNullOrWhiteSpace(remote.Id))
                return UpsertResult.Unchanged;

            var mapped = MapDto(remote);
            mapped.LastSyncAt = DateTime.UtcNow;

            // Si l'ID ERP réel diffère du provisoire XLS-*, basculer (si libre)
            if (!string.Equals(local.ErpProductId, mapped.ErpProductId, StringComparison.OrdinalIgnoreCase))
            {
                var taken = await storage.SelectAllErpProducts()
                    .AnyAsync(p => p.ErpProductId == mapped.ErpProductId && p.Id != local.Id, ct);
                if (!taken)
                    local.ErpProductId = mapped.ErpProductId;
            }

            var changes = DetectChanges(local, mapped, jobId, local.FromExcel);
            ApplyMappedValues(local, mapped, preserveExcelFields: local.FromExcel);
            local.DataSource = local.FromExcel ? "Merged" : (local.DataSource ?? "Erp");
            local.UpdatedAt = DateTime.UtcNow;
            local.LastSyncAt = mapped.LastSyncAt;
            await storage.UpdateErpProductAsync(local);

            if (changes.Count > 0)
            {
                foreach (var change in changes)
                    change.ErpProductId = local.Id;
                await storage.InsertErpProductChangeLogsAsync(changes);
                return UpsertResult.Updated;
            }

            return UpsertResult.Unchanged;
        }

        private async Task<ErpProductDto?> ResolveRemoteProductAsync(ErpProduct local, CancellationToken ct)
        {
            if (!string.IsNullOrWhiteSpace(local.ErpProductId)
                && !local.ErpProductId.StartsWith("XLS-", StringComparison.OrdinalIgnoreCase))
            {
                var byId = await GetJsonAsync<ErpProductDto[]>(
                    $"getProductByID/{Uri.EscapeDataString(local.ErpProductId)}", ct);
                var match = byId?.FirstOrDefault(p =>
                    string.Equals(p.Id?.Trim(), local.ErpProductId, StringComparison.OrdinalIgnoreCase))
                    ?? byId?.FirstOrDefault();
                if (match != null)
                    return match;
            }

            if (!string.IsNullOrWhiteSpace(local.Ean))
            {
                // Ne pas résoudre par EAN si le produit local est un pack :
                // l'EAN appartient souvent à la fiche "unité" (même ref).
                if (!IsPackProductName(local.Name, local.Name2))
                {
                    var byBarcode = await GetJsonAsync<ErpProductDto[]>(
                        $"getProductStockByBarcode/{Uri.EscapeDataString(local.Ean)}", ct);
                    if (byBarcode is { Length: > 0 })
                    {
                        var barcodeMatch = PickBestRemoteMatch(local, byBarcode);
                        if (barcodeMatch != null)
                            return barcodeMatch;
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(local.Reference))
            {
                var searchKeys = BuildReferenceSearchKeys(local.Reference);
                foreach (var searchPrefix in searchKeys)
                {
                    var byRef = await GetJsonAsync<ErpProductDto[]>(
                        $"getProductsByReference/{Uri.EscapeDataString(searchPrefix)}", ct);
                    if (byRef == null || byRef.Length == 0)
                        continue;

                    var matches = byRef
                        .Where(p => ReferencesMatch(local.Reference!, p.Reference))
                        .ToList();
                    if (matches.Count == 0 && byRef.Length == 1)
                        matches.Add(byRef[0]);

                    var best = PickBestRemoteMatch(local, matches);
                    if (best != null)
                        return best;
                }
            }

            return null;
        }

        /// <summary>
        /// Choisit la bonne fiche ERP quand plusieurs partagent la même référence
        /// (ex. vente à l'unité vs verpakking 50).
        /// </summary>
        private static ErpProductDto? PickBestRemoteMatch(ErpProduct local, IReadOnlyList<ErpProductDto> candidates)
        {
            if (candidates.Count == 0)
                return null;
            if (candidates.Count == 1)
                return candidates[0];

            IEnumerable<ErpProductDto> pool = candidates;

            if (!string.IsNullOrWhiteSpace(local.ErpProductId)
                && !local.ErpProductId.StartsWith("XLS-", StringComparison.OrdinalIgnoreCase))
            {
                var byId = candidates
                    .Where(p => string.Equals(p.Id?.Trim(), local.ErpProductId, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                if (byId.Count == 1)
                    return byId[0];
                if (byId.Count > 1)
                    pool = byId;
            }

            if (!string.IsNullOrWhiteSpace(local.Ean))
            {
                var byEan = pool
                    .Where(p => string.Equals(p.Ean?.Trim(), local.Ean, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                if (byEan.Count == 1)
                    return byEan[0];
                if (byEan.Count > 1)
                    pool = byEan;
            }

            var localIsPack = IsPackProductName(local.Name, local.Name2);
            var packMatches = pool
                .Where(p => IsPackProductName(p.Name, p.Name2) == localIsPack)
                .ToList();
            if (packMatches.Count == 1)
                return packMatches[0];
            if (packMatches.Count > 1)
                pool = packMatches;

            if (local.UnitPrice.HasValue)
            {
                var withPrice = pool
                    .Select(p => new { Dto = p, Price = ParseDecimal(p.UnitPrice) })
                    .Where(x => x.Price.HasValue)
                    .OrderBy(x => Math.Abs(x.Price!.Value - local.UnitPrice.Value))
                    .ToList();
                if (withPrice.Count > 0)
                {
                    var best = withPrice[0];
                    var tolerance = Math.Max(0.05m, local.UnitPrice.Value * 0.15m);
                    if (Math.Abs(best.Price!.Value - local.UnitPrice.Value) <= tolerance)
                        return best.Dto;
                }
            }

            // Ambigu : ne pas prendre le premier au hasard (évite 66.97 ↔ 2.10).
            return null;
        }

        private static bool IsPackProductName(string? name, string? name2)
        {
            var text = $"{name} {name2}";
            if (string.IsNullOrWhiteSpace(text))
                return false;

            return System.Text.RegularExpressions.Regex.IsMatch(
                text,
                @"verpakking|\bpack\b|\bbo[iî]te\b|\bcartouche\b|\b\d+\s*st\.?\b|\bx\s*\d+\b|\b\d+\s*pi[eè]ces?\b",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
                | System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        }

        /// <summary>
        /// Préfixes marque collés devant la référence ERP (FF Group / Benman).
        /// </summary>
        private static readonly string[] ReferenceBrandPrefixes =
        {
            "Benman", "FF-Group", "FF Group", "FFGroup"
        };

        private static IEnumerable<string> BuildReferenceSearchKeys(string reference)
        {
            var list = new List<string>();
            var raw = reference.Trim();
            var core = StripBrandPrefix(raw);

            void Add(string? value)
            {
                if (string.IsNullOrWhiteSpace(value))
                    return;
                var key = value.Contains('/') ? value.Split('/')[0].Trim() : value.Trim();
                if (string.IsNullOrWhiteSpace(key))
                    return;
                if (!list.Any(x => string.Equals(x, key, StringComparison.OrdinalIgnoreCase)))
                    list.Add(key);
            }

            Add(raw);
            Add(core);
            foreach (var prefix in ReferenceBrandPrefixes)
                Add($"{prefix} {core}");

            return list;
        }

        private static string StripBrandPrefix(string reference)
        {
            var value = reference.Trim();
            foreach (var prefix in ReferenceBrandPrefixes.OrderByDescending(p => p.Length))
            {
                if (value.StartsWith(prefix + " ", StringComparison.OrdinalIgnoreCase))
                    return value[(prefix.Length + 1)..].Trim();
                if (value.StartsWith(prefix + "-", StringComparison.OrdinalIgnoreCase))
                    return value[(prefix.Length + 1)..].Trim();
            }

            return value;
        }

        private static bool ReferencesMatch(string localRef, string? erpRef)
        {
            if (string.IsNullOrWhiteSpace(erpRef))
                return false;

            var a = localRef.Trim();
            var b = erpRef.Trim();
            if (string.Equals(a, b, StringComparison.OrdinalIgnoreCase))
                return true;

            var aCore = StripBrandPrefix(a);
            var bCore = StripBrandPrefix(b);
            if (string.Equals(aCore, bCore, StringComparison.OrdinalIgnoreCase))
                return true;

            if (b.EndsWith(" " + aCore, StringComparison.OrdinalIgnoreCase)
                || b.EndsWith("-" + aCore, StringComparison.OrdinalIgnoreCase))
                return true;

            return false;
        }

        private static async Task<ErpProduct?> FindLocalProductForRemoteAsync(
            IStorageBroker storage,
            ErpProduct mapped,
            CancellationToken ct)
        {
            var byErpId = await storage.SelectAllErpProducts()
                .FirstOrDefaultAsync(p => p.ErpProductId == mapped.ErpProductId, ct);
            if (byErpId != null)
                return byErpId;

            if (!string.IsNullOrWhiteSpace(mapped.Ean))
            {
                var byEan = await storage.SelectAllErpProducts()
                    .Where(p => p.Ean == mapped.Ean)
                    .ToListAsync(ct);
                if (byEan.Count == 1)
                    return byEan[0];
                if (byEan.Count > 1)
                {
                    return PickBestLocalForMapped(byEan, mapped)
                           ?? byEan.FirstOrDefault(p =>
                               string.Equals(p.ErpProductId, mapped.ErpProductId, StringComparison.OrdinalIgnoreCase));
                }
            }

            if (string.IsNullOrWhiteSpace(mapped.Reference))
                return null;

            var byRef = await storage.SelectAllErpProducts()
                .Where(p => p.Reference == mapped.Reference)
                .ToListAsync(ct);
            if (byRef.Count == 0)
                return null;
            if (byRef.Count == 1)
                return byRef[0];

            // Plusieurs locaux avec la même ref (unité + pack) : ne fusionner que si on départage.
            return PickBestLocalForMapped(byRef, mapped);
        }

        private static ErpProduct? PickBestLocalForMapped(IReadOnlyList<ErpProduct> candidates, ErpProduct mapped)
        {
            if (candidates.Count == 0)
                return null;
            if (candidates.Count == 1)
                return candidates[0];

            IEnumerable<ErpProduct> pool = candidates;
            var mappedIsPack = IsPackProductName(mapped.Name, mapped.Name2);
            var packMatches = candidates
                .Where(p => IsPackProductName(p.Name, p.Name2) == mappedIsPack)
                .ToList();
            if (packMatches.Count == 1)
                return packMatches[0];
            if (packMatches.Count > 1)
                pool = packMatches;

            if (mapped.UnitPrice.HasValue)
            {
                var nearest = pool
                    .Where(p => p.UnitPrice.HasValue)
                    .OrderBy(p => Math.Abs(p.UnitPrice!.Value - mapped.UnitPrice.Value))
                    .FirstOrDefault();
                if (nearest != null)
                {
                    var tolerance = Math.Max(0.05m, mapped.UnitPrice.Value * 0.15m);
                    if (Math.Abs(nearest.UnitPrice!.Value - mapped.UnitPrice.Value) <= tolerance)
                        return nearest;
                }
            }

            return null;
        }

        private Task<HashSet<string>> CollectAllProductIdsAsync(CancellationToken ct) =>
            CollectProductIdsAsync(filter: null, ct);

        private async Task<HashSet<string>> CollectProductIdsAsync(ErpCatalogSyncFilter? filter, CancellationToken ct)
        {
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var brand = filter?.Brand?.Trim();

            if (!string.IsNullOrWhiteSpace(filter?.SubTypeId))
            {
                var productsBySubType = await GetJsonAsync<ErpProductDto[]>(
                    $"getProductsBySubType/{Uri.EscapeDataString(filter.SubTypeId)}/{Uri.EscapeDataString(_options.CustomerId)}",
                    ct) ?? Array.Empty<ErpProductDto>();
                AddMatchingProductIds(ids, productsBySubType, brand);
                return ids;
            }

            if (!string.IsNullOrWhiteSpace(filter?.TypeId))
            {
                await CollectProductsForTypeAsync(ids, filter.TypeId, brand, ct);
                return ids;
            }

            IEnumerable<ErpCatalogItemDto> mainTypes;
            if (!string.IsNullOrWhiteSpace(filter?.MainTypeId))
            {
                mainTypes = new[] { new ErpCatalogItemDto { Id = filter.MainTypeId.Trim() } };
            }
            else
            {
                mainTypes = await GetJsonAsync<ErpCatalogItemDto[]>("getProductMainTypes", ct)
                            ?? Array.Empty<ErpCatalogItemDto>();
            }

            foreach (var mainType in mainTypes)
            {
                if (string.IsNullOrWhiteSpace(mainType.Id))
                    continue;

                var types = await GetJsonAsync<ErpCatalogItemDto[]>(
                    $"getProductTypesByMainType/{Uri.EscapeDataString(mainType.Id)}", ct)
                    ?? Array.Empty<ErpCatalogItemDto>();

                foreach (var type in types)
                {
                    if (string.IsNullOrWhiteSpace(type.Id))
                        continue;

                    await CollectProductsForTypeAsync(ids, type.Id, brand, ct);
                }
            }

            return ids;
        }

        private async Task CollectProductsForTypeAsync(
            HashSet<string> ids,
            string typeId,
            string? brand,
            CancellationToken ct)
        {
            var productsByType = await GetJsonAsync<ErpProductDto[]>(
                $"getProductsByType/{Uri.EscapeDataString(typeId)}/{Uri.EscapeDataString(_options.CustomerId)}",
                ct) ?? Array.Empty<ErpProductDto>();
            AddMatchingProductIds(ids, productsByType, brand);

            var subTypes = await GetJsonAsync<ErpCatalogItemDto[]>(
                $"getProductSubTypesByProductType/{Uri.EscapeDataString(typeId)}", ct)
                ?? Array.Empty<ErpCatalogItemDto>();

            foreach (var subType in subTypes)
            {
                if (string.IsNullOrWhiteSpace(subType.Id))
                    continue;

                var productsBySubType = await GetJsonAsync<ErpProductDto[]>(
                    $"getProductsBySubType/{Uri.EscapeDataString(subType.Id)}/{Uri.EscapeDataString(_options.CustomerId)}",
                    ct) ?? Array.Empty<ErpProductDto>();
                AddMatchingProductIds(ids, productsBySubType, brand);
            }
        }

        private static void AddMatchingProductIds(
            HashSet<string> ids,
            IEnumerable<ErpProductDto> products,
            string? brandFilter)
        {
            foreach (var p in products)
            {
                if (string.IsNullOrWhiteSpace(p.Id))
                    continue;
                if (!MatchesBrandFilter(p, brandFilter))
                    continue;
                ids.Add(p.Id.Trim());
            }
        }

        private static bool MatchesBrandFilter(ErpProductDto product, string? brandFilter)
        {
            if (string.IsNullOrWhiteSpace(brandFilter))
                return true;

            return !string.IsNullOrWhiteSpace(product.Brand)
                   && string.Equals(product.Brand.Trim(), brandFilter.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        private async Task<UpsertResult> UpsertProductByErpIdAsync(
            IStorageBroker storage,
            string erpProductId,
            string jobId,
            bool preserveExcelFields,
            CancellationToken ct)
        {
            var remoteList = await GetJsonAsync<ErpProductDto[]>(
                $"getProductByID/{Uri.EscapeDataString(erpProductId)}", ct);

            var remote = remoteList?.FirstOrDefault(p =>
                string.Equals(p.Id?.Trim(), erpProductId, StringComparison.OrdinalIgnoreCase))
                ?? remoteList?.FirstOrDefault();

            if (remote == null || string.IsNullOrWhiteSpace(remote.Id))
                throw new InvalidOperationException($"Product {erpProductId} not found on ERP webservice");

            var mapped = MapDto(remote);
            mapped.LastSyncAt = DateTime.UtcNow;

            var existing = await FindLocalProductForRemoteAsync(storage, mapped, ct);

            if (existing == null)
            {
                mapped.CreatedAt = DateTime.UtcNow;
                mapped.UpdatedAt = DateTime.UtcNow;
                mapped.DataSource = "Erp";
                mapped.FromExcel = false;
                var inserted = await storage.InsertErpProductAsync(mapped);
                await storage.InsertErpProductChangeLogAsync(new ErpProductChangeLog
                {
                    ErpProductId = inserted.Id,
                    ChangeType = "Created",
                    FieldName = "*",
                    OldValue = null,
                    NewValue = mapped.Name ?? mapped.Reference ?? mapped.ErpProductId,
                    DetectedAt = DateTime.UtcNow,
                    SyncJobId = jobId,
                    IsRead = false
                });
                return UpsertResult.Created;
            }

            var protect = preserveExcelFields && existing.FromExcel;
            var changes = DetectChanges(existing, mapped, jobId, protect);
            ApplyMappedValues(existing, mapped, protect);

            if (existing.ErpProductId.StartsWith("XLS-", StringComparison.OrdinalIgnoreCase)
                || !string.Equals(existing.ErpProductId, mapped.ErpProductId, StringComparison.OrdinalIgnoreCase))
            {
                var taken = await storage.SelectAllErpProducts()
                    .AnyAsync(p => p.ErpProductId == mapped.ErpProductId && p.Id != existing.Id, ct);
                if (!taken)
                    existing.ErpProductId = mapped.ErpProductId;
            }

            existing.DataSource = existing.FromExcel ? "Merged" : "Erp";
            existing.UpdatedAt = DateTime.UtcNow;
            existing.LastSyncAt = mapped.LastSyncAt;
            await storage.UpdateErpProductAsync(existing);

            if (changes.Count > 0)
            {
                foreach (var change in changes)
                    change.ErpProductId = existing.Id;
                await storage.InsertErpProductChangeLogsAsync(changes);
                return UpsertResult.Updated;
            }

            return UpsertResult.Unchanged;
        }

        private List<ErpProductChangeLog> DetectChanges(
            ErpProduct existing,
            ErpProduct incoming,
            string jobId,
            bool preserveExcelFields)
        {
            var changes = new List<ErpProductChangeLog>();
            var now = DateTime.UtcNow;

            foreach (var field in TrackedFields)
            {
                if (preserveExcelFields
                    && ExcelProtectedFields.Contains(field)
                    && !IsEmptyField(existing, field))
                {
                    continue;
                }

                if (FieldValuesEqual(existing, incoming, field))
                    continue;

                // Ancien bug Excel : prix d'achat stocké dans PriceHT.
                // Ne pas logger un faux "Prix vente HT" (achat Excel → vente HT ERP).
                if (field == nameof(ErpProduct.PriceHT)
                    && IsExcelCostMistakenlyStoredAsPriceHt(existing, incoming))
                {
                    continue;
                }

                var oldVal = GetFieldValue(existing, field);
                var newVal = GetFieldValue(incoming, field);

                var changeType = field switch
                {
                    nameof(ErpProduct.UnitPrice) or nameof(ErpProduct.PriceHT)
                        or nameof(ErpProduct.CPrice) or nameof(ErpProduct.RPrice)
                        or nameof(ErpProduct.DiscountPrice) or nameof(ErpProduct.PromoPrice)
                        => "PriceChanged",
                    nameof(ErpProduct.StockQuantity) => "StockChanged",
                    _ => "Updated"
                };

                changes.Add(new ErpProductChangeLog
                {
                    ChangeType = changeType,
                    FieldName = field,
                    OldValue = Truncate(oldVal, 2048),
                    NewValue = Truncate(newVal, 2048),
                    DetectedAt = now,
                    SyncJobId = jobId,
                    IsRead = false
                });
            }

            return changes;
        }

        private static bool IsEmptyField(ErpProduct product, string fieldName) =>
            string.IsNullOrWhiteSpace(GetFieldValue(product, fieldName));

        /// <summary>
        /// True si PriceHT local vaut encore le prix d'achat Excel (CPrice)
        /// alors que l'ERP envoie un vrai prix de vente HT différent.
        /// </summary>
        private static bool IsExcelCostMistakenlyStoredAsPriceHt(ErpProduct existing, ErpProduct incoming) =>
            existing.FromExcel
            && existing.PriceHT.HasValue
            && existing.CPrice.HasValue
            && existing.PriceHT == existing.CPrice
            && incoming.PriceHT.HasValue
            && incoming.PriceHT != existing.CPrice;

        private static bool FieldValuesEqual(ErpProduct existing, ErpProduct incoming, string fieldName) =>
            fieldName switch
            {
                nameof(ErpProduct.UnitPrice) => existing.UnitPrice == incoming.UnitPrice,
                nameof(ErpProduct.PriceHT) => existing.PriceHT == incoming.PriceHT,
                nameof(ErpProduct.CPrice) => existing.CPrice == incoming.CPrice,
                nameof(ErpProduct.RPrice) => existing.RPrice == incoming.RPrice,
                nameof(ErpProduct.DiscountPrice) => OptionalPriceValuesEqual(existing.DiscountPrice, incoming.DiscountPrice),
                nameof(ErpProduct.StockQuantity) => existing.StockQuantity == incoming.StockQuantity,
                nameof(ErpProduct.PromoPrice) => OptionalPriceValuesEqual(existing.PromoPrice, incoming.PromoPrice),
                nameof(ErpProduct.PromoActive) => existing.PromoActive == incoming.PromoActive,
                nameof(ErpProduct.Archived) => existing.Archived == incoming.Archived,
                _ => string.Equals(
                    GetFieldValue(existing, fieldName),
                    GetFieldValue(incoming, fieldName),
                    StringComparison.Ordinal)
            };

        private static bool DecimalValuesEquivalent(string? oldValue, string? newValue)
        {
            if (string.Equals(oldValue, newValue, StringComparison.Ordinal))
                return true;

            var oldDecimal = ParseDecimal(oldValue);
            var newDecimal = ParseDecimal(newValue);
            if (!oldDecimal.HasValue && !newDecimal.HasValue)
                return true;
            if (IsEmptyOrZero(oldDecimal) && IsEmptyOrZero(newDecimal))
                return true;
            if (oldDecimal.HasValue != newDecimal.HasValue)
                return false;

            return oldDecimal.Value == newDecimal.Value;
        }

        /// <summary>
        /// Prix promo / remisé : null, vide ou 0 = pas de promo (équivalents).
        /// </summary>
        private static bool OptionalPriceValuesEqual(decimal? left, decimal? right) =>
            left == right || (IsEmptyOrZero(left) && IsEmptyOrZero(right));

        private static bool IsEmptyOrZero(decimal? value) =>
            !value.HasValue || value.Value == 0m;

        /// <summary>
        /// Faux positif historique : PromoPrice / DiscountPrice — → 0.
        /// </summary>
        private static bool IsEmptyOrZeroDecimalFalsePositive(ErpProductChangeLog change)
        {
            if (change.FieldName is not (
                nameof(ErpProduct.PromoPrice) or nameof(ErpProduct.DiscountPrice)))
            {
                return false;
            }

            return IsEmptyOrZero(ParseDecimal(change.OldValue))
                && IsEmptyOrZero(ParseDecimal(change.NewValue));
        }

        private static string? GetFieldValue(ErpProduct product, string fieldName) =>
            fieldName switch
            {
                nameof(ErpProduct.ErpProductId) => product.ErpProductId,
                nameof(ErpProduct.Name) => product.Name,
                nameof(ErpProduct.Name2) => product.Name2,
                nameof(ErpProduct.Reference) => product.Reference,
                nameof(ErpProduct.Ean) => product.Ean,
                nameof(ErpProduct.Brand) => product.Brand,
                nameof(ErpProduct.UnitPrice) => FormatDecimal(product.UnitPrice),
                nameof(ErpProduct.PriceHT) => FormatDecimal(product.PriceHT),
                nameof(ErpProduct.CPrice) => FormatDecimal(product.CPrice),
                nameof(ErpProduct.RPrice) => FormatDecimal(product.RPrice),
                nameof(ErpProduct.DiscountPrice) => FormatDecimal(product.DiscountPrice),
                nameof(ErpProduct.StockQuantity) => FormatDecimal(product.StockQuantity),
                nameof(ErpProduct.Comment) => product.Comment,
                nameof(ErpProduct.TypeName) => product.TypeName,
                nameof(ErpProduct.SubTypeName) => product.SubTypeName,
                nameof(ErpProduct.MainTypeName) => product.MainTypeName,
                nameof(ErpProduct.PromoActive) => product.PromoActive.ToString(),
                nameof(ErpProduct.PromoPrice) => FormatDecimal(product.PromoPrice),
                nameof(ErpProduct.Archived) => product.Archived?.ToString(),
                _ => null
            };

        private static void ApplyMappedValues(ErpProduct target, ErpProduct source, bool preserveExcelFields)
        {
            void SetString(Action<string?> setter, string? current, string? incoming, string field)
            {
                if (preserveExcelFields && ExcelProtectedFields.Contains(field) && !string.IsNullOrWhiteSpace(current))
                    return;
                if (incoming != null || !preserveExcelFields)
                    setter(incoming ?? current);
            }

            void SetDecimal(Action<decimal?> setter, decimal? current, decimal? incoming, string field)
            {
                if (preserveExcelFields && ExcelProtectedFields.Contains(field) && current.HasValue)
                    return;
                if (incoming.HasValue || !preserveExcelFields)
                    setter(incoming ?? current);
            }

            SetString(v => target.Name = v, target.Name, source.Name, nameof(ErpProduct.Name));
            target.Name2 = source.Name2 ?? target.Name2;
            SetString(v => target.Reference = v, target.Reference, source.Reference, nameof(ErpProduct.Reference));
            SetString(v => target.Ean = v, target.Ean, source.Ean, nameof(ErpProduct.Ean));
            SetString(v => target.Brand = v, target.Brand, source.Brand, nameof(ErpProduct.Brand));
            target.Manufacturer = source.Manufacturer ?? target.Manufacturer;
            target.Model = source.Model ?? target.Model;
            SetString(v => target.Comment = v, target.Comment, source.Comment, nameof(ErpProduct.Comment));
            target.Link = source.Link ?? target.Link;
            target.PicName = source.PicName ?? target.PicName;

            SetDecimal(v => target.PriceHT = v, target.PriceHT, source.PriceHT, nameof(ErpProduct.PriceHT));
            SetDecimal(v => target.UnitPrice = v, target.UnitPrice, source.UnitPrice, nameof(ErpProduct.UnitPrice));
            SetDecimal(v => target.CPrice = v, target.CPrice, source.CPrice, nameof(ErpProduct.CPrice));
            SetDecimal(v => target.RPrice = v, target.RPrice, source.RPrice, nameof(ErpProduct.RPrice));

            // Champs ERP-only : toujours mis à jour quand présents
            target.VatIncluded = source.VatIncluded;
            target.TypeVatPerc = source.TypeVatPerc ?? target.TypeVatPerc;
            target.DiscountPerc = source.DiscountPerc ?? target.DiscountPerc;
            target.DiscountPrice = source.DiscountPrice ?? target.DiscountPrice;
            target.ProductDiscountPerc = source.ProductDiscountPerc ?? target.ProductDiscountPerc;
            target.TypeDiscountPerc = source.TypeDiscountPerc ?? target.TypeDiscountPerc;
            target.PromoActive = source.PromoActive;
            target.PromoPrice = source.PromoPrice ?? target.PromoPrice;
            target.PromoStartDate = source.PromoStartDate ?? target.PromoStartDate;
            target.PromoEndDate = source.PromoEndDate ?? target.PromoEndDate;
            target.StockQuantity = source.StockQuantity ?? target.StockQuantity;
            target.StockDate = source.StockDate ?? target.StockDate;
            target.Quantity = source.Quantity ?? target.Quantity;
            target.PerUnit = source.PerUnit ?? target.PerUnit;
            target.PieceID = source.PieceID ?? target.PieceID;
            target.Weight = source.Weight ?? target.Weight;
            target.Height = source.Height ?? target.Height;
            target.Width = source.Width ?? target.Width;
            target.Depth = source.Depth ?? target.Depth;
            target.MainTypeID = source.MainTypeID ?? target.MainTypeID;
            target.MainTypeName = source.MainTypeName ?? target.MainTypeName;
            target.MainSubTypeID = source.MainSubTypeID ?? target.MainSubTypeID;
            target.MainSubTypeName = source.MainSubTypeName ?? target.MainSubTypeName;
            target.TypeID = source.TypeID ?? target.TypeID;
            target.TypeName = source.TypeName ?? target.TypeName;
            target.SubTypeID = source.SubTypeID ?? target.SubTypeID;
            target.SubTypeName = source.SubTypeName ?? target.SubTypeName;
            target.SubProductID = source.SubProductID ?? target.SubProductID;
            target.Label = source.Label ?? target.Label;
            target.ColorCode = source.ColorCode ?? target.ColorCode;
            target.Archived = source.Archived ?? target.Archived;
        }

        private static ErpProduct MapDto(ErpProductDto dto) => new()
        {
            ErpProductId = dto.Id!.Trim(),
            Name = NullIfEmpty(dto.Name),
            Name2 = NullIfEmpty(dto.Name2),
            Reference = NullIfEmpty(dto.Reference),
            Ean = NullIfEmpty(dto.Ean),
            Brand = NullIfEmpty(dto.Brand),
            Manufacturer = NullIfEmpty(dto.Manufacturer),
            Model = NullIfEmpty(dto.Model),
            Comment = NullIfEmpty(dto.Comment),
            Link = NullIfEmpty(dto.Link),
            PicName = NullIfEmpty(dto.PicName),
            PriceHT = ParseDecimal(dto.PriceHT),
            UnitPrice = ParseDecimal(dto.UnitPrice),
            CPrice = ParseDecimal(dto.CPrice),
            RPrice = ParseDecimal(dto.RPrice),
            VatIncluded = ParseBool(dto.VatIncluded),
            TypeVatPerc = ParseDecimal(dto.TypeVatPerc),
            DiscountPerc = ParseDecimal(dto.DiscountPerc),
            DiscountPrice = ParseDecimal(dto.DiscountPrice),
            ProductDiscountPerc = ParseDecimal(dto.ProductDiscountPerc),
            TypeDiscountPerc = ParseDecimal(dto.TypeDiscountPerc),
            PromoActive = ParseBool(dto.PromoActive),
            PromoPrice = ParseDecimal(dto.PromoPrice),
            PromoStartDate = ParseDate(dto.PromoStartDate),
            PromoEndDate = ParseDate(dto.PromoEndDate),
            StockQuantity = ParseDecimal(dto.StockQuantity),
            StockDate = ParseDate(dto.StockDate),
            Quantity = ParseDecimal(dto.Quantity),
            PerUnit = NullIfEmpty(dto.PerUnit),
            PieceID = NullIfEmpty(dto.PieceID),
            Weight = ParseDecimal(dto.Weight),
            Height = ParseDecimal(dto.Height),
            Width = ParseDecimal(dto.Width),
            Depth = ParseDecimal(dto.Depth),
            MainTypeID = NullIfEmpty(dto.MainTypeID),
            MainTypeName = NullIfEmpty(dto.MainTypeName),
            MainSubTypeID = NullIfEmpty(dto.MainSubTypeID),
            MainSubTypeName = NullIfEmpty(dto.MainSubTypeName),
            TypeID = NullIfEmpty(dto.TypeID),
            TypeName = NullIfEmpty(dto.TypeName),
            SubTypeID = NullIfEmpty(dto.SubTypeID),
            SubTypeName = NullIfEmpty(dto.SubTypeName),
            SubProductID = NullIfEmpty(dto.SubProductID),
            Label = NullIfEmpty(dto.Label),
            ColorCode = NullIfEmpty(dto.ColorCode),
            Archived = ParseNullableBool(dto.Archived ?? dto.Achived)
        };

        private async Task<ErpSyncLog> FinalizeSyncLogAsync(
            IStorageBroker storage,
            ErpSyncLog syncLog,
            string jobId,
            object details,
            CancellationToken ct)
        {
            syncLog.TotalChanges = await storage.SelectAllErpProductChangeLogs()
                .CountAsync(c => c.SyncJobId == jobId, ct);

            syncLog.Status = syncLog.FailedProducts > 0 &&
                             (syncLog.NewProducts + syncLog.UpdatedProducts) > 0
                ? "PartiallyCompleted"
                : syncLog.FailedProducts > 0 && syncLog.NewProducts + syncLog.UpdatedProducts == 0
                    ? "Failed"
                    : "Completed";
            syncLog.CompletedAt = DateTime.UtcNow;
            syncLog.Details = JsonSerializer.Serialize(details);
            await storage.UpdateErpSyncLogAsync(syncLog);
            return syncLog;
        }

        private async Task<ErpSyncLog> FailSyncLogAsync(
            IStorageBroker storage,
            ErpSyncLog syncLog,
            string jobId,
            Exception ex)
        {
            _logger.LogError(ex, "ERP sync {JobId} failed", jobId);
            syncLog.Status = "Failed";
            syncLog.ErrorMessage = ex.Message;
            syncLog.CompletedAt = DateTime.UtcNow;
            await storage.UpdateErpSyncLogAsync(syncLog);
            return syncLog;
        }

        private async Task<ErpProductDto?> FetchProductByIdAsync(string erpProductId, CancellationToken ct)
        {
            // Essayer les deux casse d'URI utilisées par le webservice
            var paths = new[]
            {
                $"getProductByID/{Uri.EscapeDataString(erpProductId)}",
                $"GetProductByID/{Uri.EscapeDataString(erpProductId)}"
            };

            foreach (var path in paths)
            {
                var list = await GetJsonAsync<ErpProductDto[]>(path, ct, throwOnFinalFailure: false);
                var match = list?.FirstOrDefault(p =>
                    string.Equals(p.Id?.Trim(), erpProductId, StringComparison.OrdinalIgnoreCase))
                    ?? list?.FirstOrDefault();
                if (match != null && !string.IsNullOrWhiteSpace(match.Id))
                    return match;
            }

            return null;
        }

        private async Task<T?> GetJsonAsync<T>(string relativePath, CancellationToken ct, bool throwOnFinalFailure = false)
        {
            var baseUrl = (_options.BaseUrl ?? string.Empty).TrimEnd('/');
            var url = $"{baseUrl}/{relativePath.TrimStart('/')}";

            Exception? lastError = null;
            for (var attempt = 1; attempt <= Math.Max(1, _options.RetryCount); attempt++)
            {
                try
                {
                    using var request = new HttpRequestMessage(HttpMethod.Get, url);
                    using var response = await _httpClient.SendAsync(request, ct);
                    if (!response.IsSuccessStatusCode)
                    {
                        lastError = new HttpRequestException($"HTTP {(int)response.StatusCode} for {url}");
                        _logger.LogWarning("ERP HTTP {Status} {Url} (attempt {Attempt})",
                            (int)response.StatusCode, url, attempt);
                        continue;
                    }

                    var content = await response.Content.ReadAsStringAsync(ct);
                    if (string.IsNullOrWhiteSpace(content) || content == "null")
                        return default;

                    return JsonSerializer.Deserialize<T>(content, JsonOptions);
                }
                catch (Exception ex) when (attempt < Math.Max(1, _options.RetryCount))
                {
                    lastError = ex;
                    _logger.LogWarning(ex, "ERP request failed {Url} (attempt {Attempt})", url, attempt);
                    await Task.Delay(TimeSpan.FromSeconds(attempt), ct);
                }
                catch (Exception ex)
                {
                    lastError = ex;
                }
            }

            if (lastError != null)
                _logger.LogWarning(lastError, "ERP request ultimately failed for {Url}", url);

            if (throwOnFinalFailure && lastError != null)
                throw lastError;

            return default;
        }

        private static string? NullIfEmpty(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private static string? FormatDecimal(decimal? value) =>
            value?.ToString("G29", CultureInfo.InvariantCulture);

        private static string? Truncate(string? value, int max) =>
            value == null || value.Length <= max ? value : value[..max];

        private static decimal? ParseDecimal(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;
            value = value.Replace(",", ".").Trim();
            return decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var result)
                ? result
                : null;
        }

        private static bool ParseBool(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;
            value = value.Trim();
            return value == "1"
                   || value.Equals("true", StringComparison.OrdinalIgnoreCase)
                   || value.Equals("yes", StringComparison.OrdinalIgnoreCase);
        }

        private static bool? ParseNullableBool(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;
            return ParseBool(value);
        }

        private static DateTime? ParseDate(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;
            if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var dt))
                return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
            if (DateTime.TryParse(value, new CultureInfo("fr-BE"), DateTimeStyles.AssumeLocal, out dt))
                return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
            return null;
        }

        private enum UpsertResult
        {
            Created,
            Updated,
            Unchanged
        }
    }
}
