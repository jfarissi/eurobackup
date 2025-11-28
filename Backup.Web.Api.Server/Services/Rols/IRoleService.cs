
using Backup.Web.Api.Server.Models.Users;
using System;
using System.Linq;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Models.Rols;

namespace Backup.Web.Api.Server.Services.Roles
{
    public interface IRoleService
    {
        ValueTask<bool> RegisterRoleAsync(Role Role);
        //ValueTask<AuthenticateResponse> RegisterSocialRoleAsync(Role Role, string provider, string providerKey);
        ValueTask<Role> DeleteRoleAsync(Guid RoleId);
        ValueTask<Role> RetrieveRoleByIdAsync(Guid RoleId);
        IQueryable<Role> RetrieveAllRoles();
        ValueTask<Role> ModifyRoleAsync(Role course);

        ValueTask<bool> RoleExistAsync(Role Role);
        //ValueTask<AuthenticateResponse> Authenticate(AuthenticateRequest model);
        ValueTask<Role> RetrieveRoleByNameAsync(string roleName);
    }
}
