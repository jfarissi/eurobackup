using System;
using Backup.Web.Api.Server.Services.Audit;

namespace Backup.Web.Api.Server.Models
{
    public class Document : Backup.Web.Api.Server.Services.Tenancy.IHasCompanyId, IHasAuditTrail
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

        public string? CompanyId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public string? CreatedBy { get; set; }
        public string? UpdatedBy { get; set; }
    }
}


