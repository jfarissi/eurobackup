using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Models.Rols;
using Backup.Web.Api.Server.Models.Security;
using Backup.Web.Api.Server.Models.Users;
using Microsoft.AspNetCore.Identity;

namespace Backup.Web.Api.Server.Services.Security;

public static class PermissionResolver
{
    public const string PermissionClaimType = "Permission";

    public static async Task<List<string>> GetUserPermissionsAsync(
        UserManager<User> userManager,
        RoleManager<Role> roleManager,
        User user)
    {
        var roles = await userManager.GetRolesAsync(user);
        if (roles.Any(r => string.Equals(r, "Admin", StringComparison.OrdinalIgnoreCase)))
            return Permissions.All.ToList();

        var perms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var roleName in roles)
        {
            var role = await roleManager.FindByNameAsync(roleName);
            if (role == null) continue;
            var claims = await roleManager.GetClaimsAsync(role);
            foreach (var claim in claims.Where(c => c.Type == PermissionClaimType))
                perms.Add(claim.Value);
        }

        return perms.OrderBy(p => p).ToList();
    }

    public static IEnumerable<Claim> ToPermissionClaims(IEnumerable<string> permissions) =>
        permissions.Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(p => new Claim(PermissionClaimType, p));
}
