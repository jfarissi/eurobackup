using System;
using System.Linq;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Models.Entities.SaaS;

namespace Backup.Web.Api.Server.Brokers.Storage
{
    public partial interface IStorageBroker
    {
        ValueTask<Tenant> InsertTenantAsync(Tenant tenant);
        IQueryable<Tenant> SelectAllTenants();
        ValueTask<Tenant> UpdateTenantAsync(Tenant tenant);
        ValueTask<Company> InsertCompanyAsync(Company company);
        IQueryable<Company> SelectAllCompanies();
        ValueTask<Company?> SelectCompanyByIdAsync(string id);
        ValueTask<Company> UpdateCompanyAsync(Company company);
        ValueTask<UserCompany> InsertUserCompanyAsync(UserCompany link);
        IQueryable<UserCompany> SelectUserCompaniesByUserId(Guid userId);
        ValueTask<bool> UserHasCompanyAccessAsync(Guid userId, string companyId);
        ValueTask DeleteUserCompanyAsync(UserCompany link);
    }
}
