using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Models;
using Backup.Web.Api.Server.Models.Entities;
using Backup.Web.Api.Server.Services.Documents;
using Backup.Web.Api.Server.Services.Numbering;
using Backup.Web.Api.Server.Services.Sales;
using Backup.Web.Api.Server.Services.Tenancy;

namespace Backup.Web.Api.Server.Services.Accounting
{
    /// <summary>
    /// Après OCR en-tête : crée un brouillon facture fournisseur
    /// (lignes du parser achats si disponibles, sinon une ligne de totaux).
    /// </summary>
    public static class AccountingOcrInvoiceImport
    {
        public sealed class LinePreview
        {
            public string Product { get; set; } = string.Empty;
            public decimal Quantity { get; set; }
            public decimal UnitPrice { get; set; }
        }

        public sealed class InvoiceDto
        {
            public string DocumentType { get; set; } = "facture";
            public string? Ice { get; set; }
            public string? TaxId { get; set; }
            public string? TradeRegister { get; set; }
            public string? InvoiceNumber { get; set; }
            public DateTime? InvoiceDate { get; set; }
            public string? PartyName { get; set; }
            public decimal? AmountHt { get; set; }
            public decimal? VatAmount { get; set; }
            public decimal? AmountTtc { get; set; }
            public decimal? VatRate { get; set; }
            public double Confidence { get; set; }
            public int LineCount { get; set; }
            public List<LinePreview> Lines { get; set; } = new();
            public string Source { get; set; } = "header";
        }

        public sealed class ImportResult
        {
            public int InvoiceId { get; set; }
            public string InvoiceNumber { get; set; } = string.Empty;
            public int SupplierId { get; set; }
            public string SupplierName { get; set; } = string.Empty;
            public bool Created { get; set; }
            public int LineCount { get; set; }
            public string Source { get; set; } = "header";
            public InvoiceDto Extraction { get; set; } = new();
        }

        public sealed class BankLineDto
        {
            public DateTime OperationDate { get; set; }
            public string Label { get; set; } = string.Empty;
            public string? Reference { get; set; }
            public decimal Debit { get; set; }
            public decimal Credit { get; set; }
        }

        public sealed class UnifiedExtract
        {
            public string DocumentType { get; set; } = "facture";
            public double TypeConfidence { get; set; }
            public string Source { get; set; } = "csharp";
            public InvoiceDto? Invoice { get; set; }
            public List<BankLineDto> BankLines { get; set; } = new();
        }

        public static UnifiedExtract FromLocal(string text, string? fileName, IDocumentParserService? parser, string? hint = null)
        {
            var classified = MoroccanDocumentParser.Classify(text, fileName);
            var hinted = ResolveHint(hint);
            var type = hinted ?? classified.Type;
            var conf = hinted != null ? 1.0 : classified.Confidence;
            if (type == "releve_bancaire")
            {
                List<BankStatementCsvParser.ParsedLine> lines;
                try { lines = MoroccanDocumentParser.ParseBankStatement(text, fileName); }
                catch (InvalidOperationException) { lines = new List<BankStatementCsvParser.ParsedLine>(); }
                return new UnifiedExtract
                {
                    DocumentType = type,
                    TypeConfidence = conf,
                    Source = "csharp",
                    BankLines = lines.Select(l => new BankLineDto
                    {
                        OperationDate = l.OperationDate,
                        Label = l.Label,
                        Reference = l.Reference,
                        Debit = l.Debit,
                        Credit = l.Credit
                    }).ToList()
                };
            }

            var invoice = Preview(text, parser);
            invoice.DocumentType = type;
            return new UnifiedExtract
            {
                DocumentType = type,
                TypeConfidence = conf,
                Source = "csharp",
                Invoice = invoice
            };
        }

        private static string? ResolveHint(string? hint)
        {
            var h = (hint ?? "").Trim().ToLowerInvariant();
            return h switch
            {
                "bank" or "releve" or "relevé" or "releve_bancaire" => "releve_bancaire",
                "invoice" or "facture" => "facture",
                "delivery" or "bl" or "bon_livraison" => "bon_livraison",
                _ => null
            };
        }

        public static InvoiceDto Preview(string text, IDocumentParserService? parser)
        {
            var header = MoroccanDocumentParser.ParseInvoice(text);
            var productLines = TryParsePurchaseLines(text, parser);
            return ToDto(header, productLines);
        }

