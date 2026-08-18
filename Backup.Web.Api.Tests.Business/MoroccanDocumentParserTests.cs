using System;
using Backup.Web.Api.Server.Services.Accounting;

namespace Backup.Web.Api.Tests.Business
{
    public class MoroccanDocumentParserTests
    {
        [Fact]
        public void Invoice_ExtractsIceIfAndVat20()
        {
            var text = """
                Facture F-2026-88
                Date : 12/03/2026
                ICE : 001234567890123
                IF : 12345678
                RC : 44521
                Client : SARL Atlas Pieces
                Total HT : 1 000,00
                TVA 20 % : 200,00
                Total TTC : 1 200,00
                """;

            var dto = MoroccanDocumentParser.ParseInvoice(text);

            Assert.Equal("001234567890123", dto.Ice);
            Assert.Equal("12345678", dto.TaxId);
            Assert.Equal("F-2026-88", dto.InvoiceNumber);
            Assert.Equal(1000m, dto.AmountHt);
            Assert.Equal(200m, dto.VatAmount);
            Assert.Equal(1200m, dto.AmountTtc);
            Assert.Equal(20m, dto.VatRate);
            Assert.True(dto.Confidence >= 0.8);
        }

        [Fact]
        public void Preview_MarksHeaderWhenNoPurchaseLines()
        {
            var text = """
                Facture F-2026-88
                ICE : 001234567890123
                Total HT : 1 000,00
                Total TTC : 1 200,00
                """;

            var preview = AccountingOcrInvoiceImport.Preview(text, parser: null);

            Assert.Equal("header", preview.Source);
            Assert.Equal(0, preview.LineCount);
            Assert.Equal(1000m, preview.AmountHt);
        }

        [Fact]
        public void Classify_InvoiceVsBankVsDelivery()
        {
            var invoice = MoroccanDocumentParser.Classify("Facture F-12\nTotal HT 100\nTotal TTC 120");
            Assert.Equal("facture", invoice.Type);

            var bank = MoroccanDocumentParser.Classify(
                "Relevé de compte CIH Bank\nSolde précédent 1 000,00\nNouveau solde 2 000,00");
            Assert.Equal("releve_bancaire", bank.Type);

            var ofx = MoroccanDocumentParser.Classify("OFXHEADER:100", "releve.ofx");
            Assert.Equal("releve_bancaire", ofx.Type);

            var delivery = MoroccanDocumentParser.Classify("Bon de livraison BL-44\nLeveringsbon");
            Assert.Equal("bon_livraison", delivery.Type);
        }

        [Fact]
        public void FromLocal_HintOverridesClassifier()
        {
            var dto = AccountingOcrInvoiceImport.FromLocal(
                "Relevé de compte CIH Bank\nSolde précédent 10,00",
                "scan.pdf",
                parser: null,
                hint: "facture");
            Assert.Equal("facture", dto.DocumentType);
            Assert.NotNull(dto.Invoice);
        }

        [Fact]
        public void BankStatement_ParsesOcrLines()
        {
            var text = """
                10/03/2026 VIR CLIENT CR 1 210,00
                11/03/2026 CHQ 112233 DB 80,00
                """;

            var lines = MoroccanDocumentParser.ParseBankStatement(text);

            Assert.Equal(2, lines.Count);
            Assert.Equal(1210m, lines[0].Credit);
            Assert.Equal(80m, lines[1].Debit);
        }
    }
}
