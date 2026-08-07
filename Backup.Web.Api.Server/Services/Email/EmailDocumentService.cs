using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Models.Entities;
using Backup.Web.Api.Server.Models.Entities.Email;
using Backup.Web.Api.Server.Models.Entities.SaaS;
using Backup.Web.Api.Server.Services.BusinessPdf;
using Backup.Web.Api.Server.Services.Tenancy;

namespace Backup.Web.Api.Server.Services.Email
{
    public interface IEmailDocumentService
    {
        Task<EmailDocumentPayload?> BuildAsync(string? companyId, string documentType, int documentId, string? templateCode = null);
    }

    public class EmailDocumentService : IEmailDocumentService
    {
        private readonly IStorageBroker storage;
        private readonly IBusinessDocumentPdfService pdfService;

        public EmailDocumentService(IStorageBroker storage, IBusinessDocumentPdfService pdfService)
        {
            this.storage = storage;
            this.pdfService = pdfService;
        }

        public async Task<EmailDocumentPayload?> BuildAsync(string? companyId, string documentType, int documentId, string? templateCode = null)
        {
            var company = ResolveCompany(companyId);
            if (company == null) return null;

            return documentType.Trim().ToLowerInvariant() switch
            {
                "quote" => await BuildQuoteAsync(company, documentId, templateCode ?? EmailTemplateCodes.QuoteClient),
                "salesinvoice" or "invoice" => await BuildInvoiceAsync(company, documentId, templateCode ?? EmailTemplateCodes.InvoiceIssued),
                "salesdeliverynote" or "deliverynote" => await BuildDeliveryNoteAsync(company, documentId, templateCode ?? EmailTemplateCodes.DeliveryShipped),
                "salesorder" or "order" => await BuildOrderAsync(company, documentId, templateCode ?? EmailTemplateCodes.OrderConfirmation),
                "creditnote" => await BuildCreditNoteAsync(company, documentId, templateCode ?? EmailTemplateCodes.CreditNoteIssued),
                "purchaseorder" => await BuildPurchaseOrderAsync(company, documentId, templateCode ?? EmailTemplateCodes.PurchaseOrder),
                _ => null
            };
        }

        private Company? ResolveCompany(string? companyId)
        {
            if (!string.IsNullOrWhiteSpace(companyId))
                return this.storage.SelectAllCompanies().FirstOrDefault(c => c.Id == companyId);
            return this.storage.SelectAllCompanies().FirstOrDefault();
        }

        private async Task<EmailDocumentPayload?> BuildQuoteAsync(Company company, int id, string templateCode)
        {
            var quote = await this.storage.SelectQuoteByIdAsync(id);
            if (quote == null || !quote.BelongsToCompany(company.Id)) return null;
            var pdf = this.pdfService.GenerateQuotePdf(quote);
            var vars = BaseVars(company, quote.Customer?.Name, quote.Customer?.Email, quote.QuoteNumber, quote.Date, quote.ExpirationDate, quote.TotalTTC);
            return Render(templateCode, "Quote", quote.Id, quote.QuoteNumber, quote.Customer?.Email, quote.Customer?.Name, vars, $"{Sanitize(quote.QuoteNumber)}.pdf", pdf);
        }

        private async Task<EmailDocumentPayload?> BuildInvoiceAsync(Company company, int id, string templateCode)
        {
            var invoice = await this.storage.SelectSalesInvoiceByIdAsync(id);
            if (invoice == null || !invoice.BelongsToCompany(company.Id)) return null;
            Backup.Web.Api.Server.Services.Sales.SalesInvoiceSettlement.Enrich(invoice, this.storage);
            var pdf = this.pdfService.GenerateSalesInvoicePdf(invoice);
            var vars = BaseVars(company, invoice.Customer?.Name, invoice.Customer?.Email, invoice.InvoiceNumber, invoice.Date, invoice.DueDate, invoice.TotalTTC);
            vars["document.reste_du"] = EmailTemplateRenderer.FormatMoney(invoice.RemainingAmount, company.DefaultCurrencyCode);
            vars["document.jours_retard"] = SalesInvoiceReminderHelper.GetDaysOverdue(invoice).ToString();
            return Render(templateCode, "SalesInvoice", invoice.Id, invoice.InvoiceNumber, invoice.Customer?.Email, invoice.Customer?.Name, vars, $"{Sanitize(invoice.InvoiceNumber)}.pdf", pdf);
        }

