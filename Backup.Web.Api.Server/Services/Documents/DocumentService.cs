using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Models;
using Backup.Web.Api.Server.Models.Entities;
using Microsoft.AspNetCore.Http;
using Backup.Web.Api.Server.Services;
using Backup.Web.Api.Server.Services.Numbering;
using Backup.Web.Api.Server.Services.Tenancy;
using Backup.Web.Api.Server.Services.Documents.Parsing;
using System.Text.RegularExpressions;

namespace Backup.Web.Api.Server.Services.Documents
{
    public class DocumentService : IDocumentService
    {
        private readonly IStorageBroker storageBroker;
        private readonly ITextExtractionService textExtractor;
			private readonly IPdfToTextService pdfToText;
			private readonly IDocumentParserService parser;
        private readonly IWebHostEnvironment env;
        private readonly IConfiguration config;
		private readonly Backup.Web.Api.Server.Services.Ocr.IOcrTextExtractionService ocrExtractor;
		private readonly Backup.Web.Api.Server.Services.Documents.Ollama.IOllamaParsingService ollama;
		private readonly Backup.Web.Api.Server.Services.Documents.Python.IPythonExtractorClient pythonExtractor;
        private readonly INumberingSequenceService numberingService;
        private readonly ICompanyContextService companyContext;

			public DocumentService(IStorageBroker storageBroker, ITextExtractionService textExtractor, IPdfToTextService pdfToText, IDocumentParserService parser, IWebHostEnvironment env, IConfiguration config, Backup.Web.Api.Server.Services.Ocr.IOcrTextExtractionService ocrExtractor, Backup.Web.Api.Server.Services.Documents.Ollama.IOllamaParsingService ollama, Backup.Web.Api.Server.Services.Documents.Python.IPythonExtractorClient pythonExtractor, INumberingSequenceService numberingService, ICompanyContextService companyContext)
        {
            this.storageBroker = storageBroker;
            this.textExtractor = textExtractor;
				this.pdfToText = pdfToText;
				this.parser = parser;
            this.env = env;
            this.config = config;
            this.ocrExtractor = ocrExtractor;
			this.ollama = ollama;
			this.pythonExtractor = pythonExtractor;
            this.numberingService = numberingService;
            this.companyContext = companyContext;
        }

        public IQueryable<Document> GetAll() =>
            this.storageBroker.SelectAllDocuments()
                .ForCompany(this.companyContext.GetCurrentCompanyId())
                .OrderByDescending(d => d.DateAdded);

        public IQueryable<Document> Search(string query) =>
            this.storageBroker.SearchDocuments(query)
                .ForCompany(this.companyContext.GetCurrentCompanyId())
                .OrderByDescending(d => d.DateAdded);

        public async Task<Document> UploadAsync(IFormFile file, string typeDocument, string? numero, string? client, DateTime? dateDocument, CancellationToken ct)
        {
            return await UploadAsync(file, typeDocument, numero, client, dateDocument, ct, null);
        }

