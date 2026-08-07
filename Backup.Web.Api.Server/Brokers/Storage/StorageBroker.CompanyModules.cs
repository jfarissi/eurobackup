using System.Linq;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Models.Entities.SaaS;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Backup.Web.Api.Server.Brokers.Storage
{
    public partial class StorageBroker
    {
        public DbSet<CompanyModule> CompanyModules { get; set; } = null!;

        public IQueryable<CompanyModule> SelectAllCompanyModules() => this.CompanyModules.AsQueryable();

        public async ValueTask<CompanyModule?> SelectCompanyModuleByIdAsync(string id) =>
            await this.CompanyModules.FindAsync(id);

        public async ValueTask<CompanyModule> InsertCompanyModuleAsync(CompanyModule module)
        {
            EntityEntry<CompanyModule> entry = await this.CompanyModules.AddAsync(module);
            await this.SaveChangesAsync();
            return entry.Entity;
        }

        public async ValueTask<CompanyModule> UpdateCompanyModuleAsync(CompanyModule module)
        {
            this.CompanyModules.Update(module);
            await this.SaveChangesAsync();
            return module;
        }
    }
}
