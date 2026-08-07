using System;
using System.Collections.Generic;

namespace Backup.Web.Api.Server.Models.Entities
{
using Backup.Web.Api.Server.Services.Audit;
using Backup.Web.Api.Server.Services.Tenancy;

    public class Customer : IHasCompanyId, IHasAuditTrail
    {
        public int Id { get; set; }
        public string CustomerCode { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? VatNumber { get; set; }
        public string? Address { get; set; }
        public string? City { get; set; }
        public string? PostalCode { get; set; }
        public string? Country { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public decimal Balance { get; set; } = 0m;
        /// <summary>Plafond d'encours TTC (RG-T5). 0 = illimité.</summary>
        public decimal CreditLimit { get; set; } = 0m;
        /// <summary>Conditions de paiement (ex: "30 jours", "60D EOM"). RG-EC1.</summary>
        public string? PaymentTerms { get; set; }
        /// <summary>RG-CT2 : Active | Blocked | Closed.</summary>
        public string Status { get; set; } = "Active";
        public string? CompanyId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public string? CreatedBy { get; set; }
        public string? UpdatedBy { get; set; }
    }
}
