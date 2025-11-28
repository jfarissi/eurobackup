




using Backup.Web.Api.Server.Models.Users;
using Backup.Web.Api.Server.Authorization;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace Backup.Web.Api.Server.Brokers.UserManagement
{
    public interface IUserManagementBroker
    {
            ValueTask<User> InsertUserAsync(User user, string password);
            ValueTask<User> InsertSocialUserAsync(User user);
            IQueryable<User> SelectAllUsers();
            ValueTask<User> SelectUserByIdAsync(Guid userId);
            ValueTask<User> UpdateUserAsync(User user);
            ValueTask<User> DeleteUserAsync(User user);
            ValueTask<User> SelectUserByEmailAsync(string email);
            ValueTask<AuthenticateResponse> Authenticate(AuthenticateRequest model, User user);
            ValueTask<AuthenticateResponse> SocialLogin(User user, string provider, string providerKey);
            ValueTask<IList<string>> SelectRoleByUserAsync(User user);
            ValueTask<User> AddRoleAsync(User user);
    }
}
