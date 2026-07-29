using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Authorization;
using Authorize = Microsoft.AspNetCore.Authorization.AuthorizeAttribute;
using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Models.Entities.SaaS;
using Backup.Web.Api.Server.Models.Security;
using Backup.Web.Api.Server.Models.Users;
using Backup.Web.Api.Server.Services.Security;
using Backup.Web.Api.Server.Services.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RESTFulSense.Controllers;

namespace Backup.Web.Api.Server.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/admin")]
    public class AdminController : RESTFulController
    {
        private readonly IStorageBroker storage;
        private readonly UserManager<User> userManager;
        private readonly IPermissionChangeNotifier permissionNotifier;

        public AdminController(
            IStorageBroker storage,
            UserManager<User> userManager,
            IPermissionChangeNotifier permissionNotifier)
        {
            this.storage = storage;
            this.userManager = userManager;
            this.permissionNotifier = permissionNotifier;
        }

        // ── Tenants ──────────────────────────────────────────────────────────

        [HttpGet("tenants")]
        [RequirePermission(Permissions.UserRead)]
        public IActionResult GetTenants()
        {
            var tenants = this.storage.SelectAllTenants()
                .Include(t => t.Companies)
                .OrderBy(t => t.Name)
                .Select(t => new TenantDto
                {
                    Id = t.Id,
                    Name = t.Name,
                    IsActive = t.IsActive,
                    CreatedAt = t.CreatedAt,
                    CompanyCount = t.Companies.Count
                })
                .ToList();
            return Ok(tenants);
        }

        [HttpPost("tenants")]
        [RequirePermission(Permissions.UserUpdate)]
        public async Task<IActionResult> CreateTenant([FromBody] SaveTenantRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Name)) return BadRequest("Nom requis.");
            var tenant = new Tenant
            {
                Id = Guid.NewGuid().ToString(),
                Name = req.Name.Trim(),
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            var created = await this.storage.InsertTenantAsync(tenant);
            return Ok(new TenantDto { Id = created.Id, Name = created.Name, IsActive = created.IsActive, CreatedAt = created.CreatedAt });
        }

        [HttpPut("tenants/{id}")]
        [RequirePermission(Permissions.UserUpdate)]
        public async Task<IActionResult> UpdateTenant(string id, [FromBody] SaveTenantRequest req)
        {
            var tenant = await this.storage.SelectAllTenants().FirstOrDefaultAsync(t => t.Id == id);
            if (tenant == null) return NotFound();
            if (!string.IsNullOrWhiteSpace(req.Name)) tenant.Name = req.Name.Trim();
            tenant.IsActive = req.IsActive;
            var updated = await this.storage.UpdateTenantAsync(tenant);
            return Ok(new TenantDto { Id = updated.Id, Name = updated.Name, IsActive = updated.IsActive, CreatedAt = updated.CreatedAt });
        }

        // ── Companies ─────────────────────────────────────────────────────────

        [HttpGet("companies")]
        [RequirePermission(Permissions.UserRead)]
        public IActionResult GetCompanies([FromQuery] string? tenantId = null)
        {
            var query = this.storage.SelectAllCompanies().AsQueryable();
            if (!string.IsNullOrWhiteSpace(tenantId))
                query = query.Where(c => c.TenantId == tenantId);
            var list = query.OrderBy(c => c.Name)
                .Select(c => new CompanyAdminDto
                {
                    Id = c.Id,
                    TenantId = c.TenantId,
                    TenantName = c.Tenant != null ? c.Tenant.Name : null,
                    Name = c.Name,
                    IsActive = c.IsActive,
                    DefaultLanguageCode = c.DefaultLanguageCode,
                    DefaultCurrencyCode = c.DefaultCurrencyCode,
                    CreatedAt = c.CreatedAt
                })
                .ToList();
            return Ok(list);
        }

        [HttpPost("companies")]
        [RequirePermission(Permissions.UserUpdate)]
        public async Task<IActionResult> CreateCompany([FromBody] SaveCompanyRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Name)) return BadRequest("Nom requis.");
            if (string.IsNullOrWhiteSpace(req.TenantId)) return BadRequest("TenantId requis.");
            var company = new Company
            {
                Id = Guid.NewGuid().ToString(),
                TenantId = req.TenantId,
                Name = req.Name.Trim(),
                IsActive = true,
                DefaultLanguageCode = req.DefaultLanguageCode ?? "fr-BE",
                DefaultCurrencyCode = req.DefaultCurrencyCode ?? "EUR",
                CreatedAt = DateTime.UtcNow
            };
            var created = await this.storage.InsertCompanyAsync(company);
            return Ok(ToCompanyDto(created));
        }

        [HttpPut("companies/{id}")]
        [RequirePermission(Permissions.UserUpdate)]
        public async Task<IActionResult> UpdateCompany(string id, [FromBody] SaveCompanyRequest req)
        {
            var company = await this.storage.SelectCompanyByIdAsync(id);
            if (company == null) return NotFound();
            if (!string.IsNullOrWhiteSpace(req.Name)) company.Name = req.Name.Trim();
            if (!string.IsNullOrWhiteSpace(req.TenantId)) company.TenantId = req.TenantId;
            if (!string.IsNullOrWhiteSpace(req.DefaultLanguageCode)) company.DefaultLanguageCode = req.DefaultLanguageCode;
            if (!string.IsNullOrWhiteSpace(req.DefaultCurrencyCode)) company.DefaultCurrencyCode = req.DefaultCurrencyCode;
            company.IsActive = req.IsActive;
            var updated = await this.storage.UpdateCompanyAsync(company);
            return Ok(ToCompanyDto(updated));
        }

        // ── Users ─────────────────────────────────────────────────────────────

        [HttpGet("users")]
        [RequirePermission(Permissions.UserRead)]
        public async Task<IActionResult> GetUsers()
        {
            var users = await this.userManager.Users.ToListAsync();
            var result = new List<UserAdminDto>();
            foreach (var u in users)
            {
                var roles = await this.userManager.GetRolesAsync(u);
                result.Add(new UserAdminDto
                {
                    Id = u.Id.ToString(),
                    Username = u.UserName ?? u.Email ?? u.Id.ToString(),
                    Email = u.Email,
                    FirstName = u.Name,
                    LastName = u.FamilyName,
                    CompanyId = u.CompanyId,
                    IsAdmin = roles.Contains("Admin"),
                    Roles = roles.ToList(),
                    CreatedAt = u.CreatedDate.UtcDateTime
                });
            }
            return Ok(result);
        }

        [HttpPost("users")]
        [RequirePermission(Permissions.UserCreate)]
        public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Username)) return BadRequest(new { message = "Username requis." });
            if (string.IsNullOrWhiteSpace(req.Password)) return BadRequest(new { message = "Mot de passe requis." });

            var user = new User
            {
                UserName = req.Username.Trim(),
                Email = req.Email?.Trim(),
                Name = req.FirstName,
                FamilyName = req.LastName,
                CompanyId = req.CompanyId,
                CreatedDate = DateTimeOffset.UtcNow,
                UpdatedDate = DateTimeOffset.UtcNow,
                EmailConfirmed = true
            };

            var result = await this.userManager.CreateAsync(user, req.Password);
            if (!result.Succeeded)
                return BadRequest(new { message = string.Join("; ", result.Errors.Select(e => e.Description)) });

            if (req.IsAdmin)
            {
                await this.userManager.AddToRoleAsync(user, "Admin");
            }
            else if (!string.IsNullOrWhiteSpace(req.RoleName))
            {
                await this.userManager.AddToRoleAsync(user, req.RoleName.Trim());
            }
            else
            {
                await this.userManager.AddToRoleAsync(user, "User");
            }

            // Auto-assign to company
            if (!string.IsNullOrWhiteSpace(req.CompanyId))
            {
                var hasLink = await this.storage.UserHasCompanyAccessAsync(user.Id, req.CompanyId);
                if (!hasLink)
                    await this.storage.InsertUserCompanyAsync(new UserCompany { UserId = user.Id, CompanyId = req.CompanyId });
            }

            return Ok(new UserAdminDto
            {
                Id = user.Id.ToString(),
                Username = user.UserName,
                Email = user.Email,
                FirstName = user.Name,
                LastName = user.FamilyName,
                CompanyId = user.CompanyId,
                IsAdmin = req.IsAdmin
            });
        }

        [HttpPut("users/{userId}")]
        [RequirePermission(Permissions.UserUpdate)]
        public async Task<IActionResult> UpdateUser(string userId, [FromBody] UpdateUserRequest req)
        {
            var user = await this.userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();

            if (!string.IsNullOrWhiteSpace(req.Username)) user.UserName = req.Username.Trim();
            if (!string.IsNullOrWhiteSpace(req.Email)) { user.Email = req.Email.Trim(); user.NormalizedEmail = req.Email.Trim().ToUpperInvariant(); }
            if (req.FirstName != null) user.Name = req.FirstName;
            if (req.LastName != null) user.FamilyName = req.LastName;
            if (!string.IsNullOrWhiteSpace(req.CompanyId)) user.CompanyId = req.CompanyId;
            user.UpdatedDate = DateTimeOffset.UtcNow;

            var updateResult = await this.userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
                return BadRequest(new { message = string.Join("; ", updateResult.Errors.Select(e => e.Description)) });

            // Password change
            if (!string.IsNullOrWhiteSpace(req.NewPassword))
            {
                var token = await this.userManager.GeneratePasswordResetTokenAsync(user);
                var pwResult = await this.userManager.ResetPasswordAsync(user, token, req.NewPassword);
                if (!pwResult.Succeeded)
                    return BadRequest(new { message = string.Join("; ", pwResult.Errors.Select(e => e.Description)) });
            }

            // Role sync
            var roles = await this.userManager.GetRolesAsync(user);
            await this.userManager.RemoveFromRolesAsync(user, roles);
            if (req.IsAdmin)
                await this.userManager.AddToRoleAsync(user, "Admin");
            else if (!string.IsNullOrWhiteSpace(req.RoleName))
                await this.userManager.AddToRoleAsync(user, req.RoleName.Trim());
            else
                await this.userManager.AddToRoleAsync(user, "User");

            await this.permissionNotifier.NotifyUserPermissionsChangedAsync(user.Id.ToString());

            return Ok(new { updated = true });
        }

        [HttpDelete("users/{userId}")]
        [RequirePermission(Permissions.UserDelete)]
        public async Task<IActionResult> DeleteUser(string userId)
        {
            var user = await this.userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();
            var result = await this.userManager.DeleteAsync(user);
            if (!result.Succeeded)
                return BadRequest(new { message = string.Join("; ", result.Errors.Select(e => e.Description)) });
            return NoContent();
        }

        [HttpPost("users/{userId}/reset-password")]
        [RequirePermission(Permissions.UserUpdate)]
        public async Task<IActionResult> ResetPassword(string userId, [FromBody] ResetPasswordRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.NewPassword)) return BadRequest(new { message = "Mot de passe requis." });
            var user = await this.userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();
            var token = await this.userManager.GeneratePasswordResetTokenAsync(user);
            var result = await this.userManager.ResetPasswordAsync(user, token, req.NewPassword);
            if (!result.Succeeded)
                return BadRequest(new { message = string.Join("; ", result.Errors.Select(e => e.Description)) });
            return Ok(new { reset = true });
        }

        [HttpPost("users/{userId}/assign-company/{companyId}")]
        [RequirePermission(Permissions.UserUpdate)]
        public async Task<IActionResult> AssignUserToCompany(string userId, string companyId)
        {
            if (!Guid.TryParse(userId, out var uid)) return BadRequest("userId invalide.");
            var company = await this.storage.SelectCompanyByIdAsync(companyId);
            if (company == null) return NotFound("Société introuvable.");

            var already = await this.storage.UserHasCompanyAccessAsync(uid, companyId);
            if (!already)
                await this.storage.InsertUserCompanyAsync(new UserCompany { UserId = uid, CompanyId = companyId });

            var user = await this.userManager.FindByIdAsync(userId);
            if (user != null && string.IsNullOrWhiteSpace(user.CompanyId))
            {
                user.CompanyId = companyId;
                await this.userManager.UpdateAsync(user);
            }
            return Ok(new { assigned = true });
        }

        [HttpDelete("users/{userId}/companies/{companyId}")]
        [RequirePermission(Permissions.UserUpdate)]
        public async Task<IActionResult> RemoveUserFromCompany(string userId, string companyId)
        {
            if (!Guid.TryParse(userId, out var uid)) return BadRequest("userId invalide.");
            var link = await this.storage.SelectUserCompaniesByUserId(uid)
                .FirstOrDefaultAsync(uc => uc.CompanyId == companyId);
            if (link == null) return NotFound();
            await this.storage.DeleteUserCompanyAsync(link);
            return NoContent();
        }

        [HttpGet("users/{userId}/companies")]
        [RequirePermission(Permissions.UserRead)]
        public IActionResult GetUserCompanies(string userId)
        {
            if (!Guid.TryParse(userId, out var uid)) return BadRequest();
            var companies = this.storage.SelectUserCompaniesByUserId(uid)
                .Select(uc => new { uc.CompanyId, Name = uc.Company != null ? uc.Company.Name : uc.CompanyId })
                .ToList();
            return Ok(companies);
        }

        // ── DTOs ──────────────────────────────────────────────────────────────

        private static CompanyAdminDto ToCompanyDto(Company c) => new()
        {
            Id = c.Id, TenantId = c.TenantId, TenantName = c.Tenant?.Name,
            Name = c.Name, IsActive = c.IsActive,
            DefaultLanguageCode = c.DefaultLanguageCode, DefaultCurrencyCode = c.DefaultCurrencyCode,
            CreatedAt = c.CreatedAt
        };

        public class TenantDto { public string Id { get; set; } = ""; public string Name { get; set; } = ""; public bool IsActive { get; set; } public DateTime CreatedAt { get; set; } public int CompanyCount { get; set; } }
        public class SaveTenantRequest { public string Name { get; set; } = ""; public bool IsActive { get; set; } = true; }
        public class CompanyAdminDto { public string Id { get; set; } = ""; public string TenantId { get; set; } = ""; public string? TenantName { get; set; } public string Name { get; set; } = ""; public bool IsActive { get; set; } public string DefaultLanguageCode { get; set; } = "fr-BE"; public string DefaultCurrencyCode { get; set; } = "EUR"; public DateTime CreatedAt { get; set; } }
        public class SaveCompanyRequest { public string Name { get; set; } = ""; public string TenantId { get; set; } = ""; public bool IsActive { get; set; } = true; public string? DefaultLanguageCode { get; set; } public string? DefaultCurrencyCode { get; set; } }
        public class UserAdminDto
        {
            public string Id { get; set; } = "";
            public string Username { get; set; } = "";
            public string? Email { get; set; }
            public string? FirstName { get; set; }
            public string? LastName { get; set; }
            public string? CompanyId { get; set; }
            public bool IsAdmin { get; set; }
            public List<string> Roles { get; set; } = new();
            public DateTime? CreatedAt { get; set; }
        }
        public class CreateUserRequest
        {
            public string Username { get; set; } = "";
            public string? Email { get; set; }
            public string? FirstName { get; set; }
            public string? LastName { get; set; }
            public string Password { get; set; } = "";
            public string? CompanyId { get; set; }
            public bool IsAdmin { get; set; }
            public string? RoleName { get; set; }
        }
        public class UpdateUserRequest
        {
            public string? Username { get; set; }
            public string? Email { get; set; }
            public string? FirstName { get; set; }
            public string? LastName { get; set; }
            public string? CompanyId { get; set; }
            public string? NewPassword { get; set; }
            public bool IsAdmin { get; set; }
            public string? RoleName { get; set; }
        }
        public class ResetPasswordRequest { public string NewPassword { get; set; } = ""; }
    }
}