        private async Task<EmailDocumentPayload?> BuildDeliveryNoteAsync(Company company, int id, string templateCode)
        {
            var note = await this.storage.SelectSalesDeliveryNoteByIdAsync(id);
            if (note == null || !note.BelongsToCompany(company.Id)) return null;
            var pdf = this.pdfService.GenerateSalesDeliveryNotePdf(note);
            var customer = this.storage.SelectAllCustomers().FirstOrDefault(c => c.Id == note.CustomerId);
            var vars = BaseVars(company, customer?.Name, customer?.Email, note.DeliveryNumber, note.DeliveryDate, null, note.TotalTTC);
            return Render(templateCode, "SalesDeliveryNote", note.Id, note.DeliveryNumber, customer?.Email, customer?.Name, vars, $"{Sanitize(note.DeliveryNumber)}.pdf", pdf);
        }

        private async Task<EmailDocumentPayload?> BuildOrderAsync(Company company, int id, string templateCode)
        {
            var order = await this.storage.SelectSalesOrderByIdAsync(id);
            if (order == null || !order.BelongsToCompany(company.Id)) return null;
            var pdf = this.pdfService.GenerateSalesOrderPdf(order);
            var vars = BaseVars(company, order.Customer?.Name, order.Customer?.Email, order.OrderNumber, order.Date, null, order.TotalTTC);
            return Render(templateCode, "SalesOrder", order.Id, order.OrderNumber, order.Customer?.Email, order.Customer?.Name, vars, $"{Sanitize(order.OrderNumber)}.pdf", pdf);
        }

        private async Task<EmailDocumentPayload?> BuildCreditNoteAsync(Company company, int id, string templateCode)
        {
            var cn = await this.storage.SelectCreditNoteByIdAsync(id);
            if (cn == null || !cn.BelongsToCompany(company.Id)) return null;
            var pdf = this.pdfService.GenerateCreditNotePdf(cn);
            var customer = this.storage.SelectAllCustomers().FirstOrDefault(c => c.Id == cn.CustomerId);
            var vars = BaseVars(company, customer?.Name, customer?.Email, cn.CreditNoteNumber, cn.Date, null, cn.TotalTTC);
            return Render(templateCode, "CreditNote", cn.Id, cn.CreditNoteNumber, customer?.Email, customer?.Name, vars, $"{Sanitize(cn.CreditNoteNumber)}.pdf", pdf);
        }

        private async Task<EmailDocumentPayload?> BuildPurchaseOrderAsync(Company company, int id, string templateCode)
        {
            var po = await this.storage.SelectPurchaseOrderByIdAsync(id);
            if (po == null || !po.BelongsToCompany(company.Id)) return null;
            var pdf = this.pdfService.GeneratePurchaseOrderPdf(po);
            var supplier = this.storage.SelectAllSuppliers().FirstOrDefault(s => s.Id == po.SupplierId);
            var vars = BaseVars(company, supplier?.Name, supplier?.Email, po.OrderNumber, po.Date, null, po.TotalTTC);
            vars["fournisseur.nom"] = supplier?.Name ?? "";
            vars["fournisseur.email"] = supplier?.Email ?? "";
            return Render(templateCode, "PurchaseOrder", po.Id, po.OrderNumber, supplier?.Email, supplier?.Name, vars, $"{Sanitize(po.OrderNumber)}.pdf", pdf);
        }

        private static EmailDocumentPayload Render(
            string templateCode,
            string documentType,
            int documentId,
            string documentNumber,
            string? recipientEmail,
            string? recipientName,
            Dictionary<string, string> vars,
            string fileName,
            byte[] pdf)
        {
            var template = EmailTemplateCatalog.Get(templateCode);
            return new EmailDocumentPayload
            {
                TemplateCode = template.Code,
                DocumentType = documentType,
                DocumentId = documentId,
                DocumentNumber = documentNumber,
                RecipientEmail = recipientEmail,
                RecipientName = recipientName,
                Subject = EmailTemplateRenderer.Render(template.SubjectPattern, vars),
                BodyHtml = EmailTemplateRenderer.Render(template.BodyHtmlPattern, vars),
                AttachmentFileName = fileName,
                AttachmentBytes = pdf,
                Variables = vars
            };
        }

        private static Dictionary<string, string> BaseVars(
            Company company,
            string? partyName,
            string? partyEmail,
            string docNumber,
            DateTime? docDate,
            DateTime? dueDate,
            decimal totalTtc)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["societe.nom"] = company.Name,
                ["societe.telephone"] = "",
                ["client.nom"] = partyName ?? "",
                ["client.email"] = partyEmail ?? "",
                ["document.numero"] = docNumber,
                ["document.date"] = EmailTemplateRenderer.FormatDate(docDate),
                ["document.echeance"] = EmailTemplateRenderer.FormatDate(dueDate),
                ["document.montant_ttc"] = EmailTemplateRenderer.FormatMoney(totalTtc, company.DefaultCurrencyCode)
            };
        }

        private static string Sanitize(string name) =>
            string.Concat((name ?? "document").Where(c => !Path.GetInvalidFileNameChars().Contains(c)));
    }
}
