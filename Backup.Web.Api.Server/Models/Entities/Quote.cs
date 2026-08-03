using System;
using System.Collections.Generic;
using Backup.Web.Api.Server.Services.Sales;
using Backup.Web.Api.Server.Services.Tenancy;

namespace Backup.Web.Api.Server.Models.Entities
{
    public class Quote : IHasCompanyId, IHasSoftDelete, IHasArchive
    {
        public int Id { get; set; }
        public string QuoteNumber { get; set; } = string.Empty;
        public int CustomerId { get; set; }
        public Customer? Customer { get; set; }
        public DateTime Date { get; set; } = DateTime.UtcNow;
        public DateTime ExpirationDate { get; set; } = DateTime.UtcNow.AddDays(30);
        public string Status { get; set; } = "Draft"; // Draft, Sent, Accepted, Rejected, PartiallyConverted, Converted
        /// <summary>RG-DV7 : incrémenté à chaque modification après envoi (Sent/Accepted) — historique de versionning.</summary>
        public int Version { get; set; } = 1;
        public decimal TotalHT { get; set; }
        public decimal TotalVat { get; set; }
        public decimal TotalTTC { get; set; }
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

        public List<QuoteLine> Lines { get; set; } = new();
    }

    public class QuoteLine
    {
        public int Id { get; set; }
        public int QuoteId { get; set; }
        public Quote? Quote { get; set; }
        public string ProductKey { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        /// <summary>RG-DV3 : quantité déjà convertie en commande(s) (conversion partielle multi-CDC).</summary>
        public decimal ConvertedQuantity { get; set; } = 0m;
        public decimal UnitPrice { get; set; }
        /// <summary>RG-RM1–5 : remise ligne (%), 0-100.</summary>
        public decimal DiscountPercent { get; set; }
        public decimal VatRate { get; set; } = 21.0m;
        public decimal TotalHT { get; set; }
        public decimal TotalTTC { get; set; }
        public int LineNumber { get; set; }
        /// <summary>Fournisseur associé à la ligne (info / marge, optionnel).</summary>
        public int? SupplierId { get; set; }
    }
}
