using System.Linq;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Backup.Web.Api.Server.Brokers.Storage
{
    public partial interface IStorageBroker
    {
        IQueryable<SalesCustomerProfile> SelectAllSalesCustomerProfiles();
        ValueTask<SalesCustomerProfile> InsertSalesCustomerProfileAsync(SalesCustomerProfile profile);
        ValueTask<SalesCustomerProfile> UpdateSalesCustomerProfileAsync(SalesCustomerProfile profile);
    }

    public partial class StorageBroker
    {
        public DbSet<SalesCustomerProfile> SalesCustomerProfiles { get; set; } = null!;

        public IQueryable<SalesCustomerProfile> SelectAllSalesCustomerProfiles() =>
            this.SalesCustomerProfiles.AsQueryable();

        public async ValueTask<SalesCustomerProfile> InsertSalesCustomerProfileAsync(SalesCustomerProfile profile)
        {
            EntityEntry<SalesCustomerProfile> entry = await this.SalesCustomerProfiles.AddAsync(profile);
            await this.SaveChangesAsync();
            return entry.Entity;
        }

        public async ValueTask<SalesCustomerProfile> UpdateSalesCustomerProfileAsync(SalesCustomerProfile profile)
        {
            EntityEntry<SalesCustomerProfile> entry = this.SalesCustomerProfiles.Update(profile);
            await this.SaveChangesAsync();
            return entry.Entity;
        }
    }
}