        public static async Task<(ImportResult? Dto, string? Error)> ImportAsync(
            IStorageBroker storage,
            INumberingSequenceService numbering,
            IDocumentParserService? parser,
            string? companyId,
            string text,
            string actor)
        {
            if (string.IsNullOrWhiteSpace(text))
                return (null, "Texte requis.");

            var preview = Preview(text, parser);
            if (preview.AmountHt is null && preview.AmountTtc is null && preview.LineCount == 0)
                return (null, "Aucun montant ni ligne article reconnu. Contrôlez le scan, ou utilisez Documents / Achats.");

            var supplier = await ResolveSupplierAsync(storage, companyId, preview);
            var invoiceNumber = string.IsNullOrWhiteSpace(preview.InvoiceNumber)
                ? await numbering.GetNextNumberAsync("SupplierInvoice", companyId)
                : preview.InvoiceNumber.Trim();

            var existing = storage.SelectAllSupplierInvoices()
                .ForCompany(companyId)
                .FirstOrDefault(i =>
                    i.SupplierId == supplier.Id
                    && i.InvoiceNumber.ToLower() == invoiceNumber.ToLowerInvariant()
                    && i.Status.ToLower() != "cancelled");
            if (existing != null)
            {
                return (new ImportResult
                {
                    InvoiceId = existing.Id,
                    InvoiceNumber = existing.InvoiceNumber,
                    SupplierId = supplier.Id,
                    SupplierName = supplier.Name,
                    Created = false,
                    LineCount = existing.Lines?.Count ?? 0,
                    Source = preview.Source,
                    Extraction = preview
                }, null);
            }

            var lines = preview.LineCount > 0
                ? MapProductLines(TryParsePurchaseLines(text, parser), preview.VatRate ?? 20m)
                : new List<SupplierInvoiceLineEntity> { SummaryLine(preview) };

            var invoice = new SupplierInvoiceEntity
            {
                SupplierId = supplier.Id,
                InvoiceNumber = invoiceNumber,
                Date = preview.InvoiceDate ?? DateTime.UtcNow,
                Status = "Draft",
                Notes = BuildNotes(preview, actor),
                CompanyId = companyId,
                CreatedAt = DateTime.UtcNow,
                Lines = lines
            };
            invoice.EnsureCompanyId(companyId);
            invoice.CurrencyCode = await SalesBusinessRules.ResolveCompanyCurrencyAsync(storage, companyId);
            invoice.DueDate = invoice.Date.AddDays(30);
            SalesBusinessRules.RecalculateSupplierInvoiceTotals(invoice);

            var created = await storage.InsertSupplierInvoiceAsync(invoice);
            await SalesDocumentAudit.LogAsync(
                storage, companyId, "SupplierInvoice", created.Id, "Created",
                actor, $"OCR comptable → brouillon {created.InvoiceNumber}");

            return (new ImportResult
            {
                InvoiceId = created.Id,
                InvoiceNumber = created.InvoiceNumber,
                SupplierId = supplier.Id,
                SupplierName = supplier.Name,
                Created = true,
                LineCount = created.Lines?.Count ?? lines.Count,
                Source = preview.Source,
                Extraction = preview
            }, null);
        }

        private static InvoiceDto ToDto(
            MoroccanDocumentParser.InvoiceExtraction header,
            List<DocumentLine> productLines)
        {
            return new InvoiceDto
            {
                Ice = header.Ice,
                TaxId = header.TaxId,
                TradeRegister = header.TradeRegister,
                InvoiceNumber = header.InvoiceNumber,
                InvoiceDate = header.InvoiceDate,
                PartyName = header.PartyName,
                AmountHt = header.AmountHt,
                VatAmount = header.VatAmount,
                AmountTtc = header.AmountTtc,
                VatRate = header.VatRate,
                Confidence = header.Confidence,
                LineCount = productLines.Count,
                Source = productLines.Count > 0 ? "purchaseParser" : "header",
                Lines = productLines.Take(20).Select(l => new LinePreview
                {
                    Product = string.IsNullOrWhiteSpace(l.Product) ? (l.RawLine ?? "") : l.Product,
                    Quantity = l.Quantity,
                    UnitPrice = l.UnitPrice
                }).ToList()
            };
        }

