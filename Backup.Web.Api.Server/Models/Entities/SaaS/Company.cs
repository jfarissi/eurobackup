using System;
using System.Collections.Generic;

namespace Backup.Web.Api.Server.Models.Entities.SaaS
{
    /// <summary>Société métier — périmètre d'isolation des données (CompanyId).</summary>
    public class Company
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string TenantId { get; set; } = string.Empty;
        public Tenant? Tenant { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public string DefaultLanguageCode { get; set; } = "fr-FR";
        public string DefaultCurrencyCode { get; set; } = "EUR";
        public string? PublicDomain { get; set; }
        /// <summary>RG-CS2 : si false (défaut), le stock ne peut pas passer négatif.</summary>
        public bool AllowNegativeStock { get; set; }
        /// <summary>RG-CO3 : début de l'exercice comptable ouvert (null = pas de contrôle).</summary>
        public DateTime? OpenFiscalPeriodStart { get; set; }
        /// <summary>RG-CO3 : fin de l'exercice comptable ouvert (null = pas de contrôle).</summary>
        public DateTime? OpenFiscalPeriodEnd { get; set; }
        /// <summary>RG-S3 : durée de rétention (mois) avant archivage auto des documents clôturés/annulés.</summary>
        public int RetentionMonths { get; set; } = 24;
        /// <summary>RG-RS2 : si true, la confirmation d'une commande échoue si le stock ne peut pas couvrir intégralement la réservation (pas de réservation partielle silencieuse).</summary>
        public bool RequireHardAllocation { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public List<UserCompany> UserCompanies { get; set; } = new();
    }
}
