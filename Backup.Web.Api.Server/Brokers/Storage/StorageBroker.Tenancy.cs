using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Models.Entities.SaaS;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Backup.Web.Api.Server.Brokers.Storage
{
    public partial class StorageBroker
    {
        public DbSet<Tenant> Tenants { get; set; } = null!;
        public DbSet<Company> Companies { get; set; } = null!;
        public DbSet<UserCompany> UserCompanies { get; set; } = null!;

        public async ValueTask<Tenant> InsertTenantAsync(Tenant tenant)
        {
            var entry = await this.Tenants.AddAsync(tenant);
            await this.SaveChangesAsync();
            return entry.Entity;
        }

        public IQueryable<Tenant> SelectAllTenants() => this.Tenants.AsQueryable();

        public async ValueTask<Tenant> UpdateTenantAsync(Tenant tenant)
        {
            var entry = this.Tenants.Update(tenant);
            await this.SaveChangesAsync();
            return entry.Entity;
        }

        public async ValueTask<Company> InsertCompanyAsync(Company company)
        {
            var entry = await this.Companies.AddAsync(company);
            await this.SaveChangesAsync();
            return entry.Entity;
        }

        public IQueryable<Company> SelectAllCompanies() => this.Companies.Include(c => c.Tenant).AsQueryable();

        public async ValueTask<Company?> SelectCompanyByIdAsync(string id) =>
            await this.Companies.FirstOrDefaultAsync(c => c.Id == id);

        public async ValueTask<Company> UpdateCompanyAsync(Company company)
        {
            var entry = this.Companies.Update(company);
            await this.SaveChangesAsync();
            return entry.Entity;
        }

        public async ValueTask<UserCompany> InsertUserCompanyAsync(UserCompany link)
        {
            var entry = await this.UserCompanies.AddAsync(link);
            await this.SaveChangesAsync();
            return entry.Entity;
        }

        public IQueryable<UserCompany> SelectUserCompaniesByUserId(Guid userId) =>
            this.UserCompanies.Include(uc => uc.Company).Where(uc => uc.UserId == userId);

        public async ValueTask<bool> UserHasCompanyAccessAsync(Guid userId, string companyId) =>
            await this.UserCompanies.AnyAsync(uc => uc.UserId == userId && uc.CompanyId == companyId);

        public async ValueTask DeleteUserCompanyAsync(UserCompany link)
        {
            this.UserCompanies.Remove(link);
            await this.SaveChangesAsync();
        }
    }
}
