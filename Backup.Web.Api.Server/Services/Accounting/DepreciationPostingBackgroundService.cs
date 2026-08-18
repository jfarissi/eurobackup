using System;
using System.Threading;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Services.Numbering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Backup.Web.Api.Server.Services.Accounting
{
    /// <summary>Constate les dotations du mois précédent (tous les jours, RG-AM7 / RG-EI6).</summary>
    public class DepreciationPostingBackgroundService : BackgroundService
    {
        private static readonly TimeSpan RunInterval = TimeSpan.FromHours(24);
        private static readonly TimeSpan InitialDelay = TimeSpan.FromMinutes(2);
        private const string Actor = "System";

        private readonly IServiceScopeFactory scopeFactory;
        private readonly ILogger<DepreciationPostingBackgroundService> logger;

        public DepreciationPostingBackgroundService(
            IServiceScopeFactory scopeFactory,
            ILogger<DepreciationPostingBackgroundService> logger)
        {
            this.scopeFactory = scopeFactory;
            this.logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            this.logger.LogInformation("Depreciation posting background service started (every {Interval})", RunInterval);
            await DelaySafe(InitialDelay, stoppingToken);
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var posted = await RunOnceAsync(stoppingToken);
                    if (posted > 0)
                        this.logger.LogInformation("Depreciation posting: {Count} line(s)", posted);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    this.logger.LogError(ex, "Depreciation posting run failed");
                }

                await DelaySafe(RunInterval, stoppingToken);
            }
        }

        internal async Task<int> RunOnceAsync(CancellationToken ct)
        {
            using var scope = this.scopeFactory.CreateScope();
            var storage = scope.ServiceProvider.GetRequiredService<IStorageBroker>();
            var numbering = scope.ServiceProvider.GetRequiredService<INumberingSequenceService>();
            var previous = DateTime.UtcNow.Date.AddMonths(-1);
            var total = 0;
            var companies = storage.SelectAllCompanies();
            foreach (var company in companies)
            {
                if (ct.IsCancellationRequested) break;
                if (!company.IsActive) continue;
                var (result, error) = await FixedAssetService.PostMonthAsync(
                    storage, numbering, company.Id, previous.Year, previous.Month, Actor);
                if (error != null)
                {
                    this.logger.LogWarning("Depreciation skip {CompanyId} {Month}/{Year}: {Error}",
                        company.Id, previous.Month, previous.Year, error);
                    continue;
                }
                total += result?.PostedLines ?? 0;
            }
            return total;
        }

        private static async Task DelaySafe(TimeSpan delay, CancellationToken ct)
        {
            try { await Task.Delay(delay, ct); }
            catch (OperationCanceledException) { }
        }
    }
}
