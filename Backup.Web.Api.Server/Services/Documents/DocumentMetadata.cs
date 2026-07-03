using System;

namespace Backup.Web.Api.Server.Services.Documents
{
    public class DocumentMetadata
    {
        public string TypeDocument { get; set; } = string.Empty; // "Facture" | "BonLivraison"
        public string? Numero { get; set; }
        public string? Client { get; set; }
        public DateTime? DateDocument { get; set; }
        public string? Supplier { get; set; }
        public string? SupplierCode { get; set; }
        public string? SupplierAddress { get; set; }
        public string? SupplierPhone { get; set; }
        public string? SupplierEmail { get; set; }
        public string? SupplierContact { get; set; }
        public string? SupplierPaymentTerms { get; set; }
    }
}


