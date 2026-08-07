using System;
using Backup.Web.Api.Server.Services.Tenancy;

namespace Backup.Web.Api.Server.Models.Entities
{
    /// <summary>
    /// Historique CRUD automatique (CreatedBy / UpdatedBy) pour les entités IHasAuditTrail.
    /// </summary>
    public class EntityAuditLog : IHasCompanyId
    {
        public long Id { get; set; }
        /// <summary>Nom court du type (ex. Customer, SalesInvoice, ErpProduct).</summary>
        public string EntityType { get; set; } = string.Empty;
        /// <summary>Clé primaire sérialisée (int, guid, composite).</summary>
        public string EntityKey { get; set; } = string.Empty;
        /// <summary>Created, Updated, Deleted</summary>
        public string Action { get; set; } = string.Empty;
        public string? Summary { get; set; }
        /// <summary>Noms des propriétés modifiées (hors champs d'audit).</summary>
        public string? Details { get; set; }
        public string? Actor { get; set; }
        public string? CompanyId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
