using System;
using System.Collections.Generic;

namespace Backup.Web.Api.Server.Models.Entities
{
using Backup.Web.Api.Server.Services.Audit;
using Backup.Web.Api.Server.Services.Tenancy;

    public class PurchaseOrder : IHasCompanyId, IHasAuditTrail
    {
        public int Id { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public int SupplierId { get; set; }
        public Supplier? Supplier { get; set; }
        public DateTime Date { get; set; } = DateTime.UtcNow;
        public DateTime? ExpectedDeliveryDate { get; set; }
        public string Status { get; set; } = "Draft"; // Draft, Sent, Received, Invoiced, Cancelled
        public decimal TotalHT { get; set; }
        public decimal TotalVat { get; set; }
        public decimal TotalTTC { get; set; }
        /// <summary>RG-CP3 : remise pied de page (%), appliquée sur les marchandises (hors FDP).</summary>
        public decimal HeaderDiscountPercent { get; set; }
        /// <summary>RG-FA1 / RG-FA3 : frais de port / approche HT (forfait en-tête).</summary>
        public decimal ShippingAmountHt { get; set; }
        /// <summary>RG-FA1 : TVA applicable aux frais de port en-tête.</summary>
        public decimal ShippingVatRate { get; set; } = 21.0m;
        /// <summary>RG-CP1 : devise figée à la création (copiée de Company.DefaultCurrencyCode), gelée hors Draft.</summary>
        public string CurrencyCode { get; set; } = "EUR";
        public string? Notes { get; set; }
        /// <summary>F8 : commande vente d'origine si CDF dropship auto.</summary>
        public int? SalesOrderId { get; set; }
        public string? CompanyId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public string? CreatedBy { get; set; }
        public string? UpdatedBy { get; set; }

        public List<PurchaseOrderLine> Lines { get; set; } = new();
        public List<SupplierInvoiceEntity> SupplierInvoices { get; set; } = new();
    }

    public class PurchaseOrderLine : IHasAuditTrail
    {
        public int Id { get; set; }
        public int PurchaseOrderId { get; set; }
        public PurchaseOrder? PurchaseOrder { get; set; }
        public string ProductKey { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal ReceivedQuantity { get; set; } = 0m;
        /// <summary>RG-CF6 : quantité déjà facturée par une facture fournisseur (matching 3 voies).</summary>
        public decimal InvoicedQuantity { get; set; } = 0m;
        public decimal UnitPrice { get; set; }
        /// <summary>RG-RM1 : remise ligne (%), 0-100.</summary>
        public decimal DiscountPercent { get; set; }
        public decimal VatRate { get; set; } = 21.0m;
        public decimal TotalHT { get; set; }
        public decimal TotalTTC { get; set; }
        public int LineNumber { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public string? CreatedBy { get; set; }
        public string? UpdatedBy { get; set; }
    }
}
