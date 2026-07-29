using System;
using System.Collections.Generic;

namespace Backup.Web.Api.Server.Models.Entities.SaaS
{
    /// <summary>Client SaaS (abonnement) — regroupe plusieurs sociétés.</summary>
    public class Tenant
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public List<Company> Companies { get; set; } = new();
    }
}
