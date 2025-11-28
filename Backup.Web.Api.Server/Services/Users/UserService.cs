using Backup.Web.Api.Server.Brokers.DateTimes;
using Backup.Web.Api.Server.Brokers.Loggings;
using Backup.Web.Api.Server.Brokers.RoleManagement;
using Backup.Web.Api.Server.Brokers.UserManagement;
using Backup.Web.Api.Server.Models;
using Backup.Web.Api.Server.Models.Users;
using Microsoft.AspNetCore.Identity;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Backup.Web.Api.Server.Services.Users
{
    public partial class UserService : IUserService
    {
        //private readonly IUserManagementBroker userManagementBroker;
        private readonly ILoggingBroker loggingBroker;
        private readonly IDateTimeBroker dateTimeBroker;
        //private readonly UserManager<User> userManagement;
        private readonly IUserManagementBroker _UserManagement;
        private readonly IRoleManagementBroker _roleManagement;

        public UserService(IUserManagementBroker userManagementBroker,
            ILoggingBroker loggingBroker,
            IDateTimeBroker dateTimeBroker,
            //UserManager<User> userManager,
            IUserManagementBroker UserManagement,
            IRoleManagementBroker roleManagementBroker)
        {
            this.loggingBroker = loggingBroker;
            this.dateTimeBroker = dateTimeBroker;
            //this.userManagement = userManager;
            this._UserManagement = UserManagement;
            this._roleManagement = roleManagementBroker;
        }

        public ValueTask<User> DeleteUserAsync(Guid userId) =>
        TryCatch(async () =>
        {
            ValidateUserIdIsNull(userId);

            User maybeUser =
               await this._UserManagement.SelectUserByIdAsync(userId);

            ValidateStorageUser(maybeUser, userId);

            return await this._UserManagement.DeleteUserAsync(maybeUser);
        });

        public ValueTask<User> ModifyUserAsync(User user) =>
        TryCatch(async () =>
        {
            ValidateUserOnModify(user);
            User maybeUser = await this._UserManagement.SelectUserByIdAsync(user.Id);

            return await this._UserManagement.UpdateUserAsync(user);
        });

        public ValueTask<User> RegisterUserAsync(User user, string password) =>
        TryCatch(async () =>
        {
            ValidateUserOnCreate(user, password);

            return await this._UserManagement.InsertUserAsync(user, password);
        });
        //public ValueTask<AuthenticateResponse> RegisterSocialUserAsync(User user, string provider, string providerKey) =>
        //TryCatch(async () =>
        //{
        //    ValidateUserOnCreate(user, "");

        //    return await this._UserManagement.SocialLogin(user, provider, providerKey);
        //});
        public IQueryable<User> RetrieveAllUsers() =>
        TryCatch(() =>
        {
            IQueryable<User> storageUsers = this._UserManagement.SelectAllUsers();
            ValidateStorageUsers(storageUsers);

            return storageUsers;
        });

        public ValueTask<User> RetrieveUserByIdAsync(Guid userId) =>
        TryCatch(async () =>
        {
            ValidateUserIdIsNull(userId);
            User storageUser = await this._UserManagement.SelectUserByIdAsync(userId);
            ValidateStorageUser(storageUser, userId);

            return storageUser;
        });
        public ValueTask<User> RetrieveUserByEmailAsync(string email) =>
        TryCatch(async () =>
        {
            ValidateUserEmail(email);
            User storageUser = await this._UserManagement.SelectUserByEmailAsync(email);
            //ValidateUserIsNull(storageUser);

            return storageUser;
        });
        public ValueTask<AuthenticateResponse> Authenticate(AuthenticateRequest model) =>
        TryCatch(async () =>
        {
            //var user = userManagement.Users.SingleOrDefault(x => x.UserName == model.Username);
            var user = await this._UserManagement.SelectUserByEmailAsync(model.Username);
            var RoleName = (await this._UserManagement.SelectRoleByUserAsync(user)).FirstOrDefault();
            user.Role = await this._roleManagement.SelectRoleByNameAsync(RoleName);
            // validate

            if (user == null || !BCrypt.Net.BCrypt.Verify(model.Password, user.PasswordHash))
                throw new AppException("Username or password is incorrect");
            return await this._UserManagement.Authenticate(model, user);

        });
        //public ValueTask<AuthenticateResponse> AuthenticateSocial(JwtSecurityToken token) =>
        //TryCatch(async () =>
        //{
        //    ValidateUserEmail(token.Claims.FirstOrDefault(c => c.Type == "email")?.Value);
        //    User storageUser = await this.userManagementBroker.SelectUserByNameAsync(email);
        //    ValidateUserIsNull(storageUser);

        //    return storageUser;

        //});
        public ValueTask<string> SelectRoleByUserAsync(User user) =>
        TryCatch(async () =>
        {
            ValidateUserOnModify(user);
            string rolename = (await this._UserManagement.SelectRoleByUserAsync(user)).FirstOrDefault();

            return rolename;
        });

        public ValueTask<User> AddRoleToUserAsync(User user) =>
        TryCatch(async () =>
        {
            ValidateUserOnCreate(user, "");

            return await this._UserManagement.AddRoleAsync(user);
        });
    }
}
