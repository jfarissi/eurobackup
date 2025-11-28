




using Backup.Web.Api.Server.Models.Users;
using Backup.Web.Api.Server.Authorization;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Backup.Web.Api.Server.Models.Rols;

namespace Backup.Web.Api.Server.Brokers.RoleManagement
{
    public interface IRoleManagementBroker
    {
        ValueTask<bool> InsertRoleAsync(Role Role);
        IQueryable<Role> SelectAllRoles();
        ValueTask<Role> SelectRoleByIdAsync(Guid RoleId);
        ValueTask<Role> SelectRoleByNameAsync(string roleName);
        ValueTask<Role> UpdateRoleAsync(Role Role);
        ValueTask<Role> DeleteRoleAsync(Role Role);

        ValueTask<bool> RoleExistsAsync(Role Role);

    }
}
