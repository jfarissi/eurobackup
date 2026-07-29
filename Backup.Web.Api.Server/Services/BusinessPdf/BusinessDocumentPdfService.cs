using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Backup.Web.Api.Server.Models.Entities;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Backup.Web.Api.Server.Services.BusinessPdf
{
    public interface IBusinessDocumentPdfService
    {
        byte[] GenerateQuotePdf(Quote quote);
        byte[] GenerateSalesOrderPdf(SalesOrder order);
        byte[] GenerateSalesInvoicePdf(SalesInvoice invoice);
        byte[] GenerateCreditNotePdf(CreditNoteEntity creditNote);
        byte[] GenerateSalesDeliveryNotePdf(SalesDeliveryNote note);
        byte[] GeneratePurchaseOrderPdf(PurchaseOrder order);
        byte[] GenerateSupplierInvoicePdf(SupplierInvoiceEntity invoice);
    }

    public class BusinessDocumentPdfService : IBusinessDocumentPdfService
    {
        private static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-BE");

        public BusinessDocumentPdfService()
        {
            QuestPDF.Settings.License = LicenseType.Community;
        }

        public byte[] GenerateQuotePdf(Quote quote) =>
            Build(
                title: "DEVIS",
                number: quote.QuoteNumber,
                date: quote.Date,
                extraDateLabel: "Validité",
                extraDate: quote.ExpirationDate,
                partyLabel: "Client",
                partyName: quote.Customer?.Name ?? $"Client #{quote.CustomerId}",
                partyLines: PartyLines(quote.Customer),
                status: quote.Status,
                notes: quote.Notes,
                lines: quote.Lines.Select(l => new PdfLine(l.ProductKey, l.Description, l.Quantity, l.UnitPrice, l.VatRate, l.TotalHT, l.TotalTTC)).ToList(),
                totalHt: quote.TotalHT,
                totalVat: quote.TotalVat,
                totalTtc: quote.TotalTTC);

        public byte[] GenerateSalesOrderPdf(SalesOrder order) =>
            Build(
                title: "COMMANDE CLIENT",
                number: order.OrderNumber,
                date: order.Date,
                extraDateLabel: null,
                extraDate: null,
                partyLabel: "Client",
                partyName: order.Customer?.Name ?? $"Client #{order.CustomerId}",
                partyLines: PartyLines(order.Customer),
                status: order.Status,
                notes: order.Notes,
                lines: order.Lines.Select(l => new PdfLine(l.ProductKey, l.Description, l.Quantity, l.UnitPrice, l.VatRate, l.TotalHT, l.TotalTTC)).ToList(),
                totalHt: order.TotalHT,
                totalVat: order.TotalVat,
                totalTtc: order.TotalTTC);

        public byte[] GenerateSalesInvoicePdf(SalesInvoice invoice) =>
            Build(
                title: "FACTURE",
                number: invoice.InvoiceNumber,
                date: invoice.Date,
                extraDateLabel: "Échéance",
                extraDate: invoice.DueDate,
                partyLabel: "Client",
                partyName: invoice.Customer?.Name ?? $"Client #{invoice.CustomerId}",
                partyLines: PartyLines(invoice.Customer),
                status: invoice.Status,
                notes: invoice.Notes,
                lines: invoice.Lines.Select(l => new PdfLine(l.ProductKey, l.Description, l.Quantity, l.UnitPrice, l.VatRate, l.TotalHT, l.TotalTTC)).ToList(),
                totalHt: invoice.TotalHT,
                totalVat: invoice.TotalVat,
                totalTtc: invoice.TotalTTC,
                paidAmount: invoice.PaidAmount);

        public byte[] GenerateCreditNotePdf(CreditNoteEntity creditNote) =>
            Build(
                title: "AVOIR",
                number: creditNote.CreditNoteNumber,
                date: creditNote.Date,
                extraDateLabel: null,
                extraDate: null,
                partyLabel: "Client",
                partyName: creditNote.Customer?.Name ?? $"Client #{creditNote.CustomerId}",
                partyLines: PartyLines(creditNote.Customer),
                status: creditNote.Status,
                notes: creditNote.Notes,
                lines: creditNote.Lines.Select(l => new PdfLine(l.ProductKey, l.Description, l.Quantity, l.UnitPrice, l.VatRate, l.TotalHT, l.TotalTTC)).ToList(),
                totalHt: creditNote.TotalHT,
                totalVat: creditNote.TotalVat,
                totalTtc: creditNote.TotalTTC);

        public byte[] GenerateSalesDeliveryNotePdf(SalesDeliveryNote note) =>
            Build(
                title: "BON DE LIVRAISON",
                number: note.DeliveryNumber,
                date: note.DeliveryDate,
                extraDateLabel: null,
                extraDate: null,
                partyLabel: "Client",
                partyName: note.Customer?.Name ?? $"Client #{note.CustomerId}",
                partyLines: PartyLines(note.Customer),
                status: note.Status,
                notes: note.Notes,
                lines: note.Lines.Select(l => new PdfLine(l.ProductKey, l.Description, l.DeliveredQuantity, l.UnitPrice, l.VatRate, l.TotalHT, l.TotalTTC)).ToList(),
                totalHt: note.TotalHT,
                totalVat: note.TotalVat,
                totalTtc: note.TotalTTC);

        public byte[] GeneratePurchaseOrderPdf(PurchaseOrder order) =>
            Build(
                title: "COMMANDE FOURNISSEUR",
                number: order.OrderNumber,
                date: order.Date,
                extraDateLabel: "Livraison prévue",
                extraDate: order.ExpectedDeliveryDate,
                partyLabel: "Fournisseur",
                partyName: order.Supplier?.Name ?? $"Fournisseur #{order.SupplierId}",
                partyLines: PartyLines(order.Supplier),
                status: order.Status,
                notes: order.Notes,
                lines: order.Lines.Select(l => new PdfLine(l.ProductKey, l.Description, l.Quantity, l.UnitPrice, l.VatRate, l.TotalHT, l.TotalTTC)).ToList(),
                totalHt: order.TotalHT,
                totalVat: order.TotalVat,
                totalTtc: order.TotalTTC);

        public byte[] GenerateSupplierInvoicePdf(SupplierInvoiceEntity invoice) =>
            Build(
                title: "FACTURE FOURNISSEUR",
                number: invoice.InvoiceNumber,
                date: invoice.Date,
                extraDateLabel: "Échéance",
                extraDate: invoice.DueDate,
                partyLabel: "Fournisseur",
                partyName: invoice.Supplier?.Name ?? $"Fournisseur #{invoice.SupplierId}",
                partyLines: PartyLines(invoice.Supplier),
                status: invoice.Status,
                notes: invoice.Notes,
                lines: invoice.Lines.Select(l => new PdfLine(l.ProductKey, l.Description, l.Quantity, l.UnitPrice, l.VatRate, l.TotalHT, l.TotalTTC)).ToList(),
                totalHt: invoice.TotalHT,
                totalVat: invoice.TotalVat,
                totalTtc: invoice.TotalTTC);

        private static IEnumerable<string> PartyLines(Customer? c)
        {
            if (c == null) yield break;
            if (!string.IsNullOrWhiteSpace(c.CustomerCode)) yield return $"Code: {c.CustomerCode}";
            if (!string.IsNullOrWhiteSpace(c.VatNumber)) yield return $"TVA: {c.VatNumber}";
            if (!string.IsNullOrWhiteSpace(c.Address)) yield return c.Address!;
            var city = string.Join(" ", new[] { c.PostalCode, c.City }.Where(x => !string.IsNullOrWhiteSpace(x)));
            if (!string.IsNullOrWhiteSpace(city)) yield return city;
            if (!string.IsNullOrWhiteSpace(c.Country)) yield return c.Country!;
            if (!string.IsNullOrWhiteSpace(c.Phone)) yield return $"Tél: {c.Phone}";
            if (!string.IsNullOrWhiteSpace(c.Email)) yield return c.Email!;
        }

        private static IEnumerable<string> PartyLines(Supplier? s)
        {
            if (s == null) yield break;
            if (!string.IsNullOrWhiteSpace(s.SupplierCode)) yield return $"Code: {s.SupplierCode}";
            if (!string.IsNullOrWhiteSpace(s.VatNumber)) yield return $"TVA: {s.VatNumber}";
            if (!string.IsNullOrWhiteSpace(s.Address)) yield return s.Address!;
            var city = string.Join(" ", new[] { s.PostalCode, s.City }.Where(x => !string.IsNullOrWhiteSpace(x)));
            if (!string.IsNullOrWhiteSpace(city)) yield return city;
            if (!string.IsNullOrWhiteSpace(s.Country)) yield return s.Country!;
            if (!string.IsNullOrWhiteSpace(s.Phone)) yield return $"Tél: {s.Phone}";
            if (!string.IsNullOrWhiteSpace(s.Email)) yield return s.Email!;
        }

        private static byte[] Build(
            string title,
            string number,
            DateTime date,
            string? extraDateLabel,
            DateTime? extraDate,
            string partyLabel,
            string partyName,
            IEnumerable<string> partyLines,
            string status,
            string? notes,
            IReadOnlyList<PdfLine> lines,
            decimal totalHt,
            decimal totalVat,
            decimal totalTtc,
            decimal? paidAmount = null)
        {
            using var stream = new MemoryStream();
            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(1.2f, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(10));

                    page.Header().Row(row =>
                    {
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text("Backup").FontSize(20).SemiBold().FontColor(Colors.Blue.Medium);
                            col.Item().Text("Documents commerciaux").FontSize(9).FontColor(Colors.Grey.Darken1);
                        });
                        row.RelativeItem().AlignRight().Column(col =>
                        {
                            col.Item().Text(title).FontSize(16).SemiBold().FontColor(Colors.Grey.Darken2);
                            col.Item().Text($"N° {number}").SemiBold();
                            col.Item().Text($"Date: {date:dd/MM/yyyy}");
                            if (extraDate.HasValue && !string.IsNullOrWhiteSpace(extraDateLabel))
                            {
                                col.Item().Text($"{extraDateLabel}: {extraDate:dd/MM/yyyy}");
                            }
                            col.Item().Text($"Statut: {status}").FontColor(Colors.Grey.Darken1);
                        });
                    });

                    page.Content().PaddingVertical(16).Column(col =>
                    {
                        col.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(party =>
                        {
                            party.Item().Text(partyLabel).FontSize(9).FontColor(Colors.Grey.Darken1);
                            party.Item().Text(partyName).FontSize(13).SemiBold();
                            foreach (var line in partyLines)
                            {
                                party.Item().Text(line).FontSize(9);
                            }
                        });

                        if (!string.IsNullOrWhiteSpace(notes))
                        {
                            col.Item().PaddingTop(10).Text($"Notes: {notes}").Italic().FontColor(Colors.Grey.Darken1);
                        }

                        col.Item().PaddingTop(14).Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(70);
                                columns.RelativeColumn(3);
                                columns.ConstantColumn(45);
                                columns.ConstantColumn(65);
                                columns.ConstantColumn(40);
                                columns.ConstantColumn(70);
                                columns.ConstantColumn(70);
                            });

                            table.Header(header =>
                            {
                                header.Cell().Element(HeaderCell).Text("Réf");
                                header.Cell().Element(HeaderCell).Text("Description");
                                header.Cell().Element(HeaderCell).AlignRight().Text("Qté");
                                header.Cell().Element(HeaderCell).AlignRight().Text("P.U. HT");
                                header.Cell().Element(HeaderCell).AlignRight().Text("TVA");
                                header.Cell().Element(HeaderCell).AlignRight().Text("Total HT");
                                header.Cell().Element(HeaderCell).AlignRight().Text("Total TTC");
                            });

                            if (lines.Count == 0)
                            {
                                table.Cell().ColumnSpan(7).Element(BodyCell).Text("Aucune ligne.").Italic().FontColor(Colors.Grey.Darken1);
                            }
                            else
                            {
                                foreach (var line in lines)
                                {
                                    table.Cell().Element(BodyCell).Text(line.ProductKey ?? "");
                                    table.Cell().Element(BodyCell).Text(line.Description ?? "");
                                    table.Cell().Element(BodyCell).AlignRight().Text(line.Quantity.ToString("0.##", Fr));
                                    table.Cell().Element(BodyCell).AlignRight().Text(Money(line.UnitPrice));
                                    table.Cell().Element(BodyCell).AlignRight().Text($"{line.VatRate:0.##} %");
                                    table.Cell().Element(BodyCell).AlignRight().Text(Money(line.TotalHt));
                                    table.Cell().Element(BodyCell).AlignRight().Text(Money(line.TotalTtc));
                                }
                            }
                        });

                        col.Item().AlignRight().PaddingTop(14).Width(220).Column(totals =>
                        {
                            totals.Item().Row(r =>
                            {
                                r.RelativeItem().Text("Total HT");
                                r.ConstantItem(90).AlignRight().Text(Money(totalHt));
                            });
                            totals.Item().Row(r =>
                            {
                                r.RelativeItem().Text("Total TVA");
                                r.ConstantItem(90).AlignRight().Text(Money(totalVat));
                            });
                            totals.Item().PaddingTop(4).BorderTop(1).BorderColor(Colors.Grey.Lighten1).PaddingTop(6).Row(r =>
                            {
                                r.RelativeItem().Text("Total TTC").SemiBold();
                                r.ConstantItem(90).AlignRight().Text(Money(totalTtc)).SemiBold().FontSize(12).FontColor(Colors.Blue.Medium);
                            });
                            if (paidAmount.HasValue)
                            {
                                totals.Item().PaddingTop(4).Row(r =>
                                {
                                    r.RelativeItem().Text("Payé");
                                    r.ConstantItem(90).AlignRight().Text(Money(paidAmount.Value));
                                });
                                totals.Item().Row(r =>
                                {
                                    r.RelativeItem().Text("Reste dû").SemiBold();
                                    r.ConstantItem(90).AlignRight().Text(Money(Math.Max(0, totalTtc - paidAmount.Value))).SemiBold();
                                });
                            }
                        });
                    });

                    page.Footer().AlignCenter().DefaultTextStyle(x => x.FontSize(8).FontColor(Colors.Grey.Darken1)).Text(text =>
                    {
                        text.Span("Page ");
                        text.CurrentPageNumber();
                        text.Span(" / ");
                        text.TotalPages();
                    });
                });
            }).GeneratePdf(stream);

            return stream.ToArray();
        }

        private static string Money(decimal value) => $"{value.ToString("N2", Fr)} €";

        private static IContainer HeaderCell(IContainer container) =>
            container.DefaultTextStyle(x => x.SemiBold().FontSize(9))
                .PaddingVertical(5)
                .PaddingHorizontal(2)
                .BorderBottom(1)
                .BorderColor(Colors.Black)
                .Background(Colors.Grey.Lighten3);

        private static IContainer BodyCell(IContainer container) =>
            container.PaddingVertical(4).PaddingHorizontal(2).BorderBottom(1).BorderColor(Colors.Grey.Lighten2);

        private sealed record PdfLine(
            string? ProductKey,
            string? Description,
            decimal Quantity,
            decimal UnitPrice,
            decimal VatRate,
            decimal TotalHt,
            decimal TotalTtc);
    }
}
