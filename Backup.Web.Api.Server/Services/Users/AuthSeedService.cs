using System.Security.Claims;
using Backup.Web.Api.Server.Models.AppSettings;
using Backup.Web.Api.Server.Models.Rols;
using Backup.Web.Api.Server.Models.Security;
using Backup.Web.Api.Server.Models.Users;
using Backup.Web.Api.Server.Services.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Backup.Web.Api.Server.Services.Users;

public static class AuthSeedService
{
    /// <summary>
    /// Permissions métier de base pour le rôle User (ajoutées si absentes, sans retirer les existantes).
    /// </summary>
    private static readonly string[] DefaultUserPermissions =
    {
        Permissions.ProductRead, Permissions.ProductCreate, Permissions.ProductUpdate,
        Permissions.ErpChangeRead, Permissions.ErpChangeUpdate, Permissions.ErpChangeDelete,
        Permissions.SupplierRead, Permissions.SupplierCreate, Permissions.SupplierUpdate,
        Permissions.CustomerRead, Permissions.CustomerCreate, Permissions.CustomerUpdate,
        Permissions.QuoteRead, Permissions.QuoteCreate, Permissions.QuoteUpdate,
        Permissions.OrderRead, Permissions.OrderCreate, Permissions.OrderUpdate,
        Permissions.DeliveryNoteRead,
        Permissions.InvoiceRead, Permissions.InvoiceCreate, Permissions.InvoiceUpdate,
        Permissions.PurchaseOrderRead, Permissions.PurchaseOrderCreate, Permissions.PurchaseOrderUpdate,
        Permissions.ReceiptRead, Permissions.ReceiptCreate,
        Permissions.SupplierInvoiceRead, Permissions.SupplierInvoiceCreate,
        Permissions.StockRead, Permissions.StockUpdate,
        Permissions.CashRead, Permissions.CashManage,
        Permissions.NumberingManage,
        Permissions.DocumentRead,
    };

