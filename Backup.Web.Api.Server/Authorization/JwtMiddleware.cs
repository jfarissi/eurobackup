namespace Backup.Web.Api.Server.Authorization;

using Microsoft.Extensions.Options;
using Backup.Web.Api.Server.Models.AppSettings;
using Backup.Web.Api.Server.Services.Users;

public class JwtMiddleware
{
    private readonly RequestDelegate _next;
    private readonly AppSettings _appSettings;

    public JwtMiddleware(RequestDelegate next, IOptions<AppSettings> appSettings)
    {
        _next = next;
        _appSettings = appSettings.Value;
    }

    public async Task Invoke(HttpContext context, IUserService userService, IJwtUtils jwtUtils)
    {
        var token = context.Request.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last();
        var userId = jwtUtils.ValidateJwtToken(token);
        if (userId != null)
        {
            // attach user to context on successful jwt validation
            context.Items["User"] = userService.RetrieveUserByIdAsync(userId.Value);
        }

        await _next(context);
    }
}