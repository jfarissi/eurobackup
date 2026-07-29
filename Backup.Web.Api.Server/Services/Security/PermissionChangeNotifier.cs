using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace Backup.Web.Api.Server.Services.Security;

public interface IPermissionChangeNotifier
{
    Task NotifyRolePermissionsChangedAsync(string roleName);
    Task NotifyUserPermissionsChangedAsync(string userId);
}

public class PermissionChangeNotifier : IPermissionChangeNotifier
{
    private readonly IHubContext<PermissionsHub> _hub;
    private readonly ILogger<PermissionChangeNotifier> _logger;

    public PermissionChangeNotifier(
        IHubContext<PermissionsHub> hub,
        ILogger<PermissionChangeNotifier> logger)
    {
        _hub = hub;
        _logger = logger;
    }

    public async Task NotifyRolePermissionsChangedAsync(string roleName)
    {
        if (string.IsNullOrWhiteSpace(roleName)) return;
        try
        {
            await _hub.Clients
                .Group(PermissionsHub.RoleGroup(roleName))
                .SendAsync(PermissionsHub.PermissionsChangedEvent, new
                {
                    reason = "roleUpdated",
                    role = roleName,
                    at = DateTimeOffset.UtcNow
                });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SignalR: failed to notify role {Role}", roleName);
        }
    }

    public async Task NotifyUserPermissionsChangedAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId)) return;
        try
        {
            await _hub.Clients
                .User(userId)
                .SendAsync(PermissionsHub.PermissionsChangedEvent, new
                {
                    reason = "userUpdated",
                    userId,
                    at = DateTimeOffset.UtcNow
                });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SignalR: failed to notify user {UserId}", userId);
        }
    }
}

/// <summary>Mappe le claim JWT "id" vers SignalR Clients.User(...).</summary>
public class JwtUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection) =>
        connection.User?.FindFirst("id")?.Value
        ?? connection.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
}
