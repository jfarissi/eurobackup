using System;
using System.Collections.Generic;
using Backup.Web.Api.Server.Services.Sales;
using Backup.Web.Api.Server.Services.Tenancy;

namespace Backup.Web.Api.Server.Models.Entities
{
    public class SalesInvoice : IHasCompanyId, IHasSoftDelete, IHasArchive
    {
        public int Id { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public int CustomerId { get; set; }
        public Customer? Customer { get; set; }
        public int? SalesOrderId { get; set; }
        public DateTime Date { get; set; } = DateTime.UtcNow;
        public DateTime DueDate { get; set; } = DateTime.UtcNow.AddDays(30);
        public string Status { get; set; } = "Draft"; // Draft, Validated, Paid, PartiallyPaid, Cancelled
        public decimal TotalHT { get; set; }
        public decimal TotalVat { get; set; }
        public decimal TotalTTC { get; set; }
        public decimal PaidAmount { get; set; } = 0m;
        /// <summary>RG-CP3 : remise pied de page (%), appliquée sur le HT/TVA cumulés des lignes.</summary>
        public decimal HeaderDiscountPercent { get; set; }
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

        /// <summary>Somme des avoirs Applied (non persisté).</summary>
        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public decimal CreditedAmount { get; set; }

        /// <summary>Reste dû = TotalTTC - PaidAmount - CreditedAmount (non persisté).</summary>
        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public decimal RemainingAmount { get; set; }

        /// <summary>True si une BL livré/facturé est lié à cette facture (non persisté).</summary>
        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public bool HasDeliveredSource { get; set; }

        /// <summary>BL source à lier à la création (non persisté ; lien via SalesDeliveryNote.SalesInvoiceId).</summary>
        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public int? SalesDeliveryNoteId { get; set; }

        /// <summary>RG-BL7 : plusieurs BL → une facture (non persisté).</summary>
        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public System.Collections.Generic.List<int>? SalesDeliveryNoteIds { get; set; }

        /// <summary>RG-RG9 : true si Validated/PartiallyPaid, DueDate dépassée et solde restant dû (non persisté, calculé à la lecture).</summary>
        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public bool IsOverdue { get; set; }

        public List<SalesInvoiceLine> Lines { get; set; } = new();
    }

    public class SalesInvoiceLine
    {
        public int Id { get; set; }
        public int SalesInvoiceId { get; set; }
        public SalesInvoice? SalesInvoice { get; set; }
        public string ProductKey { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        /// <summary>Quantité facturée.</summary>
        public decimal Quantity { get; set; }
        /// <summary>Quantité commandée d’origine (traçabilité).</summary>
        public decimal OrderedQuantity { get; set; }
        /// <summary>Quantité livrée (BL) reprise à la facturation.</summary>
        public decimal DeliveredQuantity { get; set; }
        public decimal UnitPrice { get; set; }
        /// <summary>RG-RM1–5 : remise ligne (%), 0-100.</summary>
        public decimal DiscountPercent { get; set; }
        public decimal VatRate { get; set; } = 21.0m;
        public decimal TotalHT { get; set; }
        public decimal TotalTTC { get; set; }
        public int LineNumber { get; set; }
        /// <summary>RG-LS1–5 lite : n° de lot repris depuis le BL source (traçabilité simple, sans FEFO).</summary>
        public string? LotNumber { get; set; }
    }
}
