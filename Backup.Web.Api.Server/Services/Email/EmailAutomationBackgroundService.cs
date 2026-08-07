namespace Backup.Web.Api.Server.Services.Email
{
    /// <summary>Relances impayées et alertes stock (toutes les heures).</summary>
    public class EmailAutomationBackgroundService : BackgroundService
    {
        private readonly IServiceProvider services;
        private readonly ILogger<EmailAutomationBackgroundService> logger;

        public EmailAutomationBackgroundService(IServiceProvider services, ILogger<EmailAutomationBackgroundService> logger)
        {
            this.services = services;
            this.logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = this.services.CreateScope();
                    var automation = scope.ServiceProvider.GetRequiredService<IEmailAutomationService>();
                    var reminders = await automation.RunPaymentRemindersAsync(null, "AutoReminder", manual: false);
                    if (reminders.Queued > 0)
                        this.logger.LogInformation("Relances auto : {Count} email(s)", reminders.Queued);

                    var alerts = await automation.RunStockAlertsAsync(null, "AutoStockAlert");
                    if (alerts.Queued > 0)
                        this.logger.LogInformation("Alertes stock : {Count} email(s)", alerts.Queued);
                }
                catch (Exception ex)
                {
                    this.logger.LogWarning(ex, "Erreur automation email");
                }

                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }
        }
    }
}
