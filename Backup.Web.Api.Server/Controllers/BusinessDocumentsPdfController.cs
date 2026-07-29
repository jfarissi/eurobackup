using System.Threading.Tasks;
using Backup.Web.Api.Server.Authorization;
using Authorize = Microsoft.AspNetCore.Authorization.AuthorizeAttribute;
using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Models.Security;
using Backup.Web.Api.Server.Services.BusinessPdf;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RESTFulSense.Controllers;

namespace Backup.Web.Api.Server.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/business-documents")]
    public class BusinessDocumentsPdfController : RESTFulController
    {
        private readonly IStorageBroker storage;
        private readonly IBusinessDocumentPdfService pdfService;

        public BusinessDocumentsPdfController(IStorageBroker storage, IBusinessDocumentPdfService pdfService)
        {
            this.storage = storage;
            this.pdfService = pdfService;
        }

        [HttpGet("quotes/{id:int}/pdf")]
        [RequirePermission(Permissions.QuoteRead)]
        public async Task<IActionResult> QuotePdf(int id)
        {
            var quote = await this.storage.SelectQuoteByIdAsync(id);
            if (quote == null) return NotFound("Devis introuvable");
            var bytes = this.pdfService.GenerateQuotePdf(quote);
            return File(bytes, "application/pdf", $"{Sanitize(quote.QuoteNumber)}.pdf");
        }

        [HttpGet("sales-orders/{id:int}/pdf")]
        [RequirePermission(Permissions.OrderRead)]
        public async Task<IActionResult> SalesOrderPdf(int id)
        {
            var order = await this.storage.SelectSalesOrderByIdAsync(id);
            if (order == null) return NotFound("Commande introuvable");
            var bytes = this.pdfService.GenerateSalesOrderPdf(order);
            return File(bytes, "application/pdf", $"{Sanitize(order.OrderNumber)}.pdf");
        }

        [HttpGet("sales-invoices/{id:int}/pdf")]
        [RequirePermission(Permissions.InvoiceRead)]
        public async Task<IActionResult> SalesInvoicePdf(int id)
        {
            var invoice = await this.storage.SelectSalesInvoiceByIdAsync(id);
            if (invoice == null) return NotFound("Facture introuvable");
            var bytes = this.pdfService.GenerateSalesInvoicePdf(invoice);
            return File(bytes, "application/pdf", $"{Sanitize(invoice.InvoiceNumber)}.pdf");
        }

        [HttpGet("credit-notes/{id:int}/pdf")]
        [RequirePermission(Permissions.InvoiceRead)]
        public async Task<IActionResult> CreditNotePdf(int id)
        {
            var creditNote = await this.storage.SelectCreditNoteByIdAsync(id);
            if (creditNote == null) return NotFound("Avoir introuvable");
            var bytes = this.pdfService.GenerateCreditNotePdf(creditNote);
            return File(bytes, "application/pdf", $"{Sanitize(creditNote.CreditNoteNumber)}.pdf");
        }

        [HttpGet("sales-delivery-notes/{id:int}/pdf")]
        [RequirePermission(Permissions.DeliveryNoteRead)]
        public async Task<IActionResult> SalesDeliveryNotePdf(int id)
        {
            var note = await this.storage.SelectSalesDeliveryNoteByIdAsync(id);
            if (note == null) return NotFound("Bon de livraison introuvable");
            var bytes = this.pdfService.GenerateSalesDeliveryNotePdf(note);
            return File(bytes, "application/pdf", $"{Sanitize(note.DeliveryNumber)}.pdf");
        }

        [HttpGet("purchase-orders/{id:int}/pdf")]
        [RequirePermission(Permissions.PurchaseOrderRead)]
        public async Task<IActionResult> PurchaseOrderPdf(int id)
        {
            var order = await this.storage.SelectPurchaseOrderByIdAsync(id);
            if (order == null) return NotFound("Commande fournisseur introuvable");
            var bytes = this.pdfService.GeneratePurchaseOrderPdf(order);
            return File(bytes, "application/pdf", $"{Sanitize(order.OrderNumber)}.pdf");
        }

        [HttpGet("supplier-invoices/{id:int}/pdf")]
        [RequirePermission(Permissions.SupplierInvoiceRead)]
        public async Task<IActionResult> SupplierInvoicePdf(int id)
        {
            var invoice = await this.storage.SelectSupplierInvoiceByIdAsync(id);
            if (invoice == null) return NotFound("Facture fournisseur introuvable");
            var bytes = this.pdfService.GenerateSupplierInvoicePdf(invoice);
            return File(bytes, "application/pdf", $"{Sanitize(invoice.InvoiceNumber)}.pdf");
        }

        private static string Sanitize(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "document";
            foreach (var c in System.IO.Path.GetInvalidFileNameChars())
            {
                value = value.Replace(c, '_');
            }
            return value.Trim();
        }
    }
}
