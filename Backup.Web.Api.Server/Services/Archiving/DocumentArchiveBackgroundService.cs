using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Models.Entities;
using Backup.Web.Api.Server.Services.Sales;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Backup.Web.Api.Server.Services.Archiving
{
    /// <summary>
    /// RG-S3 : archive automatiquement les documents clôturés/annulés/facturés plus vieux que
    /// Company.RetentionMonths (défaut 24 mois). Tourne toutes les 6h.
    /// </summary>
    public class DocumentArchiveBackgroundService : BackgroundService
    {
        private static readonly TimeSpan RunInterval = TimeSpan.FromHours(6);
        private static readonly TimeSpan InitialDelay = TimeSpan.FromMinutes(1);
        private const string Actor = "System";

        private readonly IServiceScopeFactory scopeFactory;
        private readonly ILogger<DocumentArchiveBackgroundService> logger;

        public DocumentArchiveBackgroundService(
            IServiceScopeFactory scopeFactory,
            ILogger<DocumentArchiveBackgroundService> logger)
        {
            this.scopeFactory = scopeFactory;
            this.logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            this.logger.LogInformation(
                "Document archive background service started (RG-S3, every {Interval})", RunInterval);

            await DelaySafe(InitialDelay, stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var archived = await RunOnceAsync(stoppingToken);
                    if (archived > 0)
                        this.logger.LogInformation("Document archive run: {Count} document(s) archived", archived);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    this.logger.LogError(ex, "Document archive run failed");
                }

                await DelaySafe(RunInterval, stoppingToken);
            }
        }

        internal async Task<int> RunOnceAsync(CancellationToken ct)
        {
            using var scope = this.scopeFactory.CreateScope();
            var storage = scope.ServiceProvider.GetRequiredService<IStorageBroker>();

            var companies = storage.SelectAllCompanies().Where(c => c.IsActive).ToList();
            var total = 0;

            foreach (var company in companies)
            {
                if (ct.IsCancellationRequested) break;
                var months = company.RetentionMonths > 0 ? company.RetentionMonths : 24;
                var cutoff = DateTime.UtcNow.AddMonths(-months);

                total += await ArchiveQuotesAsync(storage, company.Id, cutoff);
                total += await ArchiveOrdersAsync(storage, company.Id, cutoff);
                total += await ArchiveInvoicesAsync(storage, company.Id, cutoff);
                total += await ArchiveCreditNotesAsync(storage, company.Id, cutoff);
                total += await ArchiveDeliveryNotesAsync(storage, company.Id, cutoff);
            }

            return total;
        }

        private static IEnumerable<T> Eligible<T>(IEnumerable<T> candidates, Func<T, string?> status, Func<T, DateTime> createdAt, DateTime cutoff)
            where T : class
        {
            return candidates.Where(c => SalesBusinessRules.CanArchive(status(c)) && createdAt(c) < cutoff);
        }

        private static async Task<int> ArchiveQuotesAsync(IStorageBroker storage, string? companyId, DateTime cutoff)
        {
            var candidates = storage.SelectAllQuotes().Where(q => q.CompanyId == companyId && !q.IsArchived).ToList();
            var toArchive = Eligible(candidates, q => q.Status, q => q.CreatedAt, cutoff).ToList();
            foreach (var q in toArchive)
            {
                SalesBusinessRules.Archive(q, Actor);
                await storage.UpdateQuoteAsync(q);
                await SalesDocumentAudit.LogAsync(storage, q.CompanyId ?? companyId, "Quote", q.Id, "Archived", Actor, $"Archivage automatique {q.QuoteNumber}");
            }
            return toArchive.Count;
        }

        private static async Task<int> ArchiveOrdersAsync(IStorageBroker storage, string? companyId, DateTime cutoff)
        {
            var candidates = storage.SelectAllSalesOrders().Where(o => o.CompanyId == companyId && !o.IsArchived).ToList();
            var toArchive = Eligible(candidates, o => o.Status, o => o.CreatedAt, cutoff).ToList();
            foreach (var o in toArchive)
            {
                SalesBusinessRules.Archive(o, Actor);
                await storage.UpdateSalesOrderAsync(o);
                await SalesDocumentAudit.LogAsync(storage, o.CompanyId ?? companyId, "Order", o.Id, "Archived", Actor, $"Archivage automatique {o.OrderNumber}");
            }
            return toArchive.Count;
        }

        private static async Task<int> ArchiveInvoicesAsync(IStorageBroker storage, string? companyId, DateTime cutoff)
        {
            var candidates = storage.SelectAllSalesInvoices().Where(i => i.CompanyId == companyId && !i.IsArchived).ToList();
            var toArchive = Eligible(candidates, i => i.Status, i => i.CreatedAt, cutoff).ToList();
            foreach (var i in toArchive)
            {
                SalesBusinessRules.Archive(i, Actor);
                await storage.UpdateSalesInvoiceAsync(i);
                await SalesDocumentAudit.LogAsync(storage, i.CompanyId ?? companyId, "Invoice", i.Id, "Archived", Actor, $"Archivage automatique {i.InvoiceNumber}");
            }
            return toArchive.Count;
        }

        private static async Task<int> ArchiveCreditNotesAsync(IStorageBroker storage, string? companyId, DateTime cutoff)
        {
            var candidates = storage.SelectAllCreditNotes().Where(c => c.CompanyId == companyId && !c.IsArchived).ToList();
            var toArchive = Eligible(candidates, c => c.Status, c => c.CreatedAt, cutoff).ToList();
            foreach (var c in toArchive)
            {
                SalesBusinessRules.Archive(c, Actor);
                await storage.UpdateCreditNoteAsync(c);
                await SalesDocumentAudit.LogAsync(storage, c.CompanyId ?? companyId, "CreditNote", c.Id, "Archived", Actor, $"Archivage automatique {c.CreditNoteNumber}");
            }
            return toArchive.Count;
        }

        private static async Task<int> ArchiveDeliveryNotesAsync(IStorageBroker storage, string? companyId, DateTime cutoff)
        {
            var candidates = storage.SelectAllSalesDeliveryNotes().Where(n => n.CompanyId == companyId && !n.IsArchived).ToList();
            var toArchive = Eligible(candidates, n => n.Status, n => n.CreatedAt, cutoff).ToList();
            foreach (var n in toArchive)
            {
                SalesBusinessRules.Archive(n, Actor);
                await storage.UpdateSalesDeliveryNoteAsync(n);
                await SalesDocumentAudit.LogAsync(storage, n.CompanyId ?? companyId, "DeliveryNote", n.Id, "Archived", Actor, $"Archivage automatique {n.DeliveryNumber}");
            }
            return toArchive.Count;
        }

        private static async Task DelaySafe(TimeSpan delay, CancellationToken ct)
        {
            if (delay <= TimeSpan.Zero) return;
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
