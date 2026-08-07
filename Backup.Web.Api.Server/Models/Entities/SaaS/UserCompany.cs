using System;
using Backup.Web.Api.Server.Models.Users;
using Backup.Web.Api.Server.Services.Audit;

namespace Backup.Web.Api.Server.Models.Entities.SaaS
{
    public class UserCompany : IHasAuditTrail
    {
        public Guid UserId { get; set; }
        public User? User { get; set; }
        public string CompanyId { get; set; } = string.Empty;
        public Company? Company { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public string? CreatedBy { get; set; }
        public string? UpdatedBy { get; set; }
    }
}
