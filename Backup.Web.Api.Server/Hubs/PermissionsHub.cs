using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Backup.Web.Api.Server.Hubs;

/// <summary>Notifie les clients quand les permissions d'un rôle changent.</summary>
[Authorize]
public class PermissionsHub : Hub
{
    public const string HubPath = "/hubs/permissions";
    public const string PermissionsChangedEvent = "permissionsChanged";

    public static string RoleGroup(string roleName) => $"role:{roleName}";

    public override async Task OnConnectedAsync()
    {
        var roles = Context.User?.FindAll(ClaimTypes.Role).Select(c => c.Value)
            ?? Enumerable.Empty<string>();
        foreach (var role in roles.Where(r => !string.IsNullOrWhiteSpace(r)).Distinct())
            await Groups.AddToGroupAsync(Context.ConnectionId, RoleGroup(role));

        await base.OnConnectedAsync();
    }
}
