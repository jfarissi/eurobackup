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
        private readonly ICompanyContextService companyContext;

        public AdminController(
            IStorageBroker storage,
            UserManager<User> userManager,
            IPermissionChangeNotifier permissionNotifier,
            ICompanyContextService companyContext)
        {
            this.storage = storage;
            this.userManager = userManager;
            this.permissionNotifier = permissionNotifier;
            this.companyContext = companyContext;
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
            company.AllowNegativeStock = req.AllowNegativeStock;
            company.OpenFiscalPeriodStart = req.OpenFiscalPeriodStart;
            company.OpenFiscalPeriodEnd = req.OpenFiscalPeriodEnd;
            if (req.RetentionMonths > 0) company.RetentionMonths = req.RetentionMonths;
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

        /// <summary>
        /// Journal d'activité — DocumentAuditLog (actions métier) + EntityAuditLog (CRUD Created/Updated/Deleted).
        /// </summary>
        [HttpGet("activity")]
        [RequirePermission(Permissions.UserRead)]
        public async Task<IActionResult> GetActivity(
            [FromQuery] string? search = null,
            [FromQuery] string? documentType = null,
            [FromQuery] string? actor = null,
            [FromQuery] string? companyId = null,
            [FromQuery] string? source = null,
            [FromQuery] DateTime? from = null,
            [FromQuery] DateTime? to = null,
            [FromQuery] int take = 200)
        {
            take = Math.Clamp(take, 1, 500);
            var company = !string.IsNullOrWhiteSpace(companyId)
                ? companyId.Trim()
                : this.companyContext.GetCurrentCompanyId();

            var wantDocs = string.IsNullOrWhiteSpace(source)
                || string.Equals(source, "document", StringComparison.OrdinalIgnoreCase)
                || string.Equals(source, "all", StringComparison.OrdinalIgnoreCase);
            var wantEntities = string.IsNullOrWhiteSpace(source)
                || string.Equals(source, "entity", StringComparison.OrdinalIgnoreCase)
                || string.Equals(source, "all", StringComparison.OrdinalIgnoreCase);

            var fetch = Math.Min(take * 2, 1000);
            var items = new List<ActivityLogDto>();

            if (wantDocs)
            {
                var query = this.storage.SelectAllDocumentAuditLogs().AsQueryable();
                if (!string.IsNullOrWhiteSpace(company))
                    query = query.Where(l => l.CompanyId == company);

                if (!string.IsNullOrWhiteSpace(documentType))
                {
                    var dt = documentType.Trim();
                    query = query.Where(l => l.DocumentType == dt);
                }

                if (!string.IsNullOrWhiteSpace(actor))
                {
                    var a = actor.Trim().ToLower();
                    query = query.Where(l => l.Actor != null && l.Actor.ToLower().Contains(a));
                }

                if (from.HasValue)
                    query = query.Where(l => l.CreatedAt >= from.Value.ToUniversalTime());
                if (to.HasValue)
                {
                    var end = to.Value.Date.AddDays(1).ToUniversalTime();
                    query = query.Where(l => l.CreatedAt < end);
                }

                if (!string.IsNullOrWhiteSpace(search))
                {
                    var s = search.Trim().ToLower();
                    query = query.Where(l =>
                        (l.Summary != null && l.Summary.ToLower().Contains(s))
                        || (l.Details != null && l.Details.ToLower().Contains(s))
                        || (l.Action != null && l.Action.ToLower().Contains(s))
                        || (l.DocumentType != null && l.DocumentType.ToLower().Contains(s))
                        || (l.Actor != null && l.Actor.ToLower().Contains(s))
                        || l.DocumentId.ToString().Contains(s));
                }

                var docs = await query
                    .OrderByDescending(l => l.CreatedAt)
                    .Take(fetch)
                    .Select(l => new ActivityLogDto
                    {
                        Id = l.Id,
                        Source = "document",
                        DocumentType = l.DocumentType,
                        EntityKey = l.DocumentId.ToString(),
                        DocumentId = l.DocumentId,
                        Action = l.Action,
                        Summary = l.Summary,
                        Details = l.Details,
                        Actor = l.Actor,
                        CompanyId = l.CompanyId,
                        CreatedAt = l.CreatedAt
                    })
                    .ToListAsync();
                items.AddRange(docs);
            }

            if (wantEntities)
            {
                var query = this.storage.SelectAllEntityAuditLogs().AsQueryable();
                if (!string.IsNullOrWhiteSpace(company))
                    query = query.Where(l => l.CompanyId == company);

                if (!string.IsNullOrWhiteSpace(documentType))
                {
                    var dt = documentType.Trim();
                    query = query.Where(l => l.EntityType == dt);
                }

                if (!string.IsNullOrWhiteSpace(actor))
                {
                    var a = actor.Trim().ToLower();
                    query = query.Where(l => l.Actor != null && l.Actor.ToLower().Contains(a));
                }

                if (from.HasValue)
                    query = query.Where(l => l.CreatedAt >= from.Value.ToUniversalTime());
                if (to.HasValue)
                {
                    var end = to.Value.Date.AddDays(1).ToUniversalTime();
                    query = query.Where(l => l.CreatedAt < end);
                }

                if (!string.IsNullOrWhiteSpace(search))
                {
                    var s = search.Trim().ToLower();
                    query = query.Where(l =>
                        (l.Summary != null && l.Summary.ToLower().Contains(s))
                        || (l.Details != null && l.Details.ToLower().Contains(s))
                        || (l.Action != null && l.Action.ToLower().Contains(s))
                        || (l.EntityType != null && l.EntityType.ToLower().Contains(s))
                        || (l.EntityKey != null && l.EntityKey.ToLower().Contains(s))
                        || (l.Actor != null && l.Actor.ToLower().Contains(s)));
                }

                var entities = await query
                    .OrderByDescending(l => l.CreatedAt)
                    .Take(fetch)
                    .Select(l => new ActivityLogDto
                    {
                        Id = l.Id,
                        Source = "entity",
                        DocumentType = l.EntityType,
                        EntityKey = l.EntityKey,
                        DocumentId = 0,
                        Action = l.Action,
                        Summary = l.Summary,
                        Details = l.Details,
                        Actor = l.Actor,
                        CompanyId = l.CompanyId,
                        CreatedAt = l.CreatedAt
                    })
                    .ToListAsync();
                items.AddRange(entities);
            }

            var page = items
                .OrderByDescending(i => i.CreatedAt)
                .Take(take)
                .ToList();

            await this.ResolveActorDisplayNamesAsync(page);

            return Ok(new ActivityPageDto { Items = page, Count = page.Count });
        }

        /// <summary>Remplace les Actor stockés en GUID par username / nom affiché.</summary>
        private async Task ResolveActorDisplayNamesAsync(List<ActivityLogDto> items)
        {
            var guidActors = items
                .Select(i => i.Actor)
                .Where(a => !string.IsNullOrWhiteSpace(a) && Guid.TryParse(a, out _))
                .Select(a => Guid.Parse(a!))
                .Distinct()
                .ToList();

            if (guidActors.Count == 0) return;

            var users = await this.userManager.Users
                .Where(u => guidActors.Contains(u.Id))
                .Select(u => new { u.Id, u.UserName, u.Email, u.Name, u.FamilyName })
                .ToListAsync();

            var map = users.ToDictionary(
                u => u.Id.ToString(),
                u =>
                {
                    var full = $"{u.Name} {u.FamilyName}".Trim();
                    if (!string.IsNullOrWhiteSpace(full)) return full;
                    if (!string.IsNullOrWhiteSpace(u.UserName)) return u.UserName!;
                    if (!string.IsNullOrWhiteSpace(u.Email)) return u.Email!;
                    return u.Id.ToString();
                },
                StringComparer.OrdinalIgnoreCase);

            foreach (var item in items)
            {
                if (item.Actor != null && map.TryGetValue(item.Actor, out var display))
                    item.Actor = display;
            }
        }

        // ── DTOs ──────────────────────────────────────────────────────────────

        private static CompanyAdminDto ToCompanyDto(Company c) => new()
        {
            Id = c.Id, TenantId = c.TenantId, TenantName = c.Tenant?.Name,
            Name = c.Name, IsActive = c.IsActive,
            DefaultLanguageCode = c.DefaultLanguageCode, DefaultCurrencyCode = c.DefaultCurrencyCode,
            AllowNegativeStock = c.AllowNegativeStock,
            OpenFiscalPeriodStart = c.OpenFiscalPeriodStart,
            OpenFiscalPeriodEnd = c.OpenFiscalPeriodEnd,
            RetentionMonths = c.RetentionMonths,
            CreatedAt = c.CreatedAt
        };

        public class TenantDto { public string Id { get; set; } = ""; public string Name { get; set; } = ""; public bool IsActive { get; set; } public DateTime CreatedAt { get; set; } public int CompanyCount { get; set; } }
        public class SaveTenantRequest { public string Name { get; set; } = ""; public bool IsActive { get; set; } = true; }
        public class CompanyAdminDto
        {
            public string Id { get; set; } = "";
            public string TenantId { get; set; } = "";
            public string? TenantName { get; set; }
            public string Name { get; set; } = "";
            public bool IsActive { get; set; }
            public string DefaultLanguageCode { get; set; } = "fr-BE";
            public string DefaultCurrencyCode { get; set; } = "EUR";
            public bool AllowNegativeStock { get; set; }
            public DateTime? OpenFiscalPeriodStart { get; set; }
            public DateTime? OpenFiscalPeriodEnd { get; set; }
            public int RetentionMonths { get; set; } = 24;
            public DateTime CreatedAt { get; set; }
        }
        public class SaveCompanyRequest
        {
            public string Name { get; set; } = "";
            public string TenantId { get; set; } = "";
            public bool IsActive { get; set; } = true;
            public string? DefaultLanguageCode { get; set; }
            public string? DefaultCurrencyCode { get; set; }
            public bool AllowNegativeStock { get; set; }
            public DateTime? OpenFiscalPeriodStart { get; set; }
            public DateTime? OpenFiscalPeriodEnd { get; set; }
            public int RetentionMonths { get; set; }
        }
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

        public class ActivityLogDto
        {
            public long Id { get; set; }
            /// <summary>document = action métier ; entity = CRUD CreatedBy/UpdatedBy</summary>
            public string Source { get; set; } = "document";
            public string DocumentType { get; set; } = "";
            public string EntityKey { get; set; } = "";
            public int DocumentId { get; set; }
            public string Action { get; set; } = "";
            public string? Summary { get; set; }
            public string? Details { get; set; }
            public string? Actor { get; set; }
            public string? CompanyId { get; set; }
            public DateTime CreatedAt { get; set; }
        }

        public class ActivityPageDto
        {
            public List<ActivityLogDto> Items { get; set; } = new();
            public int Count { get; set; }
        }
    }
}