    public static async Task SeedAsync(IServiceProvider services, ILogger logger)
    {
        var options = services.GetRequiredService<IOptions<AuthSeedOptions>>().Value;
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

        // Toujours synchroniser les permissions métier manquantes du rôle User
        // (Cash, Numbering, Document, ErpChange, …) — indépendamment du seed admin.
        await EnsureUserRolePermissionsAsync(roleManager, logger);
        await EnsureAdminRolePermissionsAsync(roleManager, logger);

        if (!options.Enabled || string.IsNullOrWhiteSpace(options.Email) || string.IsNullOrWhiteSpace(options.Password))
            return;

        var userStore = services.GetRequiredService<IUserStore<User>>();
        var passwordStore = userStore as IUserPasswordStore<User>
            ?? throw new InvalidOperationException("IUserPasswordStore<User> is required for auth seed");

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

            try
            {
                await PersistUserViaStoreAsync(userStore, passwordStore, userManager, user, options.Password, create: true);
                existing = user;
                logger.LogInformation("Auth seed: admin user created ({Email})", email);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Auth seed: failed to create admin {Email}", email);
                return;
            }
        }
        else
        {
            var hash = existing.PasswordHash;
            var needsPasswordRepair = options.ForceResetPassword
                || string.IsNullOrWhiteSpace(hash)
                || hash.StartsWith("$2", StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(existing.UserName);

            var needsNormalize = string.IsNullOrWhiteSpace(existing.UserName)
                || !string.Equals(existing.UserName, email, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(existing.Email, email, StringComparison.OrdinalIgnoreCase);

            if (needsPasswordRepair || needsNormalize)
            {
                existing.Email = email;
                existing.EmailConfirmed = true;
                existing.Status = UserStatus.Activated;
                existing.UpdatedDate = DateTimeOffset.UtcNow;

                try
                {
                    await PersistUserViaStoreAsync(
                        userStore,
                        passwordStore,
                        userManager,
                        existing,
                        needsPasswordRepair ? options.Password : null,
                        create: false);

                    logger.LogInformation(
                        "Auth seed: user normalized{PasswordPart} ({Email})",
                        needsPasswordRepair ? " + password repaired" : "",
                        email);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Auth seed: failed to repair admin {Email}", email);
                    return;
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

    /// <summary>Ajoute au rôle User les permissions métier manquantes (Cash, Document, ErpChange, etc.).</summary>
    private static async Task EnsureUserRolePermissionsAsync(RoleManager<Role> roleManager, ILogger logger)
    {
        var userRole = await roleManager.FindByNameAsync("User");
        if (userRole == null) return;

        var existing = await roleManager.GetClaimsAsync(userRole);
        var have = existing
            .Where(c => c.Type == PermissionResolver.PermissionClaimType)
            .Select(c => c.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var added = 0;
        foreach (var perm in DefaultUserPermissions)
        {
            if (have.Contains(perm)) continue;
            var result = await roleManager.AddClaimAsync(
                userRole,
                new Claim(PermissionResolver.PermissionClaimType, perm));
            if (result.Succeeded)
            {
                have.Add(perm);
                added++;
            }
            else
            {
                logger.LogWarning(
                    "Auth seed: failed to add {Permission} to User: {Errors}",
                    perm,
                    string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }

        if (added > 0)
            logger.LogInformation("Auth seed: added {Count} missing permission(s) to role User", added);
    }

    /// <summary>Le rôle Admin possède toujours toutes les permissions du catalogue.</summary>
    private static async Task EnsureAdminRolePermissionsAsync(RoleManager<Role> roleManager, ILogger logger)
    {
        var adminRole = await roleManager.FindByNameAsync("Admin");
        if (adminRole == null) return;

        var existing = await roleManager.GetClaimsAsync(adminRole);
        var have = existing
            .Where(c => c.Type == PermissionResolver.PermissionClaimType)
            .Select(c => c.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var added = 0;
        foreach (var perm in Permissions.All)
        {
            if (have.Contains(perm)) continue;
            var result = await roleManager.AddClaimAsync(
                adminRole,
                new Claim(PermissionResolver.PermissionClaimType, perm));
            if (result.Succeeded)
            {
                have.Add(perm);
                added++;
            }
            else
            {
                logger.LogWarning(
                    "Auth seed: failed to add {Permission} to Admin: {Errors}",
                    perm,
                    string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }

        if (added > 0)
            logger.LogInformation("Auth seed: added {Count} missing permission(s) to role Admin (full access)", added);
    }

    /// <summary>
    /// Writes username/email/password via the EF store, bypassing UserManager validators
    /// (required for legacy AspNetUsers rows with empty UserName).
    /// </summary>
    private static async Task PersistUserViaStoreAsync(
        IUserStore<User> userStore,
        IUserPasswordStore<User> passwordStore,
        UserManager<User> userManager,
        User user,
        string? newPassword,
        bool create)
    {
        var email = user.Email!;
        await userStore.SetUserNameAsync(user, email, CancellationToken.None);
        await userStore.SetNormalizedUserNameAsync(user, userManager.NormalizeName(email), CancellationToken.None);

        if (userStore is IUserEmailStore<User> emailStore)
        {
            await emailStore.SetEmailAsync(user, email, CancellationToken.None);
            await emailStore.SetEmailConfirmedAsync(user, true, CancellationToken.None);
            await emailStore.SetNormalizedEmailAsync(user, userManager.NormalizeEmail(email), CancellationToken.None);
        }

        if (!string.IsNullOrEmpty(newPassword))
        {
            var hash = userManager.PasswordHasher.HashPassword(user, newPassword);
            await passwordStore.SetPasswordHashAsync(user, hash, CancellationToken.None);
            user.SecurityStamp = Guid.NewGuid().ToString();
        }
        else if (string.IsNullOrEmpty(user.SecurityStamp))
        {
            user.SecurityStamp = Guid.NewGuid().ToString();
        }

        IdentityResult result = create
            ? await userStore.CreateAsync(user, CancellationToken.None)
            : await userStore.UpdateAsync(user, CancellationToken.None);

        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                "Auth seed store persist failed: " + string.Join(", ", result.Errors.Select(e => e.Description)));
        }
    }
}
