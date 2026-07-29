using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Models.Rols;
using Backup.Web.Api.Server.Models.Security;
using Backup.Web.Api.Server.Models.Users;
using Backup.Web.Api.Server.Services.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Backup.Web.Api.Server.Authorization;

public class PermissionRequirement : IAuthorizationRequirement
{
    public PermissionRequirement(params string[] permissions) =>
        Permissions = permissions?.Where(p => !string.IsNullOrWhiteSpace(p)).ToArray()
                      ?? Array.Empty<string>();

    public IReadOnlyList<string> Permissions { get; }
}

/// <summary>
/// Vérifie les permissions depuis les rôles en base (pas seulement le JWT),
/// pour appliquer immédiatement les changements admin sans reconnexion.
/// </summary>
public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly UserManager<User> _userManager;
    private readonly RoleManager<Role> _roleManager;

    public PermissionAuthorizationHandler(UserManager<User> userManager, RoleManager<Role> roleManager)
    {
        _userManager = userManager;
        _roleManager = roleManager;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (context.User.IsInRole("Admin"))
        {
            context.Succeed(requirement);
            return;
        }

        var userId = context.User.FindFirstValue("id")
            ?? context.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? context.User.FindFirstValue("sub");

        if (string.IsNullOrWhiteSpace(userId))
            return;

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return;

        var permissions = await PermissionResolver.GetUserPermissionsAsync(_userManager, _roleManager, user);
        var hasAny = requirement.Permissions.Any(required =>
            permissions.Any(p => string.Equals(p, required, StringComparison.OrdinalIgnoreCase)));

        if (hasAny)
            context.Succeed(requirement);
    }
}

/// <summary>Exige une permission (une seule).</summary>
public class RequirePermissionAttribute : Microsoft.AspNetCore.Authorization.AuthorizeAttribute
{
    public RequirePermissionAttribute(string permission)
    {
        this.Policy = $"Permission:{permission}";
    }
}

/// <summary>Exige au moins une des permissions listées (OU).</summary>
public class RequireAnyPermissionAttribute : Microsoft.AspNetCore.Authorization.AuthorizeAttribute
{
    public RequireAnyPermissionAttribute(params string[] permissions)
    {
        this.Policy = $"PermissionAny:{string.Join("|", permissions)}";
    }
}

/// <summary>
/// Résout Permission:X et PermissionAny:A|B dynamiquement
/// (évite d'enregistrer toutes les combinaisons OR à l'avance).
/// </summary>
public class PermissionPolicyProvider : IAuthorizationPolicyProvider
{
    private const string Prefix = "Permission:";
    private const string AnyPrefix = "PermissionAny:";
    private readonly DefaultAuthorizationPolicyProvider _fallback;

    public PermissionPolicyProvider(IOptions<AuthorizationOptions> options) =>
        _fallback = new DefaultAuthorizationPolicyProvider(options);

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => _fallback.GetDefaultPolicyAsync();

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => _fallback.GetFallbackPolicyAsync();

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (policyName.StartsWith(AnyPrefix, StringComparison.Ordinal))
        {
            var parts = policyName[AnyPrefix.Length..]
                .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var policy = new AuthorizationPolicyBuilder()
                .AddRequirements(new PermissionRequirement(parts))
                .Build();
            return Task.FromResult<AuthorizationPolicy?>(policy);
        }

        if (policyName.StartsWith(Prefix, StringComparison.Ordinal))
        {
            var permission = policyName[Prefix.Length..];
            var policy = new AuthorizationPolicyBuilder()
                .AddRequirements(new PermissionRequirement(permission))
                .Build();
            return Task.FromResult<AuthorizationPolicy?>(policy);
        }

        return _fallback.GetPolicyAsync(policyName);
    }
}

public static class PermissionPolicyRegistration
{
    public static void RegisterPermissionPolicies(AuthorizationOptions options)
    {
        foreach (var permission in Permissions.All)
        {
            options.AddPolicy($"Permission:{permission}",
                policy => policy.Requirements.Add(new PermissionRequirement(permission)));
        }
    }
}
