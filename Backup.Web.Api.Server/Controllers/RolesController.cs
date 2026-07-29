using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Authorization;
using Authorize = Microsoft.AspNetCore.Authorization.AuthorizeAttribute;
using Backup.Web.Api.Server.Models.Rols;
using Backup.Web.Api.Server.Models.Security;
using Backup.Web.Api.Server.Services.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Backup.Web.Api.Server.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/roles")]
    public class RolesController : ControllerBase
    {
        private readonly RoleManager<Role> roleManager;
        private readonly IPermissionChangeNotifier permissionNotifier;

        public RolesController(RoleManager<Role> roleManager, IPermissionChangeNotifier permissionNotifier)
        {
            this.roleManager = roleManager;
            this.permissionNotifier = permissionNotifier;
        }

        // ── GET all roles ──────────────────────────────────────────────────────

        [HttpGet]
        [RequirePermission(Permissions.RoleRead)]
        public async Task<IActionResult> GetAll()
        {
            var roles = await this.roleManager.Roles.OrderBy(r => r.Name).ToListAsync();
            var result = new List<RoleDto>();
            foreach (var r in roles)
            {
                var claims = await this.roleManager.GetClaimsAsync(r);
                var perms = claims.Where(c => c.Type == "Permission").Select(c => c.Value).ToList();
                // Admin = toujours le catalogue complet (affichage cohérent avec le runtime)
                if (string.Equals(r.Name, "Admin", StringComparison.OrdinalIgnoreCase))
                    perms = Permissions.All.ToList();

                result.Add(new RoleDto
                {
                    Id = r.Id,
                    Name = r.Name ?? string.Empty,
                    NormalizedName = r.NormalizedName,
                    Permissions = perms
                });
            }
            return Ok(result);
        }

        // ── GET single role ────────────────────────────────────────────────────

        [HttpGet("{id:guid}")]
        [RequirePermission(Permissions.RoleRead)]
        public async Task<IActionResult> GetById(Guid id)
        {
            var role = await this.roleManager.FindByIdAsync(id.ToString());
            if (role == null) return NotFound();
            var claims = await this.roleManager.GetClaimsAsync(role);
            var perms = claims.Where(c => c.Type == "Permission").Select(c => c.Value).ToList();
            if (string.Equals(role.Name, "Admin", StringComparison.OrdinalIgnoreCase))
                perms = Permissions.All.ToList();

            return Ok(new RoleDto
            {
                Id = role.Id,
                Name = role.Name ?? string.Empty,
                NormalizedName = role.NormalizedName,
                Permissions = perms
            });
        }

        // ── POST create role ───────────────────────────────────────────────────

        [HttpPost]
        [RequirePermission(Permissions.RoleCreate)]
        public async Task<IActionResult> Create([FromBody] CreateRoleRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Name))
                return BadRequest(new { message = "Le nom du rôle est requis." });

            if (await this.roleManager.RoleExistsAsync(req.Name))
                return Conflict(new { message = $"Le rôle '{req.Name}' existe déjà." });

            var role = new Role { Id = Guid.NewGuid(), Name = req.Name };
            var result = await this.roleManager.CreateAsync(role);
            if (!result.Succeeded)
                return BadRequest(new { message = string.Join("; ", result.Errors.Select(e => e.Description)) });

            // Assign permissions
            foreach (var perm in req.Permissions ?? new List<string>())
                await this.roleManager.AddClaimAsync(role, new Claim("Permission", perm));

            if (!string.IsNullOrWhiteSpace(role.Name))
                await this.permissionNotifier.NotifyRolePermissionsChangedAsync(role.Name);

            return Ok(new RoleDto { Id = role.Id, Name = role.Name, Permissions = req.Permissions ?? new() });
        }

        // ── PUT update role ────────────────────────────────────────────────────

        [HttpPut("{id:guid}")]
        [RequirePermission(Permissions.RoleUpdate)]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateRoleRequest req)
        {
            var role = await this.roleManager.FindByIdAsync(id.ToString());
            if (role == null) return NotFound();

            var isAdminRole = string.Equals(role.Name, "Admin", StringComparison.OrdinalIgnoreCase);
            if (isAdminRole && !string.IsNullOrWhiteSpace(req.Name) &&
                !string.Equals(req.Name, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { message = "Le rôle Admin ne peut pas être renommé." });
            }

            if (!string.IsNullOrWhiteSpace(req.Name) && req.Name != role.Name)
            {
                role.Name = req.Name;
                var renameResult = await this.roleManager.UpdateAsync(role);
                if (!renameResult.Succeeded)
                    return BadRequest(new { message = string.Join("; ", renameResult.Errors.Select(e => e.Description)) });
            }

            // Replace permissions — Admin = toujours toutes les permissions
            if (req.Permissions != null || isAdminRole)
            {
                var targetPerms = isAdminRole
                    ? Permissions.All.ToList()
                    : (req.Permissions ?? new List<string>());

                var existingClaims = await this.roleManager.GetClaimsAsync(role);
                foreach (var c in existingClaims.Where(c => c.Type == "Permission"))
                    await this.roleManager.RemoveClaimAsync(role, c);
                foreach (var perm in targetPerms)
                    await this.roleManager.AddClaimAsync(role, new Claim("Permission", perm));
            }

            var updatedClaims = await this.roleManager.GetClaimsAsync(role);
            if (!string.IsNullOrWhiteSpace(role.Name))
                await this.permissionNotifier.NotifyRolePermissionsChangedAsync(role.Name);

            var responsePerms = isAdminRole
                ? Permissions.All.ToList()
                : updatedClaims.Where(c => c.Type == "Permission").Select(c => c.Value).ToList();

            return Ok(new RoleDto
            {
                Id = role.Id,
                Name = role.Name ?? string.Empty,
                NormalizedName = role.NormalizedName,
                Permissions = responsePerms
            });
        }

        // ── DELETE role ────────────────────────────────────────────────────────

        [HttpDelete("{id:guid}")]
        [RequirePermission(Permissions.RoleDelete)]
        public async Task<IActionResult> Delete(Guid id)
        {
            var role = await this.roleManager.FindByIdAsync(id.ToString());
            if (role == null) return NotFound();
            if (string.Equals(role.Name, "Admin", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { message = "Le rôle Admin ne peut pas être supprimé." });
            var result = await this.roleManager.DeleteAsync(role);
            if (!result.Succeeded)
                return BadRequest(new { message = string.Join("; ", result.Errors.Select(e => e.Description)) });
            return NoContent();
        }

        // ── GET all available permissions ──────────────────────────────────────

        [HttpGet("permissions")]
        public IActionResult GetPermissions()
        {
            return Ok(Permissions.All);
        }

        // ── DTOs ───────────────────────────────────────────────────────────────

        public class RoleDto
        {
            public Guid Id { get; set; }
            public string Name { get; set; } = "";
            public string? NormalizedName { get; set; }
            public List<string> Permissions { get; set; } = new();
        }

        public class CreateRoleRequest
        {
            public string Name { get; set; } = "";
            public List<string>? Permissions { get; set; }
        }

        public class UpdateRoleRequest
        {
            public string? Name { get; set; }
            public List<string>? Permissions { get; set; }
        }
    }
}
