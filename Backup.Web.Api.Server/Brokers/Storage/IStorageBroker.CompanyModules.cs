using System.Linq;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Models.Entities.SaaS;

namespace Backup.Web.Api.Server.Brokers.Storage
{
    public partial interface IStorageBroker
    {
        IQueryable<CompanyModule> SelectAllCompanyModules();
        ValueTask<CompanyModule?> SelectCompanyModuleByIdAsync(string id);
        ValueTask<CompanyModule> InsertCompanyModuleAsync(CompanyModule module);
        ValueTask<CompanyModule> UpdateCompanyModuleAsync(CompanyModule module);
    }
}
