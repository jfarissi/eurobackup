using System;

namespace Backup.Web.Api.Server.Models.Entities
{
using Backup.Web.Api.Server.Services.Tenancy;

    public class DocumentNumberSequence : IHasCompanyId
    {
        public int Id { get; set; }
        public string DocumentType { get; set; } = string.Empty; // Quote, Order, Invoice, CreditNote, PurchaseOrder, DeliveryNote
        public string Prefix { get; set; } = string.Empty; // FAC-, DEV-, CMD-, etc.
        public int Year { get; set; } = DateTime.UtcNow.Year;
        public int NextNumber { get; set; } = 1;
        public string FormatPattern { get; set; } = "{Prefix}{Year}-{Number:D4}"; // e.g. FAC-2026-0001
        public string? CompanyId { get; set; }
    }
}
