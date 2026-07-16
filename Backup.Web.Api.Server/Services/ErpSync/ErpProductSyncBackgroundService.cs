using System;
using System.Threading;
using System.Threading.Tasks;
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
            _logger.LogInformation("ERP product sync background service started");

            while (!stoppingToken.IsCancellationRequested)
            {
                var options = _options.CurrentValue;
                if (!options.Enabled)
                {
                    await DelaySafe(TimeSpan.FromMinutes(1), stoppingToken);
                    continue;
                }

                var delay = GetDelayUntilNextRun(options.CronHour, options.CronMinute);
                _logger.LogInformation(
                    "Next ERP product sync scheduled in {Delay} (target {Hour:D2}:{Minute:D2} UTC)",
                    delay,
                    options.CronHour,
                    options.CronMinute);

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
                    _logger.LogInformation("Starting scheduled ERP product sync");
                    var result = await syncService.SyncAllProductsAsync(stoppingToken);
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
            }
        }

        private static TimeSpan GetDelayUntilNextRun(int hour, int minute)
        {
            hour = Math.Clamp(hour, 0, 23);
            minute = Math.Clamp(minute, 0, 59);
            var now = DateTime.UtcNow;
            var next = new DateTime(now.Year, now.Month, now.Day, hour, minute, 0, DateTimeKind.Utc);
            if (next <= now)
                next = next.AddDays(1);
            return next - now;
        }

        private static async Task DelaySafe(TimeSpan delay, CancellationToken ct)
        {
            if (delay <= TimeSpan.Zero)
                return;
            try
            {
                await Task.Delay(delay, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
            }
        }
    }
}
