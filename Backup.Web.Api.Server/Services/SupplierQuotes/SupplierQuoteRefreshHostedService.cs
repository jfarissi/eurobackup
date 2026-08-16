using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Backup.Web.Api.Server.Services.SupplierQuotes
{
    /// <summary>Re-cote uniquement les produits avec des abonnés SignalR.</summary>
    public sealed class SupplierQuoteRefreshHostedService : BackgroundService
    {
        private static readonly TimeSpan Interval = TimeSpan.FromSeconds(15);

        private readonly IServiceScopeFactory scopeFactory;
        private readonly ISupplierQuoteSubscriptionStore subscriptions;
        private readonly ILogger<SupplierQuoteRefreshHostedService> logger;

        public SupplierQuoteRefreshHostedService(
            IServiceScopeFactory scopeFactory,
            ISupplierQuoteSubscriptionStore subscriptions,
            ILogger<SupplierQuoteRefreshHostedService> logger)
        {
            this.scopeFactory = scopeFactory;
            this.subscriptions = subscriptions;
            this.logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(Interval, stoppingToken);
                    var active = this.subscriptions.ActiveProducts();
                    if (active.Count == 0) continue;

                    foreach (var (companyId, productId) in active)
                    {
                        if (stoppingToken.IsCancellationRequested) break;
                        try
                        {
                            using var scope = this.scopeFactory.CreateScope();
                            var quotes = scope.ServiceProvider.GetRequiredService<ISupplierQuoteService>();
                            var notifier = scope.ServiceProvider.GetRequiredService<ISupplierQuoteNotifier>();
                            var result = await quotes.GetQuotesAsync(productId, companyId, forceRefresh: true, stoppingToken);
                            await notifier.NotifyQuotesUpdatedAsync(companyId, productId, result, stoppingToken);
                        }
                        catch (Exception ex)
                        {
                            this.logger.LogDebug(ex, "Live quote refresh failed for product {ProductId}", productId);
                        }
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    this.logger.LogWarning(ex, "Supplier quote refresh loop error");
                }
            }
        }
    }
}
