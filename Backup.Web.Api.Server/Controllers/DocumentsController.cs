using Backup.Web.Api.Server.Models;
using Backup.Web.Api.Server.Services;
using Backup.Web.Api.Server.Services.Documents;
using Microsoft.AspNetCore.Mvc;
using RESTFulSense.Controllers;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace Backup.Web.Api.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DocumentsController : RESTFulController
    {
        private readonly IDocumentService documentService;
        private readonly Backup.Web.Api.Server.Services.Documents.IDocumentComparisonService comparisonService;

        public DocumentsController(IDocumentService documentService, Backup.Web.Api.Server.Services.Documents.IDocumentComparisonService comparisonService)
        {
            this.documentService = documentService;
            this.comparisonService = comparisonService;
        }

        [HttpGet]
        public IActionResult GetAll()
        {
            return Ok(this.documentService.GetAll().Take(200).ToList());
        }

        [HttpGet("search")]
        public IActionResult Search([FromQuery] string q)
        {
            if (string.IsNullOrWhiteSpace(q)) return BadRequest("Query required");
            q = q.Trim();
            return Ok(this.documentService.Search(q).Take(200).ToList());
        }

        [HttpGet("find-invoices-by-bl-number")]
        public IActionResult FindInvoicesByBlNumber([FromQuery] string blNumber, [FromServices] Backup.Web.Api.Server.Brokers.Storage.IStorageBroker storage)
        {
            if (string.IsNullOrWhiteSpace(blNumber)) return BadRequest("BL number required");
            blNumber = blNumber.Trim();
            
            // Charger toutes les factures en mémoire pour éviter les problèmes de traduction SQL
            var allInvoices = storage.SelectAllDocuments()
                .ToList()
                .Where(d => d.TypeDocument == "Facture" || 
                           (d.TypeDocument != null && d.TypeDocument.Contains("Facture", StringComparison.OrdinalIgnoreCase)))
                .ToList();
            
            // Filtrer celles qui contiennent le numéro du BL dans leur ContentText
            var matchingInvoices = allInvoices
                .Where(inv => !string.IsNullOrWhiteSpace(inv.ContentText) && 
                             inv.ContentText.Contains(blNumber, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(inv => inv.DateAdded)
                .Take(50)
                .ToList();
            
            return Ok(matchingInvoices);
        }

        public class UploadRequest
        {
            public IFormFile File { get; set; } = default!;
            public string TypeDocument { get; set; } = string.Empty;
            public string? Numero { get; set; }
            public string? Client { get; set; }
            public string? Supplier { get; set; }
            public DateTime? DateDocument { get; set; }
        }

        [HttpPost("upload")]
        [RequestSizeLimit(50_000_000)] // 50 MB
        public async Task<IActionResult> Upload([FromForm] UploadRequest request, CancellationToken ct)
        {
            if (request.File == null || request.File.Length == 0) return BadRequest("Fichier manquant");
            
            try
            {
                var doc = await this.documentService.UploadAsync(request.File, request.TypeDocument, request.Numero, request.Client, request.DateDocument, ct, request.Supplier);
                return Ok(doc);
            }
            catch (InvalidOperationException ex)
            {
                // Document en double
                return Conflict(new { error = ex.Message, isDuplicate = true });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        public class InspectResponse
        {
            public string TypeDocument { get; set; } = string.Empty;
            public string? Numero { get; set; }
            public string? Client { get; set; }
            public DateTime? DateDocument { get; set; }
            public string? Supplier { get; set; }
        }

        [HttpPost("inspect")]
        [RequestSizeLimit(50_000_000)]
        public async Task<IActionResult> Inspect([FromForm] IFormFile file, CancellationToken ct)
        {
            if (file == null || file.Length == 0) return BadRequest("Fichier manquant");
            var meta = await this.documentService.InspectAsync(file, ct);
            return Ok(new InspectResponse
            {
                TypeDocument = meta.TypeDocument,
                Numero = meta.Numero,
                Client = meta.Client,
                DateDocument = meta.DateDocument,
                Supplier = meta.Supplier
            });
        }

        [HttpGet("{id:int}/download")]
        public async Task<IActionResult> Download(int id)
        {
            var result = await this.documentService.DownloadAsync(id);
            return File(result.bytes, result.contentType, result.downloadName);
        }

        [HttpPost("compare")]
        public async Task<IActionResult> Compare([FromQuery] int invoiceId, [FromQuery] int deliveryId, CancellationToken ct)
        {
            if (invoiceId <= 0 || deliveryId <= 0) return BadRequest("Ids required");
            var result = await this.comparisonService.CompareAsync(invoiceId, deliveryId, ct);
            return Ok(result);
        }

        [HttpPost("compare-all-deliveries")]
        public async Task<IActionResult> CompareAllDeliveries([FromQuery] int invoiceId, CancellationToken ct)
        {
            if (invoiceId <= 0) return BadRequest("InvoiceId required");
            var result = await this.comparisonService.CompareAllDeliveriesAsync(invoiceId, ct);
            return Ok(result);
        }

        [HttpPost("compare-invoices")]
        public async Task<IActionResult> CompareInvoices([FromQuery] int invoice1Id, [FromQuery] int invoice2Id, CancellationToken ct)
        {
            if (invoice1Id <= 0 || invoice2Id <= 0) return BadRequest("Invoice IDs required");
            try
            {
                var result = await this.comparisonService.CompareInvoicesAsync(invoice1Id, invoice2Id, ct);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        public class LinkRequest
        {
            public int InvoiceId { get; set; }
            public int DeliveryId { get; set; }
        }

        [HttpPost("link")]
        public async Task<IActionResult> Link([FromBody] LinkRequest request, [FromServices] Backup.Web.Api.Server.Brokers.Storage.IStorageBroker storage)
        {
            if (request.InvoiceId <= 0 || request.DeliveryId <= 0) return BadRequest("Ids required");
            
            try
            {
                // Prevent duplicate - vérifier avant insertion (exécuter la requête avec ToList pour éviter les problèmes de traduction)
                var allRelations = storage.SelectAllRelations().ToList();
                var existing = allRelations.FirstOrDefault(r => r.InvoiceId == request.InvoiceId && r.DeliveryId == request.DeliveryId);
                if (existing != null) 
                {
                    // Si la relation existe déjà, retourner la relation existante
                    return Ok(existing);
                }
                
                var relation = new Backup.Web.Api.Server.Models.DocumentRelation { InvoiceId = request.InvoiceId, DeliveryId = request.DeliveryId };
                var saved = await storage.InsertRelationAsync(relation);
                return Ok(saved);
            }
            catch (DbUpdateException ex) when (ex.InnerException is MySqlConnector.MySqlException mysqlEx && mysqlEx.Message.Contains("Duplicate entry"))
            {
                // Gérer le cas où la relation existe déjà (race condition - deux requêtes simultanées)
                var allRelations = storage.SelectAllRelations().ToList();
                var existing = allRelations.FirstOrDefault(r => r.InvoiceId == request.InvoiceId && r.DeliveryId == request.DeliveryId);
                if (existing != null)
                {
                    return Ok(existing);
                }
                return Conflict(new { error = "Relation already exists.", isDuplicate = true });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpGet("relations")]
        public IActionResult Relations([FromServices] Backup.Web.Api.Server.Brokers.Storage.IStorageBroker storage)
        {
            var rels = storage.SelectAllRelations().ToList();
            return Ok(rels);
        }

        [HttpDelete("link/{id:int}")]
        public async Task<IActionResult> Unlink([FromRoute] int id, [FromServices] Backup.Web.Api.Server.Brokers.Storage.IStorageBroker storage)
        {
            var rel = storage.SelectAllRelations().FirstOrDefault(r => r.Id == id);
            if (rel == null) return NotFound();
            await storage.DeleteRelationAsync(id);
            return NoContent();
        }

        [HttpPost("generate-samples")]
        public async Task<IActionResult> GenerateSamples([FromServices] IConfiguration config, CancellationToken ct)
        {
            // Define sample pairs
            var samples = new List<(string type, string number, string client, string[] lines)>
            {
                ("Facture", "INV-001", "Client A", new[]{ "Produit A - 10", "Produit B - 5" }),
                ("BonLivraison", "DEL-001", "Client A", new[]{ "Produit A - 10", "Produit B - 5" }),
                ("Facture", "INV-002", "Client B", new[]{ "Produit A - 8", "Produit B - 7" }),
                ("BonLivraison", "DEL-002", "Client B", new[]{ "Produit A - 7", "Produit B - 8" })
            };

            var created = new List<object>();

            foreach (var s in samples)
            {
                var bytes = BuildSimplePdf($"{s.type} {s.number}", s.client, s.lines);
                await using var ms = new MemoryStream(bytes);
                var formFile = new FormFile(ms, 0, bytes.Length, "file", $"{s.number}.pdf");
                ms.Position = 0;
                var doc = await this.documentService.UploadAsync(formFile, s.type, s.number, s.client, DateTime.UtcNow, ct, null);
                created.Add(new { doc.Id, doc.TypeDocument, doc.Numero, doc.Client });
            }

            return Ok(new { created });
        }

        [HttpPost("{id:int}/reparse-lines")]
        public async Task<IActionResult> ReparseLines([FromRoute] int id, [FromQuery] bool useAiFallback, CancellationToken ct)
        {
            var ok = await this.documentService.ReparseDocumentLinesAsync(id, useAiFallback, ct);
            return Ok(new { documentId = id, success = ok });
        }

        [HttpPost("{id:int}/reparse-preview")]
        public async Task<IActionResult> ReparsePreview(
            [FromRoute] int id,
            [FromQuery] bool useAi,
            [FromServices] Backup.Web.Api.Server.Brokers.Storage.IStorageBroker storage,
            [FromServices] Backup.Web.Api.Server.Services.Documents.IDocumentParserService parser,
            [FromServices] Backup.Web.Api.Server.Services.Documents.Ollama.IOllamaParsingService ollama,
            CancellationToken ct)
        {
            var doc = await storage.SelectDocumentByIdAsync(id);
            if (doc == null) return NotFound();
            var text = doc.ContentText ?? string.Empty;
            var det = parser.Parse(text)?.ToList() ?? new List<DocumentLine>();
            var ai = useAi ? await ollama.TryParseAsync(text, ct) : new List<DocumentLine>();
            // use same merge logic as service
            List<DocumentLine> merged;
            if (det.Count < 5 && ai.Count >= 5)
            {
                merged = ai;
            }
            else
            {
                merged = Merge(det, ai);
            }
            return Ok(new
            {
                documentId = id,
                detCount = det.Count,
                aiCount = ai.Count,
                mergedCount = merged.Count,
                deterministic = det,
                ai = ai,
                merged = merged
            });
        }

        private static List<DocumentLine> Merge(List<DocumentLine> det, List<DocumentLine> ai)
        {
            if (det.Count == 0) return ai;
            var result = new List<DocumentLine>(det);
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
                    r.Quantity = aiLine.Quantity;
                    r.Unit = string.IsNullOrWhiteSpace(r.Unit) ? aiLine.Unit : r.Unit;
                    if (string.IsNullOrWhiteSpace(r.ProductCode)) r.ProductCode = aiLine.ProductCode;
                    if (string.IsNullOrWhiteSpace(r.Ean)) r.Ean = aiLine.Ean;
                    if (string.IsNullOrWhiteSpace(r.Product)) r.Product = aiLine.Product;
                }
            }
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

        [HttpPost("compare-and-stock")]
        public async Task<IActionResult> CompareAndStock([FromQuery] int invoiceId, [FromQuery] int deliveryId, [FromServices] Backup.Web.Api.Server.Services.Stock.IStockService stockService, CancellationToken ct, [FromQuery] bool forceUpdate = false)
        {
            if (invoiceId <= 0 || deliveryId <= 0) return BadRequest("Ids required");
            var updated = await stockService.UpdateFromDeliveryIfMatchAsync(invoiceId, deliveryId, ct, forceUpdate);
            return Ok(new { success = updated });
        }

        [HttpPost("compare-and-stock-all-deliveries")]
        public async Task<IActionResult> CompareAndStockAllDeliveries([FromQuery] int invoiceId, [FromServices] Backup.Web.Api.Server.Services.Stock.IStockService stockService, CancellationToken ct, [FromQuery] bool forceUpdate = false)
        {
            if (invoiceId <= 0) return BadRequest("InvoiceId required");
            var result = await stockService.UpdateFromAllDeliveriesForInvoiceAsync(invoiceId, ct, forceUpdate);
            return Ok(result);
        }

        public class SaveAdjustmentRequest
        {
            public int DeliveryId { get; set; }
            public int InvoiceId { get; set; }
            public int? DocumentLineId { get; set; }
            public string ProductKey { get; set; } = string.Empty;
            public decimal DeliveryQuantity { get; set; }
            public decimal? ActualQuantity { get; set; }
            public bool Validate { get; set; } // Si true, valide l'ajustement
        }

        [HttpPost("adjustments")]
        public async Task<IActionResult> SaveAdjustment([FromBody] SaveAdjustmentRequest request, [FromServices] Backup.Web.Api.Server.Brokers.Storage.IStorageBroker storage, CancellationToken ct)
        {
            System.Diagnostics.Debug.WriteLine($"[SaveAdjustment] Received request: DeliveryId={request.DeliveryId}, InvoiceId={request.InvoiceId}, ProductKey={request.ProductKey}, ActualQuantity={request.ActualQuantity}, Validate={request.Validate}");
            
            if (request.DeliveryId <= 0 || request.InvoiceId <= 0 || string.IsNullOrWhiteSpace(request.ProductKey))
            {
                System.Diagnostics.Debug.WriteLine($"[SaveAdjustment] BadRequest: DeliveryId={request.DeliveryId}, InvoiceId={request.InvoiceId}, ProductKey={request.ProductKey}");
                return BadRequest(new { message = "DeliveryId, InvoiceId, and ProductKey are required" });
            }

            // Chercher un ajustement existant
            var productKeyForSearch = Backup.Web.Api.Server.Services.Documents.Parsing.ProductKeyHelper.Normalize(request.ProductKey);
            var existing = await storage.SelectAdjustmentByDeliveryAndProductKeyAsync(request.DeliveryId, productKeyForSearch);
            
            if (request.DocumentLineId.HasValue && request.DocumentLineId.Value <= 0)
                request.DocumentLineId = null;

            Backup.Web.Api.Server.Models.DeliveryLineAdjustment adjustment;
            if (existing != null)
            {
                System.Diagnostics.Debug.WriteLine($"[SaveAdjustment] Updating existing adjustment ID={existing.Id}, Current ActualQuantity={existing.ActualQuantity}, New ActualQuantity={request.ActualQuantity}, Validate={request.Validate}");
                // Mettre à jour l'ajustement existant
                existing.ActualQuantity = request.ActualQuantity;
                if (request.Validate)
                {
                    existing.IsValidated = true;
                    existing.ValidatedAt = DateTime.UtcNow;
                    existing.ValidatedBy = "System"; // TODO: Récupérer l'utilisateur actuel
                    System.Diagnostics.Debug.WriteLine($"[SaveAdjustment] Validating adjustment ID={existing.Id} with ActualQuantity={existing.ActualQuantity}");
                }
                else
                {
                    // Si on ne valide pas, on peut réinitialiser la validation si la quantité a changé
                    if (existing.ActualQuantity != request.ActualQuantity)
                    {
                        existing.IsValidated = false;
                        existing.ValidatedAt = null;
                        existing.ValidatedBy = null;
                        System.Diagnostics.Debug.WriteLine($"[SaveAdjustment] Quantity changed, resetting validation for adjustment ID={existing.Id}");
                    }
                }
                adjustment = await storage.UpdateAdjustmentAsync(existing);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[SaveAdjustment] Creating new adjustment");
                // Créer un nouvel ajustement
                adjustment = new Backup.Web.Api.Server.Models.DeliveryLineAdjustment
                {
                    DeliveryId = request.DeliveryId,
                    InvoiceId = request.InvoiceId,
                    DocumentLineId = request.DocumentLineId,
                    ProductKey = Backup.Web.Api.Server.Services.Documents.Parsing.ProductKeyHelper.Normalize(request.ProductKey),
                    DeliveryQuantity = request.DeliveryQuantity,
                    ActualQuantity = request.ActualQuantity,
                    IsValidated = request.Validate,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "System", // TODO: Récupérer l'utilisateur actuel
                    ValidatedAt = request.Validate ? DateTime.UtcNow : null,
                    ValidatedBy = request.Validate ? "System" : null
                };
                adjustment = await storage.InsertAdjustmentAsync(adjustment);
                System.Diagnostics.Debug.WriteLine($"[SaveAdjustment] Created adjustment ID={adjustment.Id}, IsValidated={adjustment.IsValidated}");
            }

            System.Diagnostics.Debug.WriteLine($"[SaveAdjustment] Returning adjustment: ID={adjustment.Id}, IsValidated={adjustment.IsValidated}, ActualQuantity={adjustment.ActualQuantity}");
            return Ok(adjustment);
        }

        [HttpGet("{id:int}/lines")]
        public IActionResult GetLines([FromRoute] int id, [FromServices] Backup.Web.Api.Server.Brokers.Storage.IStorageBroker storage)
        {
            var lines = storage.SelectLinesByDocumentId(id).OrderBy(l => l.LineNumber).ToList();
            return Ok(lines);
        }

        public class PriceDiffLine
        {
            public string Product { get; set; } = string.Empty;
            public string? ProductCode { get; set; }
            public string? Ean { get; set; }
            public decimal InvoiceUnitPrice { get; set; }
            public decimal ReferenceUnitPrice { get; set; }
            public decimal Delta => InvoiceUnitPrice - ReferenceUnitPrice;
            public string Change => Delta == 0 ? "Same" : (Delta > 0 ? "Up" : "Down");
        }

        [HttpGet("{invoiceId:int}/price-diff")]
        public async Task<IActionResult> PriceDiff([FromRoute] int invoiceId, [FromServices] Backup.Web.Api.Server.Brokers.Storage.IStorageBroker storage, CancellationToken ct)
        {
            var invoice = await storage.SelectDocumentByIdAsync(invoiceId);
            if (invoice == null) return NotFound("Invoice not found");

            var invLines = storage.SelectLinesByDocumentId(invoiceId).ToList();
            if (invLines.Count == 0) return Ok(Array.Empty<PriceDiffLine>());

            string Key(DocumentLine l) => Backup.Web.Api.Server.Services.Documents.Parsing.ProductKeyHelper.GetProductKey(l);

            var thisKeys = new HashSet<string>(invLines.Select(Key), StringComparer.OrdinalIgnoreCase);

            // Find previous invoices
            var previousInvoiceIds = storage.SelectAllDocuments()
                .Where(d => d.Id != invoiceId
                            && d.DateAdded < invoice.DateAdded
                            && (EF.Functions.Like(d.TypeDocument, "%facture%")
                                || EF.Functions.Like(d.TypeDocument, "%invoice%")))
                .OrderByDescending(d => d.DateAdded)
                .Select(d => d.Id)
                .Take(50) // limit scan
                .ToList();

            // Build reference price per key: most recent unit price > 0
            var refPrice = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            foreach (var pid in previousInvoiceIds)
            {
                var lines = storage.SelectLinesByDocumentId(pid).ToList();
                foreach (var l in lines)
                {
                    var k = Key(l);
                    if (!thisKeys.Contains(k)) continue;
                    if (refPrice.ContainsKey(k)) continue; // we already have most recent
                    if (l.UnitPrice > 0) refPrice[k] = l.UnitPrice;
                }
                if (refPrice.Count == thisKeys.Count) break;
            }

            var results = new List<PriceDiffLine>();
            foreach (var l in invLines)
            {
                if (l.UnitPrice <= 0) continue;
                var k = Key(l);
                refPrice.TryGetValue(k, out var rp);
                results.Add(new PriceDiffLine
                {
                    Product = l.Product ?? string.Empty,
                    ProductCode = l.ProductCode,
                    Ean = l.Ean,
                    InvoiceUnitPrice = l.UnitPrice,
                    ReferenceUnitPrice = rp
                });
            }

            return Ok(results.OrderBy(r => r.Product).ToList());
        }
        [HttpPost("generate-pro-samples")]
        public async Task<IActionResult> GenerateProSamples(CancellationToken ct)
        {
            var created = new List<object>();

            // INV-101 / DEL-101 (match)
            var inv101 = BuildProPdf(
                docType: "Facture",
                number: "INV-101",
                client: "Client A",
                date: DateTime.UtcNow,
                rows: new (string product, string description, decimal quantity, decimal unitPrice)[]
                {
                    ("Produit A", "Article standard A", 10, 12.50m),
                    ("Produit B", "Article standard B", 5,  8.00m)
                });

            var del101 = BuildProPdf(
                docType: "BonLivraison",
                number: "DEL-101",
                client: "Client A",
                date: DateTime.UtcNow,
                rows: new (string product, string description, decimal quantity, decimal unitPrice)[]
                {
                    ("Produit A", "Article standard A", 10, 0m),
                    ("Produit B", "Article standard B", 5,  0m)
                });

            // INV-102 / DEL-102 (mismatch)
            var inv102 = BuildProPdf(
                docType: "Facture",
                number: "INV-102",
                client: "Client B",
                date: DateTime.UtcNow,
                rows: new (string product, string description, decimal quantity, decimal unitPrice)[]
                {
                    ("Produit A", "Article standard A", 8,  12.50m),
                    ("Produit B", "Article standard B", 7,  8.00m)
                });

            var del102 = BuildProPdf(
                docType: "BonLivraison",
                number: "DEL-102",
                client: "Client B",
                date: DateTime.UtcNow,
                rows: new (string product, string description, decimal quantity, decimal unitPrice)[]
                {
                    ("Produit A", "Article standard A", 7,  0m),
                    ("Produit B", "Article standard B", 8,  0m)
                });

            foreach (var entry in new[]{ (bytes: inv101, type:"Facture", num:"INV-101", cli:"Client A"),
                                         (bytes: del101, type:"BonLivraison", num:"DEL-101", cli:"Client A"),
                                         (bytes: inv102, type:"Facture", num:"INV-102", cli:"Client B"),
                                         (bytes: del102, type:"BonLivraison", num:"DEL-102", cli:"Client B") })
            {
                await using var ms = new MemoryStream(entry.bytes);
                var formFile = new FormFile(ms, 0, entry.bytes.Length, "file", $"{entry.num}.pdf");
                ms.Position = 0;
                var doc = await this.documentService.UploadAsync(formFile, entry.type, entry.num, entry.cli, DateTime.UtcNow, ct, null);
                created.Add(new { doc.Id, doc.TypeDocument, doc.Numero, doc.Client });
            }

            return Ok(new { created });
        }

        private static byte[] BuildProPdf(string docType, string number, string client, DateTime date, (string product, string description, decimal quantity, decimal unitPrice)[] rows)
        {
            QuestPDF.Settings.License = LicenseType.Community;
            var totalTtc = rows.Sum(r => r.quantity * r.unitPrice);
            var pdf = QuestPDF.Fluent.Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(30);
                    page.Header().Column(header =>
                    {
                        header.Item().Text($"{docType} {number}").Bold().FontSize(18);
                        header.Item().Text($"Client: {client}");
                        header.Item().Text($"Date: {date:yyyy-MM-dd}");
                    });
                    page.Content().Column(col =>
                    {
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(3);   // Produit
                                columns.RelativeColumn(4);   // Description
                                columns.RelativeColumn(2);   // Quantité
                                columns.RelativeColumn(2);   // Prix
                                columns.RelativeColumn(2);   // Total
                            });

                            // header row
                            table.Header(h =>
                            {
                                h.Cell().Text("Produit").SemiBold();
                                h.Cell().Text("Description").SemiBold();
                                h.Cell().Text("Quantité").SemiBold();
                                h.Cell().Text("Prix Unitaire").SemiBold();
                                h.Cell().Text("Total").SemiBold();
                            });

                            foreach (var r in rows)
                            {
                                // Make quantity cell include explicit label so OCR text contains "Quantité: X"
                                table.Cell().Text(r.product);
                                table.Cell().Text(string.IsNullOrWhiteSpace(r.description) ? "-" : r.description);
                                table.Cell().Text($"Quantité: {r.quantity}");
                                table.Cell().Text(r.unitPrice > 0 ? $"{r.unitPrice:0.00}" : "-");
                                table.Cell().Text(r.unitPrice > 0 ? $"{(r.unitPrice * r.quantity):0.00}" : "-");
                            }
                        });

                        if (totalTtc > 0)
                        {
                            col.Item().AlignRight().Text($"Total TTC: {totalTtc:0.00}").Bold();
                        }
                    });
                });
            });
            return pdf.GeneratePdf();
        }

        private static byte[] BuildSimplePdf(string title, string client, string[] lines)
        {
            QuestPDF.Settings.License = LicenseType.Community;
            var doc = QuestPDF.Fluent.Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(30);
                    page.Header().Text(title).Bold().FontSize(18);
                    page.Content().Column(col =>
                    {
                        col.Item().Text($"Client: {client}").FontSize(12);
                        foreach (var l in lines)
                            col.Item().Text(l).FontSize(12);
                    });
                });
            });
            return doc.GeneratePdf();
        }

        private static void LogQuantityIndependence(IReadOnlyList<DocumentLine> results)
        {
            try
            {
                Console.WriteLine("=== TEST INDÉPENDANCE DES QUANTITÉS ===");
                for (int i = 0; i < results.Count; i++)
                {
                    var line = results[i];
                    Console.WriteLine($"Ligne {i}: {line.ProductCode} - {line.Quantity} {line.Unit}");
                    if (i > 0 && line.Quantity == results[i - 1].Quantity)
                    {
                        Console.WriteLine("⚠️ ATTENTION: Quantité identique à la ligne précédente!");
                    }
                }
            }
            catch
            {
                // ignore logging errors
            }
        }
    }
}