        public async Task<Document> UploadAsync(IFormFile file, string typeDocument, string? numero, string? client, DateTime? dateDocument, CancellationToken ct, string? supplier)
        {
            if (file == null || file.Length == 0) throw new ArgumentException("file");

                    // Vérifier les doublons avant l'upload (par société)
            if (!string.IsNullOrWhiteSpace(numero))
            {
                var companyId = this.companyContext.GetCurrentCompanyId();
                var existing = this.storageBroker.SelectAllDocuments()
                    .ForCompany(companyId)
                    .Where(d => d.TypeDocument == typeDocument &&
                               d.Numero == numero &&
                               (string.IsNullOrWhiteSpace(supplier) || d.Supplier == supplier))
                    .FirstOrDefault();

                if (existing != null)
                {
                    throw new InvalidOperationException($"Un document avec le numéro '{numero}' de type '{typeDocument}'{(string.IsNullOrWhiteSpace(supplier) ? "" : $" du fournisseur '{supplier}'")} existe déjà (ID: {existing.Id}).");
                }
            }

            var storageRoot = this.config.GetValue<string>("Storage:RootPath") ?? "Storage";
            var absoluteRoot = Path.IsPathRooted(storageRoot) ? storageRoot : Path.Combine(this.env.ContentRootPath, storageRoot);
            Directory.CreateDirectory(absoluteRoot);

            var safeFileName = Path.GetFileName(file.FileName);
            var uniqueName = $"{Guid.NewGuid():N}_{safeFileName}";
            var absolutePath = Path.Combine(absoluteRoot, uniqueName);

            await using (var fs = new FileStream(absolutePath, FileMode.CreateNew))
            {
                await file.CopyToAsync(fs, ct);
            }

			// 0) Optional: Python extractor on the original PDF (structured JSON) - for all document types
			List<DocumentLine> pythonLines = new();
			var pyEnabled = this.config.GetValue<bool?>("PythonExtractor:Enabled") ?? false;
			if (pyEnabled)
			{
				try
				{
					pythonLines = await this.pythonExtractor.TryExtractAsync(absolutePath, ct);
				}
				catch { /* ignore */ }
			}

			// 1) First try Xpdf/Poppler pdftotext -layout for structured text
			var extracted = await this.pdfToText.TryExtractAsync(absolutePath, ct);

			// 2) If empty, try PdfPig logical extraction
			if (string.IsNullOrWhiteSpace(extracted))
			{
				extracted = await this.textExtractor.ExtractTextAsync(absolutePath, ct);
			}

			// 3) If still empty, fallback to OCR (scanned PDFs)
            if (string.IsNullOrWhiteSpace(extracted))
            {
                var lang = this.config.GetValue<string>("Ocr:Language") ?? "eng";
                extracted = await this.ocrExtractor.ExtractTextFromScannedPdfAsync(absolutePath, lang, 300, ct);
            }

            if (!string.IsNullOrWhiteSpace(extracted))
            {
                // Preserve line breaks, lightly normalize horizontal spaces only per line
                extracted = NormalizeTextPreserveLines(extracted);
            }

            var doc = new Document
            {
                TypeDocument = typeDocument,
                Numero = numero,
                Client = client,
                Supplier = supplier,
                DateDocument = dateDocument,
                OriginalFileName = safeFileName,
                FilePath = uniqueName,
                ContentText = extracted ?? string.Empty,
                DateAdded = DateTime.UtcNow
            };
            doc.EnsureCompanyId(this.companyContext.GetCurrentCompanyId());

			var saved = await this.storageBroker.InsertDocumentAsync(doc);

			// Parse and persist lines for reliable comparison/search
			List<DocumentLine> linesToPersist = new();

			// Prefer Python extractor lines if present; else deterministic parse
			if (pythonLines.Count > 0)
			{
				linesToPersist = pythonLines;
			}
			else if (!string.IsNullOrWhiteSpace(extracted))
			{
				linesToPersist = this.parser.Parse(extracted)?.ToList() ?? new List<DocumentLine>();
			}

			if (linesToPersist.Count > 0)
			{
				EnforceFieldLengths(linesToPersist);
				foreach (var l in linesToPersist) l.DocumentId = saved.Id;
				await this.storageBroker.InsertDocumentLinesAsync(linesToPersist);
			}

            await TryCreateSupplierInvoiceFromParsedDocumentAsync(saved, linesToPersist);

			return saved;
        }

        private static string NormalizeTextPreserveLines(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            var unified = text.Replace("\r\n", "\n").Replace("\r", "\n");
            var lines = unified.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                // Replace horizontal whitespace runs, keep line boundaries
                var line = Regex.Replace(lines[i], @"[^\S\r\n]+", " ");
                lines[i] = line.TrimEnd();
            }
            return string.Join("\n", lines).Trim();
        }

