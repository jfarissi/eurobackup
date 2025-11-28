using BCrypt.Net;
using Backup.Web.Api.Server.Authorization;
using Backup.Web.Api.Server.Brokers.RoleManagement;
using Backup.Web.Api.Server.Models.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Models.AppSettings;
using Backup.Web.Api.Server.Models.Entities;


namespace Backup.Web.Api.Server.Brokers.UserManagement
{
    public class UserManagementBroker : IUserManagementBroker
    {
        private readonly UserManager<User> userManagement;
        private readonly IRoleManagementBroker roleManagement;
        private IJwtUtils _jwtUtils;

        public UserManagementBroker(
        IJwtUtils jwtUtils,
            UserManager<User> userManager,
            IRoleManagementBroker roleManager)
        {
            _jwtUtils = jwtUtils;
            this.userManagement = userManager;
            this.roleManagement = roleManager;

        }

        //public UserManagementBroker(UserManager<User> userManagement, RoleManager<User> roleManager)
        //{
        //    this.userManagement = userManagement;
        //    this.roleManager = roleManager;

        //}

        public async ValueTask<AuthenticateResponse> Authenticate(AuthenticateRequest model, User user)
        {

            // authentication successful so generate jwt token
            var roles = await this.roleManagement.SelectAllRoles().ToListAsync();
            user.IsAdmin = await this.userManagement.IsInRoleAsync(user, "Admin");
            //IList<string> listrole = await this.userManagement.GetRolesAsync(user);
            //user.Role = await this.roleManagement.SelectRoleByNameAsync(listrole.First().ToUpper());
            var jwtToken = _jwtUtils.GenerateJwtToken(user);

            return new AuthenticateResponse(user, jwtToken);
        }

        public IQueryable<User> SelectAllUsers() => this.userManagement.Users;

        public async ValueTask<User> SelectUserByIdAsync(Guid userId)
        {
            return await this.userManagement.FindByIdAsync(userId.ToString());
        }

        public async ValueTask<User> SelectUserByEmailAsync(string email)
        {
            return await this.userManagement.FindByEmailAsync(email);
        }

        public async ValueTask<AuthenticateResponse> SocialLogin(User user, string provider, string providerKey)
        {
            var loginInfo = new UserLoginInfo(provider, providerKey, provider);
            var existingUser = await this.userManagement.FindByLoginAsync(provider, providerKey);

            if (existingUser == null)
            {
                var createResult = await this.userManagement.CreateAsync(user);
                if (!createResult.Succeeded)
                {
                    throw new Exception($"Échec de la création de l'utilisateur : {string.Join(", ", createResult.Errors.Select(e => e.Description))}");
                }

                var roleAdded = await AddRoleAsync(user);
                if (roleAdded == null)
                {
                    throw new Exception("Échec de l'ajout du rôle à l'utilisateur.");
                }

                var addLoginResult = await this.userManagement.AddLoginAsync(user, loginInfo);
                if (!addLoginResult.Succeeded)
                {
                    throw new Exception($"Échec de l'ajout du login externe : {string.Join(", ", addLoginResult.Errors.Select(e => e.Description))}");
                }

                existingUser = user; // L'utilisateur vient d'être créé, on le réutilise directement
                var jwtToken = _jwtUtils.GenerateJwtToken(existingUser);

                return new AuthenticateResponse(existingUser, jwtToken);
            }
            else
            {

                var jwtToken = _jwtUtils.GenerateJwtToken(user);

                return new AuthenticateResponse(user, jwtToken);
            }

            // Vérifier et affecter un rôle à l'utilisateur s'il n'en a pas
            //var roles = await this.userManagement.GetRolesAsync(existingUser);
            //if (!roles.Any())
            //{
            //    var roleAdded = await AddRoleAsync(existingUser);
            //    if (roleAdded == null)
            //    {
            //        throw new Exception("Échec de l'ajout du rôle à l'utilisateur.");
            //    }
            //}

            //// Récupérer le rôle final et le stocker dans l'objet User
            //var finalRoleName = (await this.userManagement.GetRolesAsync(existingUser)).FirstOrDefault();
            //existingUser.Role = finalRoleName != null ? await this.roleManagement.SelectRoleByNameAsync(finalRoleName) : null;

            // Vérifier si l'utilisateur est admin
            //existingUser.IsAdmin = await this.userManagement.IsInRoleAsync(existingUser, "Admin");

            // Générer le JWT token

        }

        public async ValueTask<User> InsertUserAsync(User user, string password)
        {
            try
            {
                //var broker = new UserManagementBroker(this.userManagement);
                var createResult = await this.userManagement.CreateAsync(user, password);
                //await this.userManagement.UpdateSecurityStampAsync(user);
                //await this.userManagement.UpdateAsync(user);
                if (!createResult.Succeeded)
                {
                    Console.WriteLine($"Erreur lors de la création de l'utilisateur : {string.Join(", ", createResult.Errors.Select(e => e.Description))}");
                    return new User();
                }
                await AddRoleAsync(user);
                return user;

                //await this.userManagement.CreateAsync(user, password);
                //IdentityResult result = await userManagement.CreateAsync(user, password);
                //await this.SaveChangesAsync();
                //User useradded = SelectUserByIdAsync(user.Id).Result;
                //return user;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
        public async ValueTask<User> InsertSocialUserAsync(User user)
        {
            try
            {
                //var broker = new UserManagementBroker(this.userManagement);
                await this.userManagement.CreateAsync(user);
                //await this.userManagement.UpdateSecurityStampAsync(user);
                //await this.userManagement.UpdateAsync(user);
                await AddRoleAsync(user);
                return user;

                //await this.userManagement.CreateAsync(user, password);
                //IdentityResult result = await userManagement.CreateAsync(user, password);
                //await this.SaveChangesAsync();
                //User useradded = SelectUserByIdAsync(user.Id).Result;
                return user;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
        public async ValueTask<User> UpdateUserAsync(User user)
        {
            await this.userManagement.UpdateAsync(user);

            return user;
        }

        public async ValueTask<User> DeleteUserAsync(User user)
        {
            await this.userManagement.DeleteAsync(user);

            return user;
        }

        public async ValueTask<User> AddRoleAsync(User user)
        {
            var roleCheck = await this.roleManagement.RoleExistsAsync(user.Role);
            if (!roleCheck)
            {
                //create the roles and seed them to the database
                await this.roleManagement.InsertRoleAsync(user.Role);
            }
            //Assign Admin role to the main User here we have given our newly registered 
            //login id for Admin management
            //User getuser = await UserManager.FindByEmailAsync("jfarissi@gmail.com");
            await this.userManagement.AddToRoleAsync(user, user.Role.Name);
            return user;
        }
        public async ValueTask<IList<string>> SelectRoleByUserAsync(User user)
        {
            return await this.userManagement.GetRolesAsync(user);
        }
    }

}
