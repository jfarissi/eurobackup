using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Models;
using Backup.Web.Api.Server.Models.Entities;
using Backup.Web.Api.Server.Models.Entities.Email;
using Backup.Web.Api.Server.Models.Entities.SaaS;
using Backup.Web.Api.Server.Services.Sales;
using Backup.Web.Api.Server.Services.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Backup.Web.Api.Server.Services.Email
{
    public interface IEmailAutomationService
    {
        Task<EmailAutomationResult> RunPaymentRemindersAsync(string? companyId, string? actor, bool manual = false);
        Task<EmailAutomationResult> RunStockAlertsAsync(string? companyId, string? actor);
    }

    public sealed class EmailAutomationResult
    {
        public int Queued { get; set; }
        public int Skipped { get; set; }
        public List<string> Messages { get; set; } = new();
    }

    public class EmailAutomationService : IEmailAutomationService
    {
        private readonly IStorageBroker storage;
        private readonly IEmailDispatchService dispatch;

        public EmailAutomationService(IStorageBroker storage, IEmailDispatchService dispatch)
        {
            this.storage = storage;
            this.dispatch = dispatch;
        }

        public async Task<EmailAutomationResult> RunPaymentRemindersAsync(string? companyId, string? actor, bool manual = false)
        {
            var result = new EmailAutomationResult();
            var companies = await ResolveCompaniesAsync(companyId);
            foreach (var company in companies)
            {
                var settings = await this.storage.SelectCompanyEmailSettingsByCompanyIdAsync(company.Id);
                if (settings == null || !settings.Enabled) continue;
                if (!manual && !settings.AutoPaymentRemindersEnabled) continue;

                var invoices = this.storage.SelectAllSalesInvoices()
                    .ForCompany(company.Id)
                    .OrderBy(i => i.DueDate)
                    .ToList();
                foreach (var invoice in invoices)
                {
                    SalesInvoiceSettlement.Enrich(invoice, this.storage);
                    if (!SalesInvoiceReminderHelper.IsOverdue(invoice)) continue;

                    var daysOverdue = SalesInvoiceReminderHelper.GetDaysOverdue(invoice);
                    var templateCode = manual
                        ? SalesInvoiceReminderHelper.ResolveTemplateCode(daysOverdue, settings)
                        : SalesInvoiceReminderHelper.ResolveAutoTemplateCode(daysOverdue, settings);
                    if (templateCode == null)
                    {
                        result.Skipped++;
                        continue;
                    }

                    if (await WasReminderSentAsync(company.Id, invoice.Id, templateCode))
                    {
                        result.Skipped++;
                        continue;
                    }

                    try
                    {
                        await this.dispatch.QueueAsync(company.Id, new SendEmailRequest
                        {
                            DocumentType = "SalesInvoice",
                            DocumentId = invoice.Id,
                            TemplateCode = templateCode,
                            SendNow = true
                        }, actor ?? "System");
                        result.Queued++;
                        result.Messages.Add($"Relance {templateCode} — facture {invoice.InvoiceNumber}");
                    }
                    catch (Exception ex)
                    {
                        result.Skipped++;
                        result.Messages.Add($"Facture {invoice.InvoiceNumber} : {ex.Message}");
                    }
                }
            }

            return result;
        }

        public async Task<EmailAutomationResult> RunStockAlertsAsync(string? companyId, string? actor)
        {
            var result = new EmailAutomationResult();
            var companies = await ResolveCompaniesAsync(companyId);
            foreach (var company in companies)
            {
                var settings = await this.storage.SelectCompanyEmailSettingsByCompanyIdAsync(company.Id);
                if (settings == null || !settings.Enabled || !settings.AutoStockAlertsEnabled) continue;

                var recipients = EmailAddressValidator.ParseList(settings.StockAlertRecipients, 10);
                if (recipients.Count == 0)
                {
                    result.Skipped++;
                    result.Messages.Add($"{company.Name} : aucun destinataire alerte stock.");
                    continue;
                }

                var cooldownHours = Math.Max(1, settings.StockAlertCooldownHours);
                var since = DateTime.UtcNow.AddHours(-cooldownHours);

                var criticalItems = this.storage.SelectAllStock()
                    .ForCompany(company.Id)
                    .AsEnumerable()
                    .Where(IsStockCritical)
                    .ToList();

                foreach (var item in criticalItems)
                {
                    if (await WasStockAlertSentRecentlyAsync(company.Id, item.Id, since))
                    {
                        result.Skipped++;
                        continue;
                    }

                    var available = Math.Max(0m, item.QuantityOnHand - item.ReservedQuantity);
                    var vars = BuildStockVars(company, item, available);
                    var template = EmailTemplateCatalog.Get(EmailTemplateCodes.StockCriticalAlert);

                    foreach (var to in recipients)
                    {
                        try
                        {
                            await this.dispatch.QueueTemplateAsync(company.Id, new QueueTemplateEmailRequest
                            {
                                TemplateCode = template.Code,
                                ToEmail = to,
                                Variables = vars,
                                DocumentType = "StockItem",
                                DocumentId = item.Id,
                                DocumentNumber = item.ProductKey,
                                SendNow = true
                            }, actor ?? "System");
                            result.Queued++;
                        }
                        catch (Exception ex)
                        {
                            result.Messages.Add($"{item.ProductKey} → {to} : {ex.Message}");
                        }
                    }

                    if (recipients.Count > 0)
                        result.Messages.Add($"Alerte stock {item.ProductKey} (dispo {available:0.####} < min {item.MinStock:0.####})");
                }
            }

            return result;
        }

        private static bool IsStockCritical(StockItem item)
        {
            if (item.MinStock <= 0) return false;
            var available = Math.Max(0m, item.QuantityOnHand - item.ReservedQuantity);
            return available + 0.0001m < item.MinStock;
        }

        private static Dictionary<string, string> BuildStockVars(Company company, StockItem item, decimal available) =>
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["societe.nom"] = company.Name,
                ["produit.cle"] = item.ProductKey,
                ["produit.disponible"] = available.ToString("0.####"),
                ["produit.min"] = item.MinStock.ToString("0.####"),
                ["produit.quantite"] = item.QuantityOnHand.ToString("0.####"),
                ["produit.reserve"] = item.ReservedQuantity.ToString("0.####")
            };

        private async Task<bool> WasReminderSentAsync(string companyId, int invoiceId, string templateCode) =>
            await this.storage.SelectAllEmailMessages()
                .Where(m => m.CompanyId == companyId
                    && m.DocumentType == "SalesInvoice"
                    && m.DocumentId == invoiceId
                    && m.TemplateCode == templateCode
                    && (m.Status == EmailStatuses.Sent || m.Status == EmailStatuses.Pending || m.Status == EmailStatuses.Scheduled))
                .AnyAsync();

        private async Task<bool> WasStockAlertSentRecentlyAsync(string companyId, int stockItemId, DateTime sinceUtc) =>
            await this.storage.SelectAllEmailMessages()
                .Where(m => m.CompanyId == companyId
                    && m.DocumentType == "StockItem"
                    && m.DocumentId == stockItemId
                    && m.TemplateCode == EmailTemplateCodes.StockCriticalAlert
                    && m.Status == EmailStatuses.Sent
                    && m.SentAt >= sinceUtc)
                .AnyAsync();

        private async Task<List<Company>> ResolveCompaniesAsync(string? companyId)
        {
            if (!string.IsNullOrWhiteSpace(companyId))
            {
                var one = await this.storage.SelectAllCompanies().FirstOrDefaultAsync(c => c.Id == companyId);
                return one == null ? new List<Company>() : new List<Company> { one };
            }

            return await this.storage.SelectAllCompanies().ToListAsync();
        }
    }
}
