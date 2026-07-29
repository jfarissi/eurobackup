using System;
using Backup.Web.Api.Server.Models.Users;

namespace Backup.Web.Api.Server.Models.Entities.SaaS
{
    public class UserCompany
    {
        public Guid UserId { get; set; }
        public User? User { get; set; }
        public string CompanyId { get; set; } = string.Empty;
        public Company? Company { get; set; }
    }
}
