using System;
using Backup.Web.Api.Server.Services.Tenancy;

namespace Backup.Web.Api.Server.Models.Entities
{
    /// <summary>P3 — Historique qui / quoi / quand sur les documents ventes.</summary>
    public class DocumentAuditLog : IHasCompanyId
    {
        public int Id { get; set; }
        /// <summary>Order, DeliveryNote, Invoice, Quote, CreditNote</summary>
        public string DocumentType { get; set; } = string.Empty;
        public int DocumentId { get; set; }
        /// <summary>Created, Updated, Confirmed, Approved, Held, Cancelled, Deleted, Validated, Applied, ...</summary>
        public string Action { get; set; } = string.Empty;
        public string? Summary { get; set; }
        public string? Details { get; set; }
        public string? Actor { get; set; }
        public string? CompanyId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
