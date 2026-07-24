using Backup.Web.Api.Server.Models.AppSettings;
using Backup.Web.Api.Server.Models.Rols;
using Backup.Web.Api.Server.Models.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Backup.Web.Api.Server.Services.Users;

public static class AuthSeedService
{
    public static async Task SeedAsync(IServiceProvider services, ILogger logger)
    {
        var options = services.GetRequiredService<IOptions<AuthSeedOptions>>().Value;
        if (!options.Enabled || string.IsNullOrWhiteSpace(options.Email) || string.IsNullOrWhiteSpace(options.Password))
            return;

        var userManager = services.GetRequiredService<UserManager<User>>();
        var roleManager = services.GetRequiredService<RoleManager<Role>>();

        foreach (var roleName in new[] { "Admin", "User" })
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                var createRole = await roleManager.CreateAsync(new Role
                {
                    Id = Guid.NewGuid(),
                    Name = roleName,
                    NormalizedName = roleName.ToUpperInvariant()
                });
                if (!createRole.Succeeded)
                {
                    logger.LogWarning(
                        "Auth seed: failed to create role {Role}: {Errors}",
                        roleName,
                        string.Join(", ", createRole.Errors.Select(e => e.Description)));
                }
            }
        }

        var email = options.Email.Trim();
        var existing = await userManager.FindByEmailAsync(email)
            ?? await userManager.FindByNameAsync(email);

        if (existing == null)
        {
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = email,
                UserName = email,
                Name = options.Name,
                FamilyName = options.FamilyName,
                EmailConfirmed = true,
                Status = UserStatus.Activated,
                CreatedDate = DateTimeOffset.UtcNow,
                UpdatedDate = DateTimeOffset.UtcNow
            };

            var createUser = await userManager.CreateAsync(user, options.Password);
            if (!createUser.Succeeded)
            {
                logger.LogError(
                    "Auth seed: failed to create admin {Email}: {Errors}",
                    email,
                    string.Join(", ", createUser.Errors.Select(e => e.Description)));
                return;
            }

            existing = user;
            logger.LogInformation("Auth seed: admin user created ({Email})", email);
        }
        else
        {
            // Repair legacy BCrypt / empty hashes so Identity CheckPasswordAsync works
            var hash = existing.PasswordHash;
            var needsPasswordRepair = string.IsNullOrWhiteSpace(hash)
                || hash.StartsWith("$2", StringComparison.Ordinal) // BCrypt
                || options.ForceResetPassword;

            if (needsPasswordRepair)
            {
                if (await userManager.HasPasswordAsync(existing))
                    await userManager.RemovePasswordAsync(existing);

                var addPassword = await userManager.AddPasswordAsync(existing, options.Password);
                if (!addPassword.Succeeded)
                {
                    logger.LogError(
                        "Auth seed: failed to reset password for {Email}: {Errors}",
                        email,
                        string.Join(", ", addPassword.Errors.Select(e => e.Description)));
                }
                else
                {
                    logger.LogInformation("Auth seed: password repaired for {Email}", email);
                }
            }
        }

        var role = string.IsNullOrWhiteSpace(options.Role) ? "Admin" : options.Role.Trim();
        if (!await userManager.IsInRoleAsync(existing, role))
        {
            var addRole = await userManager.AddToRoleAsync(existing, role);
            if (!addRole.Succeeded)
            {
                logger.LogWarning(
                    "Auth seed: user ready but role {Role} failed: {Errors}",
                    role,
                    string.Join(", ", addRole.Errors.Select(e => e.Description)));
            }
        }

        logger.LogInformation("Auth seed: admin user ready ({Email})", email);
    }
}
