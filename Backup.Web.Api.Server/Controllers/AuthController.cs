using System.Security.Claims;
using Backup.Web.Api.Server.Models.Users;
using Backup.Web.Api.Server.Services.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Backup.Web.Api.Server.Models;

namespace Backup.Web.Api.Server.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly UserManager<User> _userManager;

    public AuthController(IUserService userService, UserManager<User> userManager)
    {
        _userService = userService;
        _userManager = userManager;
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] AuthenticateRequest request)
    {
        if (request == null
            || string.IsNullOrWhiteSpace(request.Username)
            || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { message = "Username and password are required" });
        }

        try
        {
            var response = await _userService.Authenticate(request);
            return Ok(response);
        }
        catch (AppException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception)
        {
            return Unauthorized(new { message = "Username or password is incorrect" });
        }
    }

    [Authorize]
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
