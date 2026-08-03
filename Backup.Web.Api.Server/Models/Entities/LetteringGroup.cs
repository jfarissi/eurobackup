using System;
using System.Collections.Generic;
using Backup.Web.Api.Server.Services.Tenancy;

namespace Backup.Web.Api.Server.Models.Entities
{
    /// <summary>
    /// RG-LT1–4 lite : lettrage client (rapprochement factures/règlements/avoirs) simplifié —
    /// pas de moteur de proposition automatique, saisie manuelle des lignes à lettrer ensemble.
    /// </summary>
    public class LetteringGroup : IHasCompanyId
    {
        public int Id { get; set; }
        public string LetteringCode { get; set; } = string.Empty;
        public string? CompanyId { get; set; }
        public int CustomerId { get; set; }
        public Customer? Customer { get; set; }
        /// <summary>Closed (lettré), Unlettered (délettré)</summary>
        public string Status { get; set; } = "Closed";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string? CreatedBy { get; set; }
        public DateTime? UnletteredAt { get; set; }
        public string? UnletteredBy { get; set; }

        public List<LetteringLine> Lines { get; set; } = new();
    }

    public class LetteringLine
    {
        public int Id { get; set; }
        public int LetteringGroupId { get; set; }
        public LetteringGroup? LetteringGroup { get; set; }
        public int? SalesInvoiceId { get; set; }
        public int? PaymentId { get; set; }
        public int? CreditNoteId { get; set; }
        public decimal Amount { get; set; }
    }
}