        public async Task<(byte[] bytes, string contentType, string downloadName)> DownloadAsync(int id)
        {
            var doc = await this.storageBroker.SelectDocumentByIdAsync(id) ?? throw new InvalidOperationException("Document not found");

            var storageRoot = this.config.GetValue<string>("Storage:RootPath") ?? "Storage";
            var absoluteRoot = Path.IsPathRooted(storageRoot) ? storageRoot : Path.Combine(this.env.ContentRootPath, storageRoot);
            var absolutePath = Path.Combine(absoluteRoot, doc.FilePath);
            if (!File.Exists(absolutePath)) throw new FileNotFoundException("File missing", absolutePath);

            var contentType = "application/octet-stream";
            if (Path.GetExtension(doc.OriginalFileName).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
                contentType = "application/pdf";

            var bytes = await File.ReadAllBytesAsync(absolutePath);
            return (bytes, contentType, doc.OriginalFileName);
        }

		public async Task<bool> ReparseDocumentLinesAsync(int documentId, bool useAiFallback, CancellationToken ct)
		{
			var doc = await this.storageBroker.SelectDocumentByIdAsync(documentId);
			if (doc == null) return false;

			// If Python extractor is enabled, prefer it at reparse time for all document types (uses original PDF)
			var pyEnabled = this.config.GetValue<bool?>("PythonExtractor:Enabled") ?? false;
			if (pyEnabled)
			{
				// Build absolute path to stored PDF
				var storageRoot = this.config.GetValue<string>("Storage:RootPath") ?? "Storage";
				var absoluteRoot = Path.IsPathRooted(storageRoot) ? storageRoot : Path.Combine(this.env.ContentRootPath, storageRoot);
				var pdfPath = Path.Combine(absoluteRoot, doc.FilePath);

				var py = await this.pythonExtractor.TryExtractAsync(pdfPath, ct);
				if (py.Count > 0)
				{
					await this.storageBroker.DeleteLinesByDocumentIdAsync(documentId);
					EnforceFieldLengths(py);
					foreach (var l in py) l.DocumentId = documentId;
					await this.storageBroker.InsertDocumentLinesAsync(py);
					return true;
				}
			}

			var text = doc.ContentText ?? string.Empty;
			if (string.IsNullOrWhiteSpace(text)) return false;

			// Deterministic parse
			var det = this.parser.Parse(text)?.ToList() ?? new List<DocumentLine>();

			// Optional AI fallback to fix zero-qty lines
			if (useAiFallback)
			{
				var ai = await this.ollama.TryParseAsync(text, ct);
				if (ai.Count > 0)
				{
					// Si le parse déterministe est clairement insuffisant et que l'IA est riche, privilégier l'IA
					if (det.Count < 5 && ai.Count >= 5)
					{
						det = ai;
					}
					else if (det.Count <= 2 && ai.Count >= 1 && ai.Count > det.Count)
					{
						// Cas BL: parse déterministe quasi vide, IA meilleure même si partielle
						det = ai;
					}
					else
					{
						var merged = MergeDeterministicWithAi(det, ai);
						det = merged;
					}
				}
			}

			// Replace existing lines
			await this.storageBroker.DeleteLinesByDocumentIdAsync(documentId);
			EnforceFieldLengths(det);
			foreach (var l in det) l.DocumentId = documentId;
			if (det.Count > 0) await this.storageBroker.InsertDocumentLinesAsync(det);
			return true;
		}

		private static List<DocumentLine> MergeDeterministicWithAi(List<DocumentLine> det, List<DocumentLine> ai)
		{
			if (det.Count == 0) return ai;
			var result = new List<DocumentLine>(det);

			// Build maps by strong keys
			string Key(DocumentLine l) => Backup.Web.Api.Server.Services.Documents.Parsing.ProductKeyHelper.GetProductKey(l);

			var aiMap = ai.GroupBy(Key, StringComparer.OrdinalIgnoreCase)
				.ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.Quantity).First(), StringComparer.OrdinalIgnoreCase);

			for (int i = 0; i < result.Count; i++)
			{
				var r = result[i];
				if (r.Quantity != 0) continue;
				var k = Key(r);
				if (aiMap.TryGetValue(k, out var aiLine))
				{
					// adopt AI quantity and, if empty, code/EAN
					r.Quantity = aiLine.Quantity;
					r.Unit = string.IsNullOrWhiteSpace(r.Unit) ? aiLine.Unit : r.Unit;
					if (string.IsNullOrWhiteSpace(r.ProductCode)) r.ProductCode = aiLine.ProductCode;
					if (string.IsNullOrWhiteSpace(r.Ean)) r.Ean = aiLine.Ean;
					if (string.IsNullOrWhiteSpace(r.Product)) r.Product = aiLine.Product;
				}
			}

			// Also add AI-only lines that don't exist in det
			var detKeys = new HashSet<string>(result.Select(Key), StringComparer.OrdinalIgnoreCase);
			foreach (var a in ai)
			{
				var ak = Key(a);
				if (!detKeys.Contains(ak))
				{
					result.Add(a);
				}
			}

