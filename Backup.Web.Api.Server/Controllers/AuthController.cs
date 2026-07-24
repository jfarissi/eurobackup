using System.Security.Claims;
using Backup.Web.Api.Server.Authorization;
using Backup.Web.Api.Server.Models.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MsAuthorize = Microsoft.AspNetCore.Authorization.AuthorizeAttribute;
using MsAllowAnonymous = Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute;

namespace Backup.Web.Api.Server.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly UserManager<User> _userManager;
    private readonly IJwtUtils _jwtUtils;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        UserManager<User> userManager,
        IJwtUtils jwtUtils,
        ILogger<AuthController> logger)
    {
        _userManager = userManager;
        _jwtUtils = jwtUtils;
        _logger = logger;
    }

    [MsAllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] AuthenticateRequest? request)
    {
        if (request == null
            || string.IsNullOrWhiteSpace(request.Username)
            || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { message = "Username and password are required" });
        }

        var login = request.Username.Trim();
        try
        {
            var user = await _userManager.FindByEmailAsync(login)
                ?? await _userManager.FindByNameAsync(login);

            if (user == null)
            {
                user = await _userManager.Users.FirstOrDefaultAsync(u =>
                    (u.Email != null && u.Email.ToLower() == login.ToLower())
                    || (u.UserName != null && u.UserName.ToLower() == login.ToLower()));
            }

            if (user == null)
            {
                _logger.LogWarning("Login failed: user not found ({Login})", login);
                return Unauthorized(new { message = "Username or password is incorrect" });
            }

            var passwordOk = await _userManager.CheckPasswordAsync(user, request.Password);
            if (!passwordOk)
            {
                _logger.LogWarning(
                    "Login failed: bad password for {Login} (hashPrefix={HashPrefix})",
                    login,
                    string.IsNullOrEmpty(user.PasswordHash) ? "(empty)" : user.PasswordHash[..Math.Min(4, user.PasswordHash.Length)]);
                return Unauthorized(new { message = "Username or password is incorrect" });
            }

            var roles = await _userManager.GetRolesAsync(user);
            var roleName = roles.FirstOrDefault() ?? "User";
            user.IsAdmin = roles.Contains("Admin")
                || string.Equals(roleName, "Admin", StringComparison.OrdinalIgnoreCase);
            user.Role = null;

            var token = _jwtUtils.GenerateJwtToken(user);
            return Ok(new AuthenticateResponse(user, token));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Login failed with exception for {Login}", login);
            return Unauthorized(new { message = "Username or password is incorrect", detail = ex.Message });
        }
    }

    [MsAuthorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var idValue = User.FindFirstValue("id")
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");

        if (string.IsNullOrWhiteSpace(idValue) || !Guid.TryParse(idValue, out var userId))
            return Unauthorized(new { message = "Invalid token" });

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
            return Unauthorized(new { message = "User not found" });

        var roles = await _userManager.GetRolesAsync(user);
        return Ok(new
        {
            id = user.Id,
            firstName = user.Name,
            lastName = user.FamilyName,
            username = user.Email ?? user.UserName,
            role = roles.FirstOrDefault() ?? "User",
            isAdmin = roles.Contains("Admin")
        });
    }
}
