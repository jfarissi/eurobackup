using Backup.Web.Api.Server.Brokers.DateTimes;
using Backup.Web.Api.Server.Brokers.Loggings;
using Backup.Web.Api.Server.Brokers.RoleManagement;
using Backup.Web.Api.Server.Models;
using Backup.Web.Api.Server.Models.Roles;
using Microsoft.AspNetCore.Identity;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Backup.Web.Api.Server.Models.AppSettings;
using Backup.Web.Api.Server.Models.Entities;
using Backup.Web.Api.Server.Brokers.RoleManagement;
using Backup.Web.Api.Server.Models.Rols;

namespace Backup.Web.Api.Server.Services.Roles
{
    public partial class RoleService : IRoleService
    {
        private readonly IRoleManagementBroker RoleManagementBroker;
        private readonly ILoggingBroker loggingBroker;
        private readonly IDateTimeBroker dateTimeBroker;
        private readonly RoleManager<Role> RoleManagement;
        private readonly IRoleManagementBroker _roleManagement;

        public RoleService(IRoleManagementBroker RoleManagementBroker,
            ILoggingBroker loggingBroker,
            IDateTimeBroker dateTimeBroker,
            RoleManager<Role> RoleManager,
            IRoleManagementBroker roleManagement)
        {
            this.RoleManagementBroker = RoleManagementBroker;
            this.loggingBroker = loggingBroker;
            this.dateTimeBroker = dateTimeBroker;
            this.RoleManagement = RoleManager;
            this._roleManagement = roleManagement;
        }

        public ValueTask<Role> DeleteRoleAsync(Guid RoleId) =>
        TryCatch(async () =>
        {
            ValidateRoleIdIsNull(RoleId);

            Role maybeRole =
               await this.RoleManagementBroker.SelectRoleByIdAsync(RoleId);

            ValidateStorageRole(maybeRole, RoleId);

            return await this.RoleManagementBroker.DeleteRoleAsync(maybeRole);
        });

        public ValueTask<Role> ModifyRoleAsync(Role Role) =>
        TryCatch(async () =>
        {
            ValidateRoleOnModify(Role);
            Role maybeRole = await this.RoleManagementBroker.SelectRoleByIdAsync(Role.Id);

            return await this.RoleManagementBroker.UpdateRoleAsync(Role);
        });

        public ValueTask<bool> RegisterRoleAsync(Role Role) =>
        TryCatch(async () =>
        {
            ValidateRoleOnCreate(Role);

            return await this.RoleManagementBroker.InsertRoleAsync(Role);
        });

        public ValueTask<bool> RoleExistAsync(Role Role) =>
        TryCatch(async () =>
        {
            ValidateRoleOnCreate(Role);

            return await this.RoleManagementBroker.RoleExistsAsync(Role);
        });
        //public ValueTask<AuthenticateResponse> RegisterSocialRoleAsync(Role Role, string provider,string providerKey) =>
        //TryCatch(async () =>
        //{
        //    ValidateRoleOnCreate(Role,"");

        //    return await this.RoleManagementBroker.SocialLogin(Role,provider,providerKey);
        //});
        public IQueryable<Role> RetrieveAllRoles() =>
        TryCatch(() =>
        {
            IQueryable<Role> storageRoles = this.RoleManagementBroker.SelectAllRoles();
            ValidateStorageRoles(storageRoles);

            return storageRoles;
        });

        public ValueTask<Role> RetrieveRoleByIdAsync(Guid RoleId) =>
        TryCatch(async () =>
        {
            ValidateRoleIdIsNull(RoleId);
            Role storageRole = await this.RoleManagementBroker.SelectRoleByIdAsync(RoleId);
            ValidateStorageRole(storageRole, RoleId);

            return storageRole;
        });
        public ValueTask<Role> RetrieveRoleByNameAsync(string roleName) =>
        TryCatch(async () =>
        {
            ValidateRoleEmail(roleName);
            Role storageRole = await this.RoleManagementBroker.SelectRoleByNameAsync(roleName);
            ValidateRoleIsNull(storageRole);

            return storageRole;
        });
        //public ValueTask<AuthenticateResponse> Authenticate(AuthenticateRequest model) =>
        //TryCatch(async () =>
        //{
        //    //var Role = RoleManagement.Roles.SingleOrDefault(x => x.RoleName == model.Rolename);
        //    var Role = await this.RoleManagementBroker.SelectRoleByNameAsync(model.Rolename);
        //    var roleName = (await this.RoleManagementBroker.SelectRoleByRoleAsync(Role)).FirstOrDefault();
        //    Role.Role = await this._roleManagement.SelectRoleByNameAsync(roleName);
        //    // validate

        //    if (Role == null || !BCrypt.Net.BCrypt.Verify(model.Password, Role.PasswordHash))
        //        throw new AppException("Rolename or password is incorrect");
        //    return await this.RoleManagementBroker.Authenticate(model, Role);

        //});
        //public ValueTask<AuthenticateResponse> AuthenticateSocial(JwtSecurityToken token) =>
        //TryCatch(async () =>
        //{
        //    ValidateRoleEmail(token.Claims.FirstOrDefault(c => c.Type == "email")?.Value);
        //    Role storageRole = await this.RoleManagementBroker.SelectRoleByNameAsync(email);
        //    ValidateRoleIsNull(storageRole);

        //    return storageRole;

        //});
    }
}
