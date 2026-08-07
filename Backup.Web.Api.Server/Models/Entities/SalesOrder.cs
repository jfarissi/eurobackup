using System;
using System.Collections.Generic;
using Backup.Web.Api.Server.Services.Audit;
using Backup.Web.Api.Server.Services.Sales;
using Backup.Web.Api.Server.Services.Tenancy;

namespace Backup.Web.Api.Server.Models.Entities
{
    public class SalesOrder : IHasCompanyId, IHasSoftDelete, IHasArchive, IHasAuditTrail
    {
        public int Id { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public int CustomerId { get; set; }
        public Customer? Customer { get; set; }
        public int? QuoteId { get; set; }
        public DateTime Date { get; set; } = DateTime.UtcNow;
        /// <summary>Draft, Pending (crédit/validation), Confirmed, PartiallyDelivered, Delivered, PartiallyInvoiced, Invoiced, Closed, Cancelled</summary>
        public string Status { get; set; } = "Draft";
        public decimal TotalHT { get; set; }
        public decimal TotalVat { get; set; }
        public decimal TotalTTC { get; set; }
        /// <summary>RG-CP3 : remise pied de page (%), appliquée sur le HT/TVA cumulés des lignes.</summary>
        public decimal HeaderDiscountPercent { get; set; }
        /// <summary>RG-CP1 : devise figée à la création (copiée de Company.DefaultCurrencyCode), gelée hors Draft.</summary>
        public string CurrencyCode { get; set; } = "EUR";
        public string? Notes { get; set; }
        /// <summary>RG-CT3 : adresse facturation figée à Confirm.</summary>
        public string? BillingAddress { get; set; }
        /// <summary>RG-CT3 : adresse livraison figée à Confirm.</summary>
        public string? ShippingAddress { get; set; }
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

        public List<SalesOrderLine> Lines { get; set; } = new();
    }

    public class SalesOrderLine : IHasAuditTrail
    {
        public int Id { get; set; }
        public int SalesOrderId { get; set; }
        public SalesOrder? SalesOrder { get; set; }
        public string ProductKey { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal DeliveredQuantity { get; set; } = 0m;
        /// <summary>Quantité déjà facturée (RG-V8).</summary>
        public decimal InvoicedQuantity { get; set; } = 0m;
        /// <summary>P4 — quantité réservée en stock pour cette ligne.</summary>
        public decimal ReservedQuantity { get; set; } = 0m;
        public decimal UnitPrice { get; set; }
        /// <summary>RG-RM1–5 : remise ligne (%), 0-100.</summary>
        public decimal DiscountPercent { get; set; }
        public decimal VatRate { get; set; } = 21.0m;
        public decimal TotalHT { get; set; }
        public decimal TotalTTC { get; set; }
        public int LineNumber { get; set; }
        /// <summary>Fournisseur associé à la ligne (info / marge, optionnel).</summary>
        public int? SupplierId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public string? CreatedBy { get; set; }
        public string? UpdatedBy { get; set; }
    }
}
