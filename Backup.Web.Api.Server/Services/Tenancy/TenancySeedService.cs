using System;
using System.Linq;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Models.Entities.SaaS;
using Backup.Web.Api.Server.Models.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Backup.Web.Api.Server.Services.Tenancy
{
    public class TenancySeedService
    {
        public const string DefaultTenantId = "00000000-0000-0000-0000-000000000001";
        public const string DefaultCompanyId = "00000000-0000-0000-0000-000000000002";

        private readonly IStorageBroker storage;
        private readonly UserManager<User> userManager;
        private readonly ILogger<TenancySeedService> logger;

        public TenancySeedService(
            IStorageBroker storage,
            UserManager<User> userManager,
            ILogger<TenancySeedService> logger)
        {
            this.storage = storage;
            this.userManager = userManager;
            this.logger = logger;
        }

        public async Task EnsureDefaultsAsync()
        {
            if (!await this.storage.SelectAllTenants().AnyAsync())
            {
                await this.storage.InsertTenantAsync(new Tenant
                {
                    Id = DefaultTenantId,
                    Name = "Tenant principal",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                });
                this.logger.LogInformation("Created default tenant {TenantId}", DefaultTenantId);
            }

            if (!await this.storage.SelectAllCompanies().AnyAsync())
            {
                await this.storage.InsertCompanyAsync(new Company
                {
                    Id = DefaultCompanyId,
                    TenantId = DefaultTenantId,
                    Name = "Société principale",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                });
                this.logger.LogInformation("Created default company {CompanyId}", DefaultCompanyId);
            }

            var users = await this.userManager.Users.ToListAsync();
            foreach (var user in users)
            {
                var hasLink = await this.storage.SelectUserCompaniesByUserId(user.Id).AnyAsync();
                if (!hasLink)
                {
                    await this.storage.InsertUserCompanyAsync(new UserCompany
                    {
                        UserId = user.Id,
                        CompanyId = DefaultCompanyId
                    });
                }

                if (string.IsNullOrWhiteSpace(user.CompanyId))
                {
                    user.CompanyId = DefaultCompanyId;
                    await this.userManager.UpdateAsync(user);
                }
            }
        }
    }
}
