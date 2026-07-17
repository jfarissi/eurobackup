using System;
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
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        /// <summary>Champs Excel — non écrasés s'ils sont déjà renseignés. Les prix sont toujours pris depuis l'ERP (avec historique PriceChanged).</summary>
        private static readonly HashSet<string> ExcelProtectedFields = new(StringComparer.Ordinal)
        {
            nameof(ErpProduct.Name),
            nameof(ErpProduct.Reference),
            nameof(ErpProduct.Ean),
            nameof(ErpProduct.Brand),
            nameof(ErpProduct.Comment)
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

        public async Task<ErpSyncLog> SyncAllProductsAsync(CancellationToken ct = default)
        {
            var mode = (_options.SyncMode ?? "LocalEnrich").Trim();
            if (mode.Equals("FullCatalog", StringComparison.OrdinalIgnoreCase))
                return await SyncFullCatalogAsync(ct);
            return await SyncLocalEnrichAsync(ct);
        }

        public async Task<ErpSyncLog> StartSyncAllAsync(CancellationToken ct = default)
        {
            using var scope = _scopeFactory.CreateScope();
            var storage = scope.ServiceProvider.GetRequiredService<IStorageBroker>();

            var running = await storage.SelectAllErpSyncLogs()
                .AsNoTracking()
                .Where(s => s.Status == "Running")
                .OrderByDescending(s => s.StartedAt)
                .FirstOrDefaultAsync(ct);
            if (running != null)
                return running;

            var jobId = Guid.NewGuid().ToString("N");
            var syncLog = await storage.InsertErpSyncLogAsync(new ErpSyncLog
            {
                JobId = jobId,
                Status = "Running",
                StartedAt = DateTime.UtcNow,
                Details = JsonSerializer.Serialize(new
                {
                    mode = (_options.SyncMode ?? "LocalEnrich").Trim(),
                    phase = "starting"
                })
            });

            _ = Task.Run(async () =>
            {
                try
                {
                    var mode = (_options.SyncMode ?? "LocalEnrich").Trim();
                    if (mode.Equals("FullCatalog", StringComparison.OrdinalIgnoreCase))
                        await SyncFullCatalogAsync(CancellationToken.None, jobId);
                    else
                        await SyncLocalEnrichAsync(CancellationToken.None, jobId);
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
            });

            return syncLog;
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

            var existing = await storage.SelectAllErpProducts()
                .FirstOrDefaultAsync(p =>
                    p.ErpProductId == mapped.ErpProductId
                    || (!string.IsNullOrWhiteSpace(mapped.Ean) && p.Ean == mapped.Ean)
                    || (!string.IsNullOrWhiteSpace(mapped.Reference) && p.Reference == mapped.Reference), ct);

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

                var productIds = await CollectAllProductIdsAsync(ct);
                syncLog.TotalProducts = productIds.Count;
                syncLog.ProcessedProducts = 0;
                syncLog.Details = JsonSerializer.Serialize(new { mode = "FullCatalog", phase = "upserting" });
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

                return await FinalizeSyncLogAsync(storage, syncLog, jobId, new
                {
                    mode = "FullCatalog",
                    productIdsCollected = productIds.Count,
                    processed
                }, ct);
            }
            catch (Exception ex)
            {
                return await FailSyncLogAsync(storage, syncLog, jobId, ex);
            }
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
                var byBarcode = await GetJsonAsync<ErpProductDto[]>(
                    $"getProductStockByBarcode/{Uri.EscapeDataString(local.Ean)}", ct);
                if (byBarcode is { Length: > 0 })
                    return byBarcode[0];
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

                    var match = byRef.FirstOrDefault(p => ReferencesMatch(local.Reference, p.Reference));
                    if (match != null)
                        return match;
                    if (byRef.Length == 1)
                        return byRef[0];
                }
            }

            return null;
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

        private async Task<HashSet<string>> CollectAllProductIdsAsync(CancellationToken ct)
        {
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var mainTypes = await GetJsonAsync<ErpCatalogItemDto[]>("getProductMainTypes", ct)
                            ?? Array.Empty<ErpCatalogItemDto>();

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

                    var productsByType = await GetJsonAsync<ErpProductDto[]>(
                        $"getProductsByType/{Uri.EscapeDataString(type.Id)}/{Uri.EscapeDataString(_options.CustomerId)}",
                        ct)
                        ?? Array.Empty<ErpProductDto>();

                    foreach (var p in productsByType)
                    {
                        if (!string.IsNullOrWhiteSpace(p.Id))
                            ids.Add(p.Id.Trim());
                    }

                    var subTypes = await GetJsonAsync<ErpCatalogItemDto[]>(
                        $"getProductSubTypesByProductType/{Uri.EscapeDataString(type.Id)}", ct)
                        ?? Array.Empty<ErpCatalogItemDto>();

                    foreach (var subType in subTypes)
                    {
                        if (string.IsNullOrWhiteSpace(subType.Id))
                            continue;

                        var productsBySubType = await GetJsonAsync<ErpProductDto[]>(
                            $"getProductsBySubType/{Uri.EscapeDataString(subType.Id)}/{Uri.EscapeDataString(_options.CustomerId)}",
                            ct)
                            ?? Array.Empty<ErpProductDto>();

                        foreach (var p in productsBySubType)
                        {
                            if (!string.IsNullOrWhiteSpace(p.Id))
                                ids.Add(p.Id.Trim());
                        }
                    }
                }
            }

            return ids;
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

            var existing = await storage.SelectAllErpProducts()
                .FirstOrDefaultAsync(p =>
                    p.ErpProductId == mapped.ErpProductId
                    || (!string.IsNullOrWhiteSpace(mapped.Ean) && p.Ean == mapped.Ean)
                    || (!string.IsNullOrWhiteSpace(mapped.Reference) && p.Reference == mapped.Reference), ct);

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

                var oldVal = GetFieldValue(existing, field);
                var newVal = GetFieldValue(incoming, field);
                if (string.Equals(oldVal, newVal, StringComparison.Ordinal))
                    continue;

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
            value?.ToString(CultureInfo.InvariantCulture);

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
