using System.Security.Claims;
using Backup.Web.Api.Server.Authorization;
using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Models.Rols;
using Backup.Web.Api.Server.Models.Users;
using Backup.Web.Api.Server.Services.Security;
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
    private readonly RoleManager<Role> _roleManager;
    private readonly IJwtUtils _jwtUtils;
    private readonly IStorageBroker _storage;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        UserManager<User> userManager,
        RoleManager<Role> roleManager,
        IJwtUtils jwtUtils,
        IStorageBroker storage,
        ILogger<AuthController> logger)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _jwtUtils = jwtUtils;
        _storage = storage;
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
                _logger.LogWarning("Login failed: bad password for {Login}", login);
                return Unauthorized(new { message = "Username or password is incorrect" });
            }

            var roles = await _userManager.GetRolesAsync(user);
            var roleName = roles.FirstOrDefault() ?? "User";
            user.IsAdmin = roles.Contains("Admin")
                || string.Equals(roleName, "Admin", StringComparison.OrdinalIgnoreCase);
            user.Role = new Role { Name = roleName };

            var companies = await LoadUserCompaniesAsync(user.Id);
            var companyId = user.CompanyId;
            if (string.IsNullOrWhiteSpace(companyId) && companies.Count > 0)
            {
                companyId = companies[0].Id;
                user.CompanyId = companyId;
                await _userManager.UpdateAsync(user);
            }

            var companyName = companies.FirstOrDefault(c => c.Id == companyId)?.Name;
            var permissions = await PermissionResolver.GetUserPermissionsAsync(_userManager, _roleManager, user);
            var token = _jwtUtils.GenerateJwtToken(user, companyId, permissions);
            return Ok(new AuthenticateResponse(user, token, companyId, companyName, companies, permissions));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Login failed with exception for {Login}", login);
            return Unauthorized(new { message = "Username or password is incorrect", detail = ex.Message });
        }
    }

    public class SwitchCompanyRequest
    {
        public string CompanyId { get; set; } = string.Empty;
    }

    [MsAuthorize]
    [HttpPost("switch-company")]
    public async Task<IActionResult> SwitchCompany([FromBody] SwitchCompanyRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CompanyId))
            return BadRequest(new { message = "CompanyId required" });

        var idValue = User.FindFirstValue("id") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(idValue, out var userId))
            return Unauthorized();

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null) return Unauthorized();

        var hasAccess = await _storage.UserHasCompanyAccessAsync(userId, request.CompanyId);
        if (!hasAccess)
            return Forbid();

        user.CompanyId = request.CompanyId;
        await _userManager.UpdateAsync(user);

        var companies = await LoadUserCompaniesAsync(userId);
        var companyName = companies.FirstOrDefault(c => c.Id == request.CompanyId)?.Name;
        var roles = await _userManager.GetRolesAsync(user);
        user.IsAdmin = roles.Contains("Admin");
        user.Role = new Role { Name = roles.FirstOrDefault() ?? "User" };

        var permissions = await PermissionResolver.GetUserPermissionsAsync(_userManager, _roleManager, user);
        var token = _jwtUtils.GenerateJwtToken(user, request.CompanyId, permissions);
        return Ok(new AuthenticateResponse(user, token, request.CompanyId, companyName, companies, permissions));
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
        var companies = await LoadUserCompaniesAsync(userId);
        var companyId = User.FindFirstValue("CompanyId") ?? user.CompanyId;
        var companyName = companies.FirstOrDefault(c => c.Id == companyId)?.Name;
        user.Role = new Role { Name = roles.FirstOrDefault() ?? "User" };
        var permissions = await PermissionResolver.GetUserPermissionsAsync(_userManager, _roleManager, user);
        var token = _jwtUtils.GenerateJwtToken(user, companyId, permissions);

        return Ok(new AuthenticateResponse(user, token, companyId, companyName, companies, permissions)
        {
            Role = roles.FirstOrDefault() ?? "User",
            IsAdmin = roles.Contains("Admin")
        });
    }

    private async Task<List<CompanySummary>> LoadUserCompaniesAsync(Guid userId)
    {
        return await _storage.SelectUserCompaniesByUserId(userId)
            .Where(uc => uc.Company != null && uc.Company.IsActive)
            .OrderBy(uc => uc.Company!.Name)
            .Select(uc => new CompanySummary
            {
                Id = uc.CompanyId,
                Name = uc.Company!.Name,
                EnableErpCatalogSync = uc.Company.EnableErpCatalogSync
            })
            .ToListAsync();
    }
}
