using Microsoft.AspNetCore.Identity;
using Backup.Web.Api.Server.Models.Users;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using BCrypt.Net;
using Backup.Web.Api.Server.Authorization;
using Backup.Web.Api.Server.Models.AppSettings;
using Microsoft.Extensions.Options;
using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Models.Entities;
using Microsoft.Extensions.DependencyInjection;
using Backup.Web.Api.Server.Models.Rols;

namespace Backup.Web.Api.Server.Brokers.RoleManagement
{
    public class RoleManagementBroker : IRoleManagementBroker
    {
        private readonly RoleManager<Role> roleManagement;

        public RoleManagementBroker(
            RoleManager<Role> roleManager)
        {
            this.roleManagement = roleManager;

        }

        public IQueryable<Role> SelectAllRoles() => this.roleManagement.Roles;

        public async ValueTask<Role> SelectRoleByIdAsync(Guid RoleId)
        {
            return await this.roleManagement.FindByIdAsync(RoleId.ToString());
        }

        public async ValueTask<Role> SelectRoleByNameAsync(string roleName)
        {
            return await this.roleManagement.FindByNameAsync(roleName);
        }

        public async ValueTask<bool> InsertRoleAsync(Role Role)
        {
            try
            {
                IdentityResult roleResult;

                //Role role = new Role();
                //role.Name = Role;

                roleResult = await this.roleManagement.CreateAsync(Role);
                return roleResult.Succeeded;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public async ValueTask<Role> UpdateRoleAsync(Role Role)
        {
            await this.roleManagement.UpdateAsync(Role);

            return Role;
        }

        public async ValueTask<Role> DeleteRoleAsync(Role Role)
        {
            await this.roleManagement.DeleteAsync(Role);

            return Role;
        }

        public async ValueTask<bool> RoleExistsAsync(Role Role)
        {
            bool roleexist = await this.roleManagement.RoleExistsAsync(Role.Name);

            return roleexist;
        }
    }
}