			return result;
		}

		private static void EnforceFieldLengths(List<DocumentLine> lines)
		{
			if (lines == null) return;
			static string Trunc(string? s, int max) => string.IsNullOrEmpty(s) ? (s ?? string.Empty) : (s.Length <= max ? s : s.Substring(0, max));
			foreach (var l in lines)
			{
				l.Product = Trunc(l.Product, 255); // safe for existing DBs
				l.ProductCode = Trunc(l.ProductCode, 128);
				if (!string.IsNullOrWhiteSpace(l.Ean) && l.Ean!.Length > 13) l.Ean = l.Ean.Substring(0, 13);
				l.Unit = Trunc(l.Unit, 16);
				l.RawLine = Trunc(l.RawLine, 2048);
			}
		}

        private async Task TryCreateSupplierInvoiceFromParsedDocumentAsync(Document document, List<DocumentLine> lines)
        {
            if (!IsSupplierInvoiceDocument(document.TypeDocument)) return;
            if (string.IsNullOrWhiteSpace(document.Supplier)) return;

            var normalizedSupplierName = document.Supplier.Trim();
            var supplier = this.storageBroker.SelectAllSuppliers()
                .FirstOrDefault(s => s.Name.ToLower() == normalizedSupplierName.ToLower());

            if (supplier == null)
            {
                supplier = await this.storageBroker.InsertSupplierAsync(new Supplier
                {
                    SupplierCode = "SUP-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
                    Name = normalizedSupplierName,
                    CompanyId = null,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }

            SupplierInvoiceEntity? existingInvoice = null;
            if (!string.IsNullOrWhiteSpace(document.Numero))
            {
                var normalizedNumber = document.Numero.Trim().ToLowerInvariant();
                existingInvoice = this.storageBroker.SelectAllSupplierInvoices()
                    .FirstOrDefault(i => i.SupplierId == supplier.Id && i.InvoiceNumber.ToLower() == normalizedNumber);
            }

            if (existingInvoice != null)
            {
                if (!existingInvoice.DocumentId.HasValue)
                {
                    existingInvoice.DocumentId = document.Id;
                    existingInvoice.Date = existingInvoice.Date == default ? (document.DateDocument ?? DateTime.UtcNow) : existingInvoice.Date;
                    existingInvoice.Notes = AppendOriginNote(existingInvoice.Notes, document);
                    if (existingInvoice.Lines == null || existingInvoice.Lines.Count == 0)
                    {
                        existingInvoice.Lines = MapSupplierInvoiceLines(lines);
                    }

                    NormalizeSupplierInvoice(existingInvoice);
                    await this.storageBroker.UpdateSupplierInvoiceAsync(existingInvoice);
                }

                return;
            }

            var createdInvoice = new SupplierInvoiceEntity
            {
                SupplierId = supplier.Id,
                DocumentId = document.Id,
                InvoiceNumber = !string.IsNullOrWhiteSpace(document.Numero)
                    ? document.Numero.Trim()
                    : await this.numberingService.GetNextNumberAsync("SupplierInvoice", null),
                Date = document.DateDocument ?? DateTime.UtcNow,
                DueDate = (document.DateDocument ?? DateTime.UtcNow).AddDays(30),
                Status = "Draft",
                Notes = BuildOriginNote(document),
                CreatedAt = DateTime.UtcNow,
                Lines = MapSupplierInvoiceLines(lines)
            };

            NormalizeSupplierInvoice(createdInvoice);
            await this.storageBroker.InsertSupplierInvoiceAsync(createdInvoice);
        }

        private static bool IsSupplierInvoiceDocument(string? typeDocument) =>
            !string.IsNullOrWhiteSpace(typeDocument) &&
            typeDocument.Contains("facture", StringComparison.OrdinalIgnoreCase);

        private static List<SupplierInvoiceLineEntity> MapSupplierInvoiceLines(IEnumerable<DocumentLine> lines)
        {
            return lines.Select((line, index) =>
            {
                var quantity = line.Quantity == 0 ? 1m : line.Quantity;
                var unitPrice = line.UnitPrice;
                var totalHt = line.TotalValue != 0 ? line.TotalValue : quantity * unitPrice;
                if (unitPrice == 0 && quantity != 0 && totalHt != 0)
                {
                    unitPrice = totalHt / quantity;
                }

                return new SupplierInvoiceLineEntity
                {
                    ProductKey = line.ProductCode ?? string.Empty,
                    Description = string.IsNullOrWhiteSpace(line.Product) ? (line.RawLine ?? string.Empty) : line.Product,
                    Quantity = quantity,
                    UnitPrice = unitPrice,
                    VatRate = 21.0m,
                    TotalHT = totalHt,
                    TotalTTC = totalHt * 1.21m,
                    LineNumber = line.LineNumber > 0 ? line.LineNumber : index + 1
                };
            }).ToList();
        }

        private static void NormalizeSupplierInvoice(SupplierInvoiceEntity invoice)
        {
            invoice.Lines ??= new List<SupplierInvoiceLineEntity>();
            invoice.Status = string.IsNullOrWhiteSpace(invoice.Status) ? "Draft" : invoice.Status.Trim();
            invoice.Date = invoice.Date == default ? DateTime.UtcNow : invoice.Date;
            invoice.DueDate = invoice.DueDate == default ? invoice.Date.AddDays(30) : invoice.DueDate;
            invoice.CreatedAt = invoice.CreatedAt == default ? DateTime.UtcNow : invoice.CreatedAt;

            for (int i = 0; i < invoice.Lines.Count; i++)
            {
                var line = invoice.Lines[i];
                line.LineNumber = line.LineNumber <= 0 ? i + 1 : line.LineNumber;
                line.Description = line.Description?.Trim() ?? string.Empty;
                line.ProductKey = line.ProductKey?.Trim() ?? string.Empty;
                line.TotalHT = line.Quantity * line.UnitPrice;
                line.TotalTTC = line.TotalHT * (1 + (line.VatRate / 100m));
            }

            invoice.TotalHT = invoice.Lines.Sum(l => l.TotalHT);
            invoice.TotalVat = invoice.Lines.Sum(l => l.TotalTTC - l.TotalHT);
            invoice.TotalTTC = invoice.Lines.Sum(l => l.TotalTTC);
        }

        private static string BuildOriginNote(Document document)
        {
            var parts = new List<string> { $"Auto-created from parsed document #{document.Id}" };

            if (!string.IsNullOrWhiteSpace(document.OriginalFileName))
            {
                parts.Add(document.OriginalFileName);
            }

            return string.Join(" | ", parts);
        }

        private static string AppendOriginNote(string? existingNotes, Document document)
        {
            var note = BuildOriginNote(document);
            if (string.IsNullOrWhiteSpace(existingNotes)) return note;
            if (existingNotes.Contains(note, StringComparison.OrdinalIgnoreCase)) return existingNotes;
            return $"{existingNotes}{Environment.NewLine}{note}";
        }

        public async Task<DocumentMetadata> InspectAsync(IFormFile file, CancellationToken ct)
        {
            if (file == null || file.Length == 0) throw new ArgumentException("file");

            // Persist temporarily alongside storage root, then delete
            var storageRoot = this.config.GetValue<string>("Storage:RootPath") ?? "Storage";
            var absoluteRoot = Path.IsPathRooted(storageRoot) ? storageRoot : Path.Combine(this.env.ContentRootPath, storageRoot);
            Directory.CreateDirectory(absoluteRoot);

            var safeFileName = Path.GetFileName(file.FileName);
            var tempName = $"{Guid.NewGuid():N}_{safeFileName}";
            var tempPath = Path.Combine(absoluteRoot, tempName);

            try
            {
                await using (var fs = new FileStream(tempPath, FileMode.CreateNew))
                {
                    await file.CopyToAsync(fs, ct);
                }

                // 0) If Python extractor is enabled, ask it for metadata first
                string? typeFromPython = null;
                string? numeroFromPython = null;
                string? clientFromPython = null;
                DateTime? dateFromPython = null;
                string? supplierFromPython = null;
                var pyEnabled = this.config.GetValue<bool?>("PythonExtractor:Enabled") ?? false;
                if (pyEnabled)
                {
                    try
                    {
                        var pyMeta = await this.pythonExtractor.InspectMetadataAsync(tempPath, ct);
                        if (pyMeta != null)
                        {
                            typeFromPython = pyMeta.TypeDocument;
                            numeroFromPython = pyMeta.Numero;
                            clientFromPython = pyMeta.Client;
                            dateFromPython = pyMeta.DateDocument;
                            supplierFromPython = pyMeta.Supplier;
                            
                            // DEBUG: Log what we got from Python
                            System.Diagnostics.Debug.WriteLine($"[DocumentService] Python metadata received:");
                            System.Diagnostics.Debug.WriteLine($"  Type: '{typeFromPython}'");
                            System.Diagnostics.Debug.WriteLine($"  Numero: '{numeroFromPython}'");
                            System.Diagnostics.Debug.WriteLine($"  Client: '{clientFromPython}'");
                            System.Diagnostics.Debug.WriteLine($"  Date: '{dateFromPython}'");
                            System.Diagnostics.Debug.WriteLine($"  Supplier: '{supplierFromPython}'");
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine("[DocumentService] Python metadata is NULL!");
                        }
                    }
                    catch (Exception ex) 
                    { 
                        System.Diagnostics.Debug.WriteLine($"[DocumentService] Python extractor error: {ex.Message}");
                        System.Diagnostics.Debug.WriteLine($"[DocumentService] Stack trace: {ex.StackTrace}");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[DocumentService] Python extractor is DISABLED");
                }

                // Try pdftotext first, then PdfPig, then OCR
                var extracted = await this.pdfToText.TryExtractAsync(tempPath, ct);
                if (string.IsNullOrWhiteSpace(extracted))
                {
                    extracted = await this.textExtractor.ExtractTextAsync(tempPath, ct);
                }
                if (string.IsNullOrWhiteSpace(extracted))
                {
                    var lang = this.config.GetValue<string>("Ocr:Language") ?? "eng";
                    extracted = await this.ocrExtractor.ExtractTextFromScannedPdfAsync(tempPath, lang, 300, ct);
                }

                extracted = NormalizeTextPreserveLines(extracted ?? string.Empty);
                
                // PRIORISER les valeurs Python si disponibles (surtout pour FF GROUP)
                System.Diagnostics.Debug.WriteLine($"[DocumentService] Vérification FF GROUP - supplierFromPython: '{supplierFromPython}'");
                if (!string.IsNullOrWhiteSpace(supplierFromPython) && supplierFromPython.Trim().Equals("FF GROUP", StringComparison.OrdinalIgnoreCase))
                {
                    // Pour FF GROUP, utiliser UNIQUEMENT les valeurs Python
                    System.Diagnostics.Debug.WriteLine($"[DocumentService] FF GROUP détecté - Utilisation UNIQUEMENT des valeurs Python");
                    System.Diagnostics.Debug.WriteLine($"[DocumentService] clientFromPython avant assignation: '{clientFromPython}'");
                    
                    var metaffgroup = new DocumentMetadata();
                    if (!string.IsNullOrWhiteSpace(typeFromPython)) metaffgroup.TypeDocument = typeFromPython;
                    if (!string.IsNullOrWhiteSpace(numeroFromPython)) metaffgroup.Numero = numeroFromPython;
                    if (!string.IsNullOrWhiteSpace(clientFromPython)) 
                    {
                        metaffgroup.Client = clientFromPython;
                        System.Diagnostics.Debug.WriteLine($"[DocumentService] Client assigné: '{metaffgroup.Client}'");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[DocumentService] clientFromPython est NULL ou vide!");
                    }
                    if (dateFromPython != null) metaffgroup.DateDocument = dateFromPython;
                    if (!string.IsNullOrWhiteSpace(supplierFromPython)) metaffgroup.Supplier = supplierFromPython;
                    
                    System.Diagnostics.Debug.WriteLine($"[DocumentService] Retour FF GROUP - Client final: '{metaffgroup.Client}'");
                    return metaffgroup;
                }
                
                // Pour les autres fournisseurs, utiliser la logique générique puis surcharger avec Python
                var meta = ExtractMetadataFromText(extracted);
                // Overlay Python metadata when available
                if (!string.IsNullOrWhiteSpace(typeFromPython)) meta.TypeDocument = typeFromPython;
                if (!string.IsNullOrWhiteSpace(numeroFromPython)) meta.Numero = numeroFromPython;
                if (!string.IsNullOrWhiteSpace(clientFromPython)) meta.Client = clientFromPython;
                if (dateFromPython != null) meta.DateDocument = dateFromPython;
                if (!string.IsNullOrWhiteSpace(supplierFromPython)) meta.Supplier = supplierFromPython;
                return meta;
            }
            finally
            {
                try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { /* ignore */ }
            }
        }

        private static DocumentMetadata ExtractMetadataFromText(string text)
        {
            var meta = new DocumentMetadata();
            if (string.IsNullOrWhiteSpace(text))
            {
                meta.TypeDocument = "Facture";
                return meta;
            }

            // Decide type
            var lower = text.ToLowerInvariant();
            if (lower.Contains("bon de livraison") || lower.Contains("delivery note") || lower.Contains("bonlivraison") || lower.Contains("bon livraison"))
                meta.TypeDocument = "BonLivraison";
            else if (lower.Contains("facture") || lower.Contains("invoice") || lower.Contains("factuur"))
                meta.TypeDocument = "Facture";
            else
                meta.TypeDocument = "Facture";

            // Numero
            var numeroPatterns = new[]
            {
                @"\b(?:N[°o]|No|Num[eé]ro|Invoice|Facture|BL|Bon)\s*[:#]?\s*([A-Z0-9][A-Z0-9\-\/\.]{2,})",
                @"\b(?:Invoice\s*No\.?|Invoice\s*#)\s*([A-Z0-9][A-Z0-9\-\/\.]{2,})",
            };
            foreach (var p in numeroPatterns)
            {
                var m = Regex.Match(text, p, RegexOptions.IgnoreCase);
                if (m.Success) { meta.Numero = m.Groups[1].Value.Trim(); break; }
            }

            // Date
            var date = TryExtractDate(text);
            if (date != null) meta.DateDocument = date;

            // Client (very heuristic): first non-empty line following a 'Client'/'Customer'/'Facturé à' label
            var clientLabel = Regex.Match(text, @"(?:(Client|Customer|Factur[eé]\s*à|Billed\s*to|Bill\s*to)\s*[:\-]?\s*)(.+)", RegexOptions.IgnoreCase);
            if (clientLabel.Success)
            {
                var val = clientLabel.Groups[2].Value.Trim();
                // Stop at obvious separators
                val = Regex.Replace(val, @"\s{2,}.*$", "");
                if (!string.IsNullOrWhiteSpace(val)) meta.Client = val;
            }
            else
            {
                // Fallback: take a likely company name near top
                var lines = text.Split('\n').Select(l => l.Trim()).Where(l => !string.IsNullOrWhiteSpace(l)).Take(30).ToList();
                var candidate = lines.FirstOrDefault(l => Regex.IsMatch(l, @"[A-Za-z]{3,}\s+[A-Za-z]{3,}"));
                meta.Client = candidate;
            }

            // Supplier (optional): look for known suppliers
            if (lower.Contains("ff group")) meta.Supplier = "FF GROUP";
            if (lower.Contains("knauf")) meta.Supplier = "Knauf";
            if (lower.Contains("bobrush") || lower.Contains("bobruche")) meta.Supplier = "Bobrush";
            if (lower.Contains("sadu abrasives") || lower.Contains("info@sadu.be")) meta.Supplier = "Sadu";
            if (lower.Contains("xenex") || lower.Contains("info@xenex.be")) meta.Supplier = "Xenex";
            if (lower.Contains("rectavit") || lower.Contains("info@rectavit.be")) meta.Supplier = "Rectavit";

            return meta;
        }

        private static DateTime? TryExtractDate(string text)
        {
            // Common formats: dd/MM/yyyy, dd-MM-yyyy, yyyy-MM-dd
            var patterns = new[]
            {
                @"\b(\d{2})[\/\-\.](\d{2})[\/\-\.](\d{4})\b",
                @"\b(\d{4})[\/\-\.](\d{2})[\/\-\.](\d{2})\b"
            };
            foreach (var p in patterns)
            {
                var m = Regex.Match(text, p);
                if (!m.Success) continue;
                try
                {
                    if (m.Groups.Count == 4 && m.Groups[1].Value.Length == 2) // dd sep MM sep yyyy
                    {
                        var d = int.Parse(m.Groups[1].Value);
                        var mo = int.Parse(m.Groups[2].Value);
                        var y = int.Parse(m.Groups[3].Value);
                        return new DateTime(y, mo, d);
                    }
                    if (m.Groups.Count == 4 && m.Groups[1].Value.Length == 4) // yyyy sep MM sep dd
                    {
                        var y = int.Parse(m.Groups[1].Value);
                        var mo = int.Parse(m.Groups[2].Value);
                        var d = int.Parse(m.Groups[3].Value);
                        return new DateTime(y, mo, d);
                    }
                }
                catch { /* ignore bad dates */ }
            }
            return null;
        }
    }
}


