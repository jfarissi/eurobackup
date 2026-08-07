using Backup.Web.Api.Server.Brokers.Storage;

namespace Backup.Web.Api.Server.Services.Email
{
    public class EmailQueueBackgroundService : BackgroundService
    {
        private readonly IServiceProvider services;
        private readonly ILogger<EmailQueueBackgroundService> logger;

        public EmailQueueBackgroundService(IServiceProvider services, ILogger<EmailQueueBackgroundService> logger)
        {
            this.services = services;
            this.logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = this.services.CreateScope();
                    var dispatch = scope.ServiceProvider.GetRequiredService<IEmailDispatchService>();
                    var sent = await dispatch.ProcessPendingAsync(25, stoppingToken);
                    if (sent > 0)
                        this.logger.LogInformation("Emails envoyés: {Count}", sent);
                }
                catch (Exception ex)
                {
                    this.logger.LogWarning(ex, "Erreur traitement file email");
                }

                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }
    }
}
