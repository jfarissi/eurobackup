




using Backup.Web.Api.Server.Models.Users;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Backup.Web.Api.Server.Services.Users
{
    public interface IUserService
    {
        ValueTask<User> RegisterUserAsync(User user, string password);
        //ValueTask<AuthenticateResponse> RegisterSocialUserAsync(User user, string provider, string providerKey);
        ValueTask<User> DeleteUserAsync(Guid userId);
        ValueTask<User> RetrieveUserByIdAsync(Guid userId);
        IQueryable<User> RetrieveAllUsers();
        ValueTask<User> ModifyUserAsync(User course);
        ValueTask<AuthenticateResponse> Authenticate(AuthenticateRequest model);
        ValueTask<User> RetrieveUserByEmailAsync(string email);
        ValueTask<string> SelectRoleByUserAsync(User user);
        ValueTask<User> AddRoleToUserAsync(User user);
    }
}
