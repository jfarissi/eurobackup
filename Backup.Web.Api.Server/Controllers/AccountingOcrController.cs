using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Authorization;
using Authorize = Microsoft.AspNetCore.Authorization.AuthorizeAttribute;
using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Models.Security;
using Backup.Web.Api.Server.Services.Accounting;
using Backup.Web.Api.Server.Services.Documents;
using Backup.Web.Api.Server.Services.Documents.Python;
using Backup.Web.Api.Server.Services.Numbering;
using Backup.Web.Api.Server.Services.Ocr;
using Backup.Web.Api.Server.Services.Sales;
using Backup.Web.Api.Server.Services.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RESTFulSense.Controllers;
using UglyToad.PdfPig;

namespace Backup.Web.Api.Server.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/accounting-ocr")]
    [RequestFormLimits(MultipartBodyLengthLimit = 20_000_000)]
    public class AccountingOcrController : RESTFulController
    {
        private static readonly string[] ImageExtensions = [".png", ".jpg", ".jpeg", ".webp", ".tif", ".tiff", ".bmp", ".gif"];
        private readonly IStorageBroker storage;
        private readonly ICompanyContextService companyContext;
        private readonly IOcrTextExtractionService ocr;
        private readonly IDocumentParserService documentParser;
        private readonly INumberingSequenceService numbering;
        private readonly IPythonExtractorClient pythonExtractor;

        public AccountingOcrController(
            IStorageBroker storage,
            ICompanyContextService companyContext,
            IOcrTextExtractionService ocr,
            IDocumentParserService documentParser,
            INumberingSequenceService numbering,
            IPythonExtractorClient pythonExtractor)
        {
            this.storage = storage;
            this.companyContext = companyContext;
            this.ocr = ocr;
            this.documentParser = documentParser;
            this.numbering = numbering;
            this.pythonExtractor = pythonExtractor;
        }

        public class TextRequest
        {
            public string? Text { get; set; }
            public string? Bank { get; set; }
            public string? AccountCode { get; set; }
            public string? FileName { get; set; }
            public string? Hint { get; set; }
        }

        [HttpPost("extract")]
        [RequirePermission(Permissions.AccountingRead)]
        public async Task<IActionResult> Extract([FromBody] TextRequest body, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(body?.Text)) return BadRequest("Texte requis.");
            var python = await this.pythonExtractor.TryAccountingExtractTextAsync(
                body.Text, body.FileName, body.Hint, ct);
            if (python != null) return Ok(python);
            return Ok(AccountingOcrInvoiceImport.FromLocal(body.Text, body.FileName, this.documentParser, body.Hint));
        }

        [HttpPost("extract/file")]
        [RequirePermission(Permissions.AccountingRead)]
        [RequestSizeLimit(20_000_000)]
        public async Task<IActionResult> ExtractFile(IFormFile file, [FromForm] string? hint, CancellationToken ct)
        {
            if (file == null || file.Length == 0) return BadRequest("Fichier requis.");
            await using var buffer = new MemoryStream();
            await file.CopyToAsync(buffer, ct);
            var bytes = buffer.ToArray();

            var python = await this.pythonExtractor.TryAccountingExtractAsync(bytes, file.FileName, hint, ct);
            if (python != null) return Ok(python);

            var (text, error) = await ExtractBytesAsync(bytes, file.FileName, ct);
            if (error != null) return BadRequest(error);
            return Ok(AccountingOcrInvoiceImport.FromLocal(text!, file.FileName, this.documentParser, hint));
        }

        [HttpPost("invoice")]
        [RequirePermission(Permissions.AccountingRead)]
        public IActionResult Invoice([FromBody] TextRequest body)
        {
            if (string.IsNullOrWhiteSpace(body?.Text)) return BadRequest("Texte requis.");
            return Ok(AccountingOcrInvoiceImport.Preview(body.Text, this.documentParser));
        }

        [HttpPost("invoice/file")]
        [RequirePermission(Permissions.AccountingRead)]
        [RequestSizeLimit(20_000_000)]
        public async Task<IActionResult> InvoiceFile(IFormFile file, CancellationToken ct)
        {
            var (text, error) = await ExtractUploadAsync(file, ct);
            if (error != null) return BadRequest(error);
            return Ok(AccountingOcrInvoiceImport.Preview(text!, this.documentParser));
        }

        [HttpPost("invoice/import")]
        [RequirePermission(Permissions.AccountingCreate)]
        public async Task<IActionResult> ImportInvoice([FromBody] TextRequest body)
        {
            if (string.IsNullOrWhiteSpace(body?.Text)) return BadRequest("Texte requis.");
            return await ImportInvoiceFromText(body.Text);
        }

        [HttpPost("invoice/import/file")]
        [RequirePermission(Permissions.AccountingCreate)]
        [RequestSizeLimit(20_000_000)]
        public async Task<IActionResult> ImportInvoiceFile(IFormFile file, CancellationToken ct)
        {
            var (text, error) = await ExtractUploadAsync(file, ct);
            if (error != null) return BadRequest(error);
            return await ImportInvoiceFromText(text!);
        }

        [HttpPost("bank-statement")]
        [RequirePermission(Permissions.AccountingRead)]
        public IActionResult BankStatement([FromBody] TextRequest body)
        {
            if (string.IsNullOrWhiteSpace(body?.Text)) return BadRequest("Texte requis.");
            return BankFromText(body.Text, body.Bank, body.FileName);
        }

        [HttpPost("bank-statement/file")]
        [RequirePermission(Permissions.AccountingRead)]
        [RequestSizeLimit(20_000_000)]
        public async Task<IActionResult> BankStatementFile(
            IFormFile file, [FromForm] string? bank, CancellationToken ct)
        {
            var (text, error) = await ExtractUploadAsync(file, ct);
            if (error != null) return BadRequest(error);
            return BankFromText(text!, bank, file.FileName);
        }

        [HttpPost("bank-statement/import")]
        [RequirePermission(Permissions.AccountingCreate)]
        public async Task<IActionResult> ImportBankStatement([FromBody] TextRequest body)
        {
            if (string.IsNullOrWhiteSpace(body?.Text)) return BadRequest("Texte requis.");
            return await ImportFromText(body.Text, body.FileName, body.AccountCode);
        }

        [HttpPost("bank-statement/import/file")]
        [RequirePermission(Permissions.AccountingCreate)]
        [RequestSizeLimit(20_000_000)]
        public async Task<IActionResult> ImportBankStatementFile(
            IFormFile file, [FromForm] string? accountCode, CancellationToken ct)
        {
            var (text, error) = await ExtractUploadAsync(file, ct);
            if (error != null) return BadRequest(error);
            return await ImportFromText(text!, file.FileName, accountCode);
        }

        private IActionResult BankFromText(string text, string? bank, string? fileName)
        {
            try
            {
                var lines = MoroccanDocumentParser.ParseBankStatement(text, bank ?? fileName);
                return Ok(new
                {
                    bank = BankStatementImport.DetectBank(fileName, text),
                    lines
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        private async Task<IActionResult> ImportInvoiceFromText(string text)
        {
            var (dto, error) = await AccountingOcrInvoiceImport.ImportAsync(
                this.storage,
                this.numbering,
                this.documentParser,
                this.companyContext.GetCurrentCompanyId(),
                text,
                SalesDocumentAudit.ActorFrom(User));
            if (error != null) return BadRequest(error);
            return Ok(dto);
        }

        private async Task<IActionResult> ImportFromText(string text, string? fileName, string? accountCode)
        {
            try
            {
                var lines = MoroccanDocumentParser.ParseBankStatement(text, fileName);
                var csv = "Date;Libelle;Reference;Debit;Credit;Solde\n" + string.Join('\n',
                    lines.Select(l => $"{l.OperationDate:dd/MM/yyyy};{l.Label};{l.Reference};{l.Debit};{l.Credit};{l.RunningBalance}"));
                var (dto, error) = await BankReconciliationService.ImportAsync(
                    this.storage,
                    this.companyContext.GetCurrentCompanyId(),
                    csv,
                    fileName ?? "ocr.txt",
                    accountCode,
                    SalesDocumentAudit.ActorFrom(User));
                if (error != null) return BadRequest(error);
                return Ok(dto);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        private async Task<(string? Text, string? Error)> ExtractUploadAsync(IFormFile? file, CancellationToken ct)
        {
            if (file == null || file.Length == 0) return (null, "Fichier requis.");
            await using var buffer = new MemoryStream();
            await file.CopyToAsync(buffer, ct);
            return await ExtractBytesAsync(buffer.ToArray(), file.FileName, ct);
        }

        private async Task<(string? Text, string? Error)> ExtractBytesAsync(byte[] bytes, string? fileName, CancellationToken ct)
        {
            var ext = Path.GetExtension(fileName ?? "").ToLowerInvariant();

            if (ImageExtensions.Contains(ext))
            {
                var ocrText = await this.ocr.ExtractTextFromImageAsync(bytes, "fra+ara", 300, ct);
                return string.IsNullOrWhiteSpace(ocrText)
                    ? (null, "OCR : aucun texte reconnu sur l'image. Installez Tesseract (fra, ara).")
                    : (ocrText, null);
            }

            if (ext == ".pdf")
            {
                var tmp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".pdf");
                await System.IO.File.WriteAllBytesAsync(tmp, bytes, ct);
                try
                {
                    var layered = ReadPdfTextLayer(tmp);
                    if (!string.IsNullOrWhiteSpace(layered) && layered.Trim().Length >= 40)
                        return (layered, null);
                    var ocrText = await this.ocr.ExtractTextFromScannedPdfAsync(tmp, "fra+ara", 300, ct);
                    var combined = string.Join("\n", new[] { layered, ocrText }.Where(s => !string.IsNullOrWhiteSpace(s)));
                    return string.IsNullOrWhiteSpace(combined)
                        ? (null, "PDF : aucun texte reconnu.")
                        : (combined, null);
                }
                finally
                {
                    try { System.IO.File.Delete(tmp); } catch { /* ignore */ }
                }
            }

            return (Encoding.UTF8.GetString(bytes), null);
        }

        private static string ReadPdfTextLayer(string path)
        {
            try
            {
                using var document = PdfDocument.Open(path);
                var sb = new StringBuilder();
                foreach (var page in document.GetPages())
                    sb.AppendLine(page.Text);
                return sb.ToString();
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
