using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Backup.Web.Api.Server.Services.StoreChat
{
    public interface IStoreChatPdfService
    {
        StoreChatQuotePdfDto GenerateQuote(IReadOnlyList<StoreChatCartItem> items, string customerName);
        StoreChatQuotePdfDto GenerateInvoice(IReadOnlyList<StoreChatCartItem> items, string invoiceNumber, string customerName);
    }

    public class StoreChatPdfService : IStoreChatPdfService
    {
        private readonly StoreChatOptions _options;

        public StoreChatPdfService(Microsoft.Extensions.Options.IOptions<StoreChatOptions> options)
        {
            _options = options.Value ?? new StoreChatOptions();
            QuestPDF.Settings.License = LicenseType.Community;
        }

        public StoreChatQuotePdfDto GenerateQuote(IReadOnlyList<StoreChatCartItem> items, string customerName)
        {
            var total = items.Sum(i => i.TotalPrice);
            var fileName = $"devis-{DateTime.Now:yyyyMMdd-HHmmss}.pdf";
            var bytes = BuildPdf("DEVIS", fileName, customerName, items, total);
            return new StoreChatQuotePdfDto
            {
                PdfBase64 = Convert.ToBase64String(bytes),
                FileName = fileName,
                Total = total,
                Source = "quote",
                SourceLabel = "Devis magasin"
            };
        }

        public StoreChatQuotePdfDto GenerateInvoice(IReadOnlyList<StoreChatCartItem> items, string invoiceNumber, string customerName)
        {
            var total = items.Sum(i => i.TotalPrice);
            var fileName = $"facture-{invoiceNumber}.pdf";
            var bytes = BuildPdf("FACTURE", invoiceNumber, customerName, items, total);
            return new StoreChatQuotePdfDto
            {
                PdfBase64 = Convert.ToBase64String(bytes),
                FileName = fileName,
                Total = total,
                Source = "invoice",
                SourceLabel = "Facture"
            };
        }

        private byte[] BuildPdf(
            string docType,
            string docRef,
            string customerName,
            IReadOnlyList<StoreChatCartItem> items,
            decimal total)
        {
            using var stream = new MemoryStream();
            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(1, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(11).FontFamily(Fonts.Verdana));

                    page.Header().Row(row =>
                    {
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text(_options.BrandName).FontSize(22).SemiBold().FontColor(Colors.Blue.Medium);
                            col.Item().Text("Assistant magasin");
                        });
                        row.RelativeItem().AlignRight().Column(col =>
                        {
                            col.Item().Text(docType).FontSize(18).FontColor(Colors.Grey.Medium);
                            col.Item().Text($"Réf: {docRef}");
                            col.Item().Text($"Date: {DateTime.Now:dd/MM/yyyy}");
                        });
                    });

                    page.Content().PaddingVertical(20).Column(col =>
                    {
                        col.Item().Text($"Client: {customerName}").FontSize(14).SemiBold();
                        col.Item().PaddingVertical(10).Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(3);
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                            });

                            table.Header(header =>
                            {
                                header.Cell().Element(HeaderCell).Text("Produit");
                                header.Cell().Element(HeaderCell).Text("Qté");
                                header.Cell().Element(HeaderCell).Text("P.U.");
                                header.Cell().Element(HeaderCell).Text("Total");
                            });

                            foreach (var item in items)
                            {
                                table.Cell().Element(BodyCell).Text(item.Name);
                                table.Cell().Element(BodyCell).Text(item.Quantity.ToString("0.##", CultureInfo.InvariantCulture));
                                table.Cell().Element(BodyCell).Text($"{item.UnitPrice:N2} €");
                                table.Cell().Element(BodyCell).Text($"{item.TotalPrice:N2} €");
                            }
                        });

                        col.Item().AlignRight().PaddingTop(10).Text(x =>
                        {
                            x.Span("Total: ").SemiBold();
                            x.Span($"{total:N2} €").FontSize(14).SemiBold().FontColor(Colors.Red.Medium);
                        });
                    });
                });
            }).GeneratePdf(stream);

            return stream.ToArray();
        }

        private static IContainer HeaderCell(IContainer container) =>
            container.DefaultTextStyle(x => x.SemiBold()).PaddingVertical(5).BorderBottom(1).BorderColor(Colors.Black);

        private static IContainer BodyCell(IContainer container) =>
            container.PaddingVertical(5).BorderBottom(1).BorderColor(Colors.Grey.Lighten2);
    }
}
