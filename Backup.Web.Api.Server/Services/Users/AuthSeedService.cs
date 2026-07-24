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
        if (existing != null)
            return;

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

        var role = string.IsNullOrWhiteSpace(options.Role) ? "Admin" : options.Role.Trim();
        if (!await userManager.IsInRoleAsync(user, role))
        {
            var addRole = await userManager.AddToRoleAsync(user, role);
            if (!addRole.Succeeded)
            {
                logger.LogWarning(
                    "Auth seed: user created but role {Role} failed: {Errors}",
                    role,
                    string.Join(", ", addRole.Errors.Select(e => e.Description)));
            }
        }

        logger.LogInformation("Auth seed: admin user ready ({Email})", email);
    }
}
