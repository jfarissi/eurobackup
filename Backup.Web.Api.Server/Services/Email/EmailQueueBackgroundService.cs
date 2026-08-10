using Backup.Web.Api.Server.Brokers.Storage;
using MySqlConnector;

namespace Backup.Web.Api.Server.Services.Email
{
    public class EmailQueueBackgroundService : BackgroundService
    {
        private readonly IServiceProvider services;
        private readonly ILogger<EmailQueueBackgroundService> logger;
        private bool schemaMissingLogged;

        public EmailQueueBackgroundService(IServiceProvider services, ILogger<EmailQueueBackgroundService> logger)
        {
            this.services = services;
            this.logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var delay = TimeSpan.FromSeconds(30);
                try
                {
                    using var scope = this.services.CreateScope();
                    var dispatch = scope.ServiceProvider.GetRequiredService<IEmailDispatchService>();
                    var sent = await dispatch.ProcessPendingAsync(25, stoppingToken);
                    if (sent > 0)
                        this.logger.LogInformation("Emails envoyés: {Count}", sent);
                    this.schemaMissingLogged = false;
                }
                catch (Exception ex) when (IsMissingEmailSchema(ex))
                {
                    if (!this.schemaMissingLogged)
                    {
                        this.logger.LogWarning(
                            "Tables email absentes (EmailMessages / CompanyEmailSettings). " +
                            "Exécuter scripts/add-email-system.sql (+ add-email-automation.sql). " +
                            "File email en pause jusqu'à correction.");
                        this.schemaMissingLogged = true;
                    }
                    delay = TimeSpan.FromMinutes(10);
                }
                catch (Exception ex)
                {
                    this.logger.LogWarning(ex, "Erreur traitement file email");
                }

                await Task.Delay(delay, stoppingToken);
            }
        }

        private static bool IsMissingEmailSchema(Exception ex)
        {
            for (var e = ex; e != null; e = e.InnerException!)
            {
                if (e is MySqlException mysql
                    && (mysql.Message.Contains("EmailMessages", StringComparison.OrdinalIgnoreCase)
                        || mysql.Message.Contains("CompanyEmailSettings", StringComparison.OrdinalIgnoreCase))
                    && mysql.Message.Contains("doesn't exist", StringComparison.OrdinalIgnoreCase))
                    return true;

                if (e.Message.Contains("EmailMessages", StringComparison.OrdinalIgnoreCase)
                    && e.Message.Contains("doesn't exist", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
    }
}
