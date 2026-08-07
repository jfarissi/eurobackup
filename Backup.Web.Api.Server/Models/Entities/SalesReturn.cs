using System;
using System.Collections.Generic;
using Backup.Web.Api.Server.Services.Audit;
using Backup.Web.Api.Server.Services.Sales;
using Backup.Web.Api.Server.Services.Tenancy;

namespace Backup.Web.Api.Server.Models.Entities
{
    /// <summary>
    /// Bon de retour client (BRC) : RG-BR1–5. Toujours lié à un BL livré/facturé.
    /// Cycle : Draft → Received (stock In) → Controlled (qualité) → Integrated (avoir éventuel) / Cancelled.
    /// </summary>
    public class SalesReturn : IHasCompanyId, IHasSoftDelete, IHasArchive, IHasAuditTrail
    {
        public int Id { get; set; }
        public string ReturnNumber { get; set; } = string.Empty;
        public int CustomerId { get; set; }
        public Customer? Customer { get; set; }
        public int SalesDeliveryNoteId { get; set; }
        public SalesDeliveryNote? SalesDeliveryNote { get; set; }
        public int? SalesOrderId { get; set; }
        public SalesOrder? SalesOrder { get; set; }
        public DateTime ReturnDate { get; set; } = DateTime.UtcNow;
        /// <summary>Draft, Received, Controlled, Integrated, Cancelled</summary>
        public string Status { get; set; } = "Draft";
        /// <summary>Conforme, Degraded, NonRecoverable — statut qualité global (peut aussi être précisé par ligne).</summary>
        public string? QualityStatus { get; set; }
        public decimal TotalHT { get; set; }
        public decimal TotalVat { get; set; }
        public decimal TotalTTC { get; set; }
        /// <summary>RG-CP1 : devise figée à la création (copiée de Company.DefaultCurrencyCode), gelée hors Draft.</summary>
        public string CurrencyCode { get; set; } = "EUR";
        public string? Notes { get; set; }
        public string? CompanyId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }
        public string? DeletedBy { get; set; }
        public bool IsArchived { get; set; }
        public DateTime? ArchivedAt { get; set; }
        public string? ArchivedBy { get; set; }
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public string? CreatedBy { get; set; }
        public string? UpdatedBy { get; set; }

        /// <summary>True dès que le stock a été impacté (Receive/Integrate), pour piloter la réversibilité de Cancel.</summary>
        public bool StockApplied { get; set; }

        /// <summary>Avoir généré depuis ce retour (RG-AC4), si déjà créé.</summary>
        public int? CreditNoteId { get; set; }

        public List<SalesReturnLine> Lines { get; set; } = new();
    }

    public class SalesReturnLine : IHasAuditTrail
    {
        public int Id { get; set; }
        public int SalesReturnId { get; set; }
        public SalesReturn? SalesReturn { get; set; }
        public string ProductKey { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal VatRate { get; set; } = 21.0m;
        public decimal TotalHT { get; set; }
        public decimal TotalTTC { get; set; }
        public int LineNumber { get; set; }
        /// <summary>Conforme, Degraded, NonRecoverable — surcharge le statut qualité de l'en-tête pour cette ligne.</summary>
        public string? QualityStatus { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public string? CreatedBy { get; set; }
        public string? UpdatedBy { get; set; }
    }
}
