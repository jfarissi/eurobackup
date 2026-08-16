using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Models.Catalog;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Backup.Web.Api.Server.Services.AutoParts
{
    public class KTypeEnrichmentService : IKTypeEnrichmentService
    {
        private readonly IStorageBroker storage;
        private readonly IKTypeSyncProgressStore progressStore;
        private readonly IServiceScopeFactory scopeFactory;
        private readonly IOptions<RapidApiOptions> rapidOptions;
        private readonly ILogger<KTypeEnrichmentService> logger;

        public KTypeEnrichmentService(
            IStorageBroker storage,
            IKTypeSyncProgressStore progressStore,
            IServiceScopeFactory scopeFactory,
            IOptions<RapidApiOptions> rapidOptions,
            ILogger<KTypeEnrichmentService> logger)
        {
            this.storage = storage;
            this.progressStore = progressStore;
            this.scopeFactory = scopeFactory;
            this.rapidOptions = rapidOptions;
            this.logger = logger;
        }

        public async Task<bool> ExistsInCatalogAsync(string kType, CancellationToken ct = default)
        {
            _ = ct;
            return await storage.ErpProductVehicleKTypeExistsAsync(kType);
        }

        public async Task<KTypeEnrichmentResult> EnrichIfMissingAsync(
            string kType, KTypeEnrichmentContext context, CancellationToken ct = default)
        {
            var k = (kType ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(k))
                return new KTypeEnrichmentResult(false, false, 0);

            if (await storage.ErpProductVehicleKTypeExistsAsync(k))
                return new KTypeEnrichmentResult(false, false, 0, "K-Type déjà en catalogue.");

            if (progressStore.IsRunning(k))
            {
                var running = progressStore.Get(k);
                return new KTypeEnrichmentResult(
                    true,
                    false,
                    0,
                    running?.Message ?? "Import catalogue en cours…",
                    SyncInProgress: true);
            }

            var queued = false;
            try
            {
                await storage.EnqueueErpKTypeEnrichmentAsync(new ErpKTypeEnrichmentQueue
                {
                    CompanyId = context.CompanyId,
                    KType = k,
                    Vin = context.Vin,
                    Make = context.Make,
                    Model = context.Model,
                    Year = context.Year,
                    EngineCode = context.EngineCode,
                    Source = string.IsNullOrWhiteSpace(context.Source) ? "VinLookup" : context.Source,
                    Status = "Pending"
                });
                queued = true;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Impossible d'enregistrer le K-Type {KType} en file", k);
            }

            var cfg = rapidOptions.Value;
            if (!cfg.EnableOnDemandKTypeSync || string.IsNullOrWhiteSpace(context.Make))
            {
                return new KTypeEnrichmentResult(
                    queued,
                    false,
                    0,
                    queued
                        ? "K-Type absent — enrichissement planifié (sync batch)."
                        : null);
            }

            return new KTypeEnrichmentResult(
                queued,
                false,
                0,
                "K-Type identifié — choisissez les catégories RapidAPI à importer.",
                NeedsCategorySelection: true);
        }

        public async Task<KTypeEnrichmentResult> StartOnDemandImportAsync(
            string kType,
            KTypeEnrichmentContext context,
            IReadOnlyList<int> categoryIds,
            CancellationToken ct = default)
        {
            _ = ct;
            var k = (kType ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(k))
                return new KTypeEnrichmentResult(false, false, 0, "K-Type requis.");

            var selected = (categoryIds ?? Array.Empty<int>()).Where(id => id > 0).Distinct().Take(20).ToList();
            if (selected.Count == 0)
                return new KTypeEnrichmentResult(false, false, 0, "Sélectionnez au moins une catégorie.");

            if (progressStore.IsRunning(k))
            {
                var running = progressStore.Get(k);
                return new KTypeEnrichmentResult(
                    true,
                    false,
                    0,
                    running?.Message ?? "Import catalogue en cours…",
                    SyncInProgress: true);
            }

            var cfg = rapidOptions.Value;
            if (!cfg.EnableOnDemandKTypeSync)
                return new KTypeEnrichmentResult(false, false, 0, "Import RapidAPI à la demande désactivé.");

            if (string.IsNullOrWhiteSpace(context.Make))
                return new KTypeEnrichmentResult(false, false, 0, "Marque requise pour l'import.");

            try
            {
                await storage.EnqueueErpKTypeEnrichmentAsync(new ErpKTypeEnrichmentQueue
                {
                    CompanyId = context.CompanyId,
                    KType = k,
                    Vin = context.Vin,
                    Make = context.Make,
                    Model = context.Model,
                    Year = context.Year,
                    EngineCode = context.EngineCode,
                    Source = string.IsNullOrWhiteSpace(context.Source) ? "VinLookup" : context.Source,
                    Status = "Pending"
                });
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Impossible d'enregistrer le K-Type {KType} en file", k);
            }

            var maxProducts = Math.Clamp(cfg.OnDemandMaxProducts * selected.Count, 1, 200);
            progressStore.Start(k, maxProducts, context.Make, context.Model);

            try
            {
                await storage.UpdateErpKTypeEnrichmentStatusAsync(k, "Syncing", null);
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Mise à jour statut Syncing ignorée pour {KType}", k);
            }

            _ = RunBackgroundSyncAsync(k, context, selected);

            return new KTypeEnrichmentResult(
                true,
                false,
                0,
                "Import catalogue RapidAPI en cours…",
                SyncInProgress: true);
        }

        private async Task RunBackgroundSyncAsync(
            string kType, KTypeEnrichmentContext context, IReadOnlyList<int>? categoryIds = null)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var sync = scope.ServiceProvider.GetRequiredService<IRapidApiKTypeSyncService>();
                var scopedStorage = scope.ServiceProvider.GetRequiredService<IStorageBroker>();

                var fuel = context.FuelType;
                if (string.IsNullOrWhiteSpace(fuel) && !string.IsNullOrWhiteSpace(context.Vin))
                {
                    var vinRow = await scopedStorage.SelectErpVinVehicleByVinAsync(context.Vin);
                    fuel = vinRow?.FuelType;
                }

                var imported = await sync.SyncKTypeAsync(
                    kType,
                    context.Make ?? string.Empty,
                    string.IsNullOrWhiteSpace(context.Model) ? (context.Make ?? "unknown") : context.Model,
                    context.Year,
                    categoryIds,
                    fuel,
                    CancellationToken.None);

                if (!string.IsNullOrWhiteSpace(fuel))
                {
                    try
                    {
                        await scopedStorage.FillMissingErpProductVehicleFuelAsync(kType, fuel);
                    }
                    catch (Exception ex)
                    {
                        logger.LogDebug(ex, "Remplissage FuelType ErpProductVehicles ignoré pour {KType}", kType);
                    }
                }

                if (imported > 0)
                {
                    progressStore.Complete(kType, imported);
                    try
                    {
                        await scopedStorage.UpdateErpKTypeEnrichmentStatusAsync(
                            kType,
                            "Done",
                            imported);
                    }
                    catch (Exception ex)
                    {
                        logger.LogDebug(ex, "Mise à jour statut Done ignorée pour {KType}", kType);
                    }

                    logger.LogInformation(
                        "K-Type {KType} enrichi en arrière-plan — {Count} produit(s)",
                        kType,
                        imported);
                }
                else
                {
                    if (progressStore.Get(kType)?.Status != KTypeSyncStatus.Failed)
                        progressStore.Fail(kType, "Aucun produit importé pour ce K-Type.");
                    try
                    {
                        await scopedStorage.UpdateErpKTypeEnrichmentStatusAsync(kType, "Pending", null);
                    }
                    catch (Exception ex)
                    {
                        logger.LogDebug(ex, "Mise à jour statut Pending ignorée pour {KType}", kType);
                    }
                }
            }
            catch (Exception ex)
            {
                progressStore.Fail(kType, "Échec import catalogue.");
                logger.LogWarning(ex, "Échec sync arrière-plan K-Type {KType}", kType);
                try
                {
                    using var scope = scopeFactory.CreateScope();
                    var scopedStorage = scope.ServiceProvider.GetRequiredService<IStorageBroker>();
                    await scopedStorage.UpdateErpKTypeEnrichmentStatusAsync(kType, "Pending", null);
                }
                catch
                {
                    // ignore
                }
            }
        }
    }
}
