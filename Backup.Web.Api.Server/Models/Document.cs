using System;

namespace Backup.Web.Api.Server.Models
{
    public class Document
    {
        public int Id { get; set; }

        public string TypeDocument { get; set; } = string.Empty; // Facture / BonLivraison / Autre

        public string? Numero { get; set; }

        public string? Client { get; set; }

        public string? Supplier { get; set; }

        public DateTime? DateDocument { get; set; }

        public string OriginalFileName { get; set; } = string.Empty;

        public string FilePath { get; set; } = string.Empty; // relative path under storage root

        public string ContentText { get; set; } = string.Empty; // extracted text for search

        public DateTime DateAdded { get; set; } = DateTime.UtcNow;
    }
}


