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
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public List<UserCompany> UserCompanies { get; set; } = new();
    }
}