        private static List<DocumentLine> TryParsePurchaseLines(string text, IDocumentParserService? parser)
        {
            if (parser == null || string.IsNullOrWhiteSpace(text)) return new List<DocumentLine>();
            try
            {
                return parser.Parse(text)?.Where(l =>
                    !string.IsNullOrWhiteSpace(l.Product) || l.Quantity != 0 || l.UnitPrice != 0 || l.TotalValue != 0
                ).ToList() ?? new List<DocumentLine>();
            }
            catch
            {
                return new List<DocumentLine>();
            }
        }

        private static async Task<Supplier> ResolveSupplierAsync(
            IStorageBroker storage, string? companyId, InvoiceDto preview)
        {
            var ice = (preview.Ice ?? "").Trim();
            var name = (preview.PartyName ?? "").Trim();
            var suppliers = storage.SelectAllSuppliers().ForCompany(companyId);

            Supplier? match = null;
            if (ice.Length >= 8)
            {
                match = suppliers.FirstOrDefault(s =>
                    s.VatNumber != null && s.VatNumber.Replace(" ", "") == ice);
            }
            if (match == null && name.Length >= 3)
            {
                var lower = name.ToLowerInvariant();
                match = suppliers.FirstOrDefault(s => s.Name.ToLower() == lower)
                    ?? suppliers.FirstOrDefault(s => s.Name.ToLower().Contains(lower) || lower.Contains(s.Name.ToLower()));
            }

            if (match != null)
            {
                if (string.IsNullOrWhiteSpace(match.VatNumber) && ice.Length >= 8)
                {
                    match.VatNumber = ice;
                    match.UpdatedAt = DateTime.UtcNow;
                    await storage.UpdateSupplierAsync(match);
                }
                return match;
            }

            return await storage.InsertSupplierAsync(new Supplier
            {
                SupplierCode = "SUP-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
                Name = name.Length >= 2 ? name : (ice.Length > 0 ? "ICE " + ice : "Fournisseur OCR"),
                VatNumber = string.IsNullOrWhiteSpace(ice) ? preview.TaxId : ice,
                IsActive = true,
                Status = "Active",
                CompanyId = companyId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        private static List<SupplierInvoiceLineEntity> MapProductLines(List<DocumentLine> lines, decimal vatRate)
        {
            return lines.Select((line, index) =>
            {
                var quantity = line.Quantity == 0 ? 1m : line.Quantity;
                var unitPrice = line.UnitPrice;
                var totalHt = line.TotalValue != 0 ? line.TotalValue : quantity * unitPrice;
                if (unitPrice == 0 && quantity != 0 && totalHt != 0)
                    unitPrice = totalHt / quantity;
                return new SupplierInvoiceLineEntity
                {
                    ProductKey = string.IsNullOrWhiteSpace(line.ProductCode) ? (line.Ean ?? "OCR") : line.ProductCode,
                    Description = string.IsNullOrWhiteSpace(line.Product) ? (line.RawLine ?? "Ligne OCR") : line.Product,
                    Quantity = quantity,
                    UnitPrice = unitPrice,
                    VatRate = vatRate,
                    LineNumber = line.LineNumber > 0 ? line.LineNumber : index + 1
                };
            }).ToList();
        }

        private static SupplierInvoiceLineEntity SummaryLine(InvoiceDto preview)
        {
            var ht = preview.AmountHt
                ?? (preview.AmountTtc is { } ttc && preview.VatRate is { } r && r > 0
                    ? Math.Round(ttc / (1 + r / 100m), 2)
                    : preview.AmountTtc ?? 0m);
            var rate = preview.VatRate ?? 20m;
            return new SupplierInvoiceLineEntity
            {
                ProductKey = "OCR",
                Description = string.IsNullOrWhiteSpace(preview.InvoiceNumber)
                    ? "Facture OCR (en-tête)"
                    : "Facture OCR " + preview.InvoiceNumber,
                Quantity = 1,
                UnitPrice = ht,
                VatRate = rate,
                LineNumber = 1
            };
        }

        private static string BuildNotes(InvoiceDto preview, string actor)
        {
            var parts = new List<string> { "Créé depuis OCR comptable (" + actor + ")." };
            if (!string.IsNullOrWhiteSpace(preview.Ice)) parts.Add("ICE " + preview.Ice);
            if (!string.IsNullOrWhiteSpace(preview.TaxId)) parts.Add("IF " + preview.TaxId);
            parts.Add("Confiance " + Math.Round(preview.Confidence * 100) + " %.");
            if (preview.Source == "header")
                parts.Add("Pas de lignes articles : à compléter dans Achats.");
            return string.Join(" ", parts);
        }
    }
}
