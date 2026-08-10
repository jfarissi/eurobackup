namespace Backup.Web.Api.Server.Services.Email
{
    /// <summary>Relances impayées et alertes stock (toutes les heures).</summary>
    public class EmailAutomationBackgroundService : BackgroundService
    {
        private readonly IServiceProvider services;
        private readonly ILogger<EmailAutomationBackgroundService> logger;
        private bool schemaMissingLogged;

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
                var delay = TimeSpan.FromHours(1);
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

                    this.schemaMissingLogged = false;
                }
                catch (Exception ex) when (IsMissingEmailSchema(ex))
                {
                    if (!this.schemaMissingLogged)
                    {
                        this.logger.LogWarning(
                            "Tables email absentes — automation email en pause. " +
                            "Exécuter scripts/add-email-system.sql (+ add-email-automation.sql).");
                        this.schemaMissingLogged = true;
                    }
                    delay = TimeSpan.FromHours(6);
                }
                catch (Exception ex)
                {
                    this.logger.LogWarning(ex, "Erreur automation email");
                }

                await Task.Delay(delay, stoppingToken);
            }
        }

        private static bool IsMissingEmailSchema(Exception ex)
        {
            for (var e = ex; e != null; e = e.InnerException!)
            {
                if (e.Message.Contains("EmailMessages", StringComparison.OrdinalIgnoreCase)
                    || e.Message.Contains("CompanyEmailSettings", StringComparison.OrdinalIgnoreCase))
                {
                    if (e.Message.Contains("doesn't exist", StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
            return false;
        }
    }
}
