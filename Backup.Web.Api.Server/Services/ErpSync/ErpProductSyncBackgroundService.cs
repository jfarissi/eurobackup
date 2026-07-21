using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Brokers.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Backup.Web.Api.Server.Services.ErpSync
{
    public class ErpProductSyncBackgroundService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IOptionsMonitor<ErpSyncOptions> _options;
        private readonly ILogger<ErpProductSyncBackgroundService> _logger;

        /// <summary>Créneau (vendredi 18:00 UTC) déjà exécuté — évite une boucle de rattrapage.</summary>
        private DateTime? _lastCompletedSlotUtc;

        public ErpProductSyncBackgroundService(
            IServiceScopeFactory scopeFactory,
            IOptionsMonitor<ErpSyncOptions> options,
            ILogger<ErpProductSyncBackgroundService> logger)
        {
            _scopeFactory = scopeFactory;
            _options = options;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("ERP product sync background service started (weekly Friday evening)");

            // Jobs Running sans process actif (restart Docker) → fantômes qui bloquent StartSyncAll.
            await ClearOrphanedRunningJobsAsync(stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                var options = _options.CurrentValue;
                if (!options.Enabled)
                {
                    await DelaySafe(TimeSpan.FromMinutes(1), stoppingToken);
                    continue;
                }

                var slot = GetNextSlotUtc(
                    options.CronHour,
                    options.CronMinute,
                    options.CronDayOfWeek,
                    _lastCompletedSlotUtc);

                var delay = slot - DateTime.UtcNow;
                if (delay < TimeSpan.Zero)
                    delay = TimeSpan.Zero;

                var mode = string.IsNullOrWhiteSpace(options.ScheduledSyncMode)
                    ? "FullCatalog"
                    : options.ScheduledSyncMode.Trim();

                _logger.LogInformation(
                    "Next scheduled ERP sync ({Mode}) for slot {Slot:u} UTC — waiting {Delay}",
                    mode,
                    slot,
                    delay);

                await DelaySafe(delay, stoppingToken);
                if (stoppingToken.IsCancellationRequested)
                    break;

                options = _options.CurrentValue;
                if (!options.Enabled)
                    continue;

                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var syncService = scope.ServiceProvider.GetRequiredService<IErpProductSyncService>();

                    _logger.LogInformation("Starting scheduled ERP product sync (mode={Mode}, slot={Slot:u})", mode, slot);
                    var result = await syncService.SyncAllProductsAsync(mode, stoppingToken);
                    _logger.LogInformation(
                        "Scheduled ERP sync finished: JobId={JobId} Status={Status} New={New} Updated={Updated} Failed={Failed} Changes={Changes}",
                        result.JobId,
                        result.Status,
                        result.NewProducts,
                        result.UpdatedProducts,
                        result.FailedProducts,
                        result.TotalChanges);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Scheduled ERP product sync failed");
                    await DelaySafe(TimeSpan.FromMinutes(5), stoppingToken);
                }
                finally
                {
                    // Marquer ce créneau comme fait → prochain = vendredi suivant.
                    _lastCompletedSlotUtc = slot;
                }
            }
        }

        /// <summary>
        /// Prochain créneau UTC. Si on est le bon jour dans les 6h après l'heure et que
        /// ce créneau n'a pas encore été exécuté → rattrapage immédiat.
        /// </summary>
        internal static DateTime GetNextSlotUtc(
            int hour,
            int minute,
            int? dayOfWeek,
            DateTime? lastCompletedSlotUtc)
        {
            hour = Math.Clamp(hour, 0, 23);
            minute = Math.Clamp(minute, 0, 59);
            var now = DateTime.UtcNow;

            if (!dayOfWeek.HasValue)
            {
                var today = new DateTime(now.Year, now.Month, now.Day, hour, minute, 0, DateTimeKind.Utc);
                if (lastCompletedSlotUtc != today
                    && now >= today
                    && now <= today.AddHours(6))
                {
                    return today;
                }

                var next = today;
                if (next <= now)
                    next = next.AddDays(1);
                if (lastCompletedSlotUtc == next)
                    next = next.AddDays(1);
                return next;
            }

            var targetDow = (DayOfWeek)Math.Clamp(dayOfWeek.Value, 0, 6);
            var daysUntil = ((int)targetDow - (int)now.DayOfWeek + 7) % 7;
            var slot = new DateTime(now.Year, now.Month, now.Day, hour, minute, 0, DateTimeKind.Utc)
                .AddDays(daysUntil);

            // Aujourd'hui = jour cible.
            if (daysUntil == 0)
            {
                if (lastCompletedSlotUtc == slot)
                    return slot.AddDays(7);

                if (now < slot)
                    return slot;

                // Rattrapage : jusqu'à 6h après l'heure prévue.
                if (now <= slot.AddHours(6))
                    return slot;

                return slot.AddDays(7);
            }

            if (lastCompletedSlotUtc == slot)
                return slot.AddDays(7);

            return slot;
        }

        /// <summary>
        /// Au démarrage du conteneur, les jobs Running en base n'ont plus de Task associée.
        /// Les laisser en Running bloque StartSyncAll (ancien comportement) et trompe l'UI.
        /// </summary>
        private async Task ClearOrphanedRunningJobsAsync(CancellationToken ct)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var storage = scope.ServiceProvider.GetRequiredService<IStorageBroker>();
                var orphans = await storage.SelectAllErpSyncLogs()
                    .Where(s => s.Status == "Running")
                    .ToListAsync(ct);

                if (orphans.Count == 0)
                    return;

                foreach (var orphan in orphans)
                {
                    orphan.Status = "Cancelled";
                    orphan.CompletedAt = DateTime.UtcNow;
                    orphan.ErrorMessage = "Annulé au redémarrage (job orphelin)";
                    await storage.UpdateErpSyncLogAsync(orphan);
                    _logger.LogWarning(
                        "Cleared orphaned ERP sync job {JobId} (processed={Processed}/{Total})",
                        orphan.JobId,
                        orphan.ProcessedProducts,
                        orphan.TotalProducts);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Failed to clear orphaned ERP sync jobs");
            }
        }

        private static async Task DelaySafe(TimeSpan delay, CancellationToken ct)
        {
            if (delay <= TimeSpan.Zero)
                return;
            try
            {
                // Tranches d'1h pour rester réactif au stoppingToken.
                var remaining = delay;
                while (remaining > TimeSpan.Zero && !ct.IsCancellationRequested)
                {
                    var slice = remaining > TimeSpan.FromHours(1) ? TimeSpan.FromHours(1) : remaining;
                    await Task.Delay(slice, ct);
                    remaining -= slice;
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
            }
        }
    }
}
