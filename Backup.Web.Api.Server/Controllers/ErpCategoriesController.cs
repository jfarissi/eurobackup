using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Authorization;
using Authorize = Microsoft.AspNetCore.Authorization.AuthorizeAttribute;
using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Models;
using Backup.Web.Api.Server.Models.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RESTFulSense.Controllers;

namespace Backup.Web.Api.Server.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/erp-categories")]
    public class ErpCategoriesController : RESTFulController
    {
        private readonly IStorageBroker storage;

        public ErpCategoriesController(IStorageBroker storage)
        {
            this.storage = storage;
        }

        [HttpGet]
        [RequireAnyPermission(Permissions.CategoryRead, Permissions.ProductRead)]
        public async Task<IActionResult> GetAll(
            [FromQuery] string? level = null,
            [FromQuery] int? parentId = null,
            [FromQuery] bool? activeOnly = null)
        {
            try
            {
                var query = this.storage.SelectAllErpCategories().AsNoTracking();
                if (!string.IsNullOrWhiteSpace(level))
                    query = query.Where(c => c.Level == level);
                if (parentId.HasValue)
                    query = query.Where(c => c.ParentId == parentId.Value);
                if (activeOnly == true)
                    query = query.Where(c => c.IsActive);

                var list = await query
                    .OrderBy(c => c.SortOrder)
                    .ThenBy(c => c.NameFr)
                    .ThenBy(c => c.NameNl)
                    .ToListAsync();
                return Ok(list);
            }
            catch (Exception ex)
            {
                var msg = ex.InnerException?.Message ?? ex.Message;
                return StatusCode(500, new { message = "Lecture des catégories impossible", detail = msg });
            }
        }

        [HttpGet("{id:int}")]
        [RequireAnyPermission(Permissions.CategoryRead, Permissions.ProductRead)]
        public async Task<IActionResult> GetById(int id)
        {
            var cat = await this.storage.SelectAllErpCategories()
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == id);
            if (cat == null) return NotFound();
            return Ok(cat);
        }

        [HttpPost]
        [RequireAnyPermission(Permissions.CategoryCreate, Permissions.ProductCreate)]
        public async Task<IActionResult> Create([FromBody] ErpCategory dto)
        {
            if (dto == null)
                return BadRequest(new { error = "Corps requis." });

            var level = (dto.Level ?? string.Empty).Trim();
            if (level is not ("MainType" or "Type" or "SubType"))
                return BadRequest(new { error = "Level doit être MainType, Type ou SubType." });

            var parentError = await ValidateParentAsync(level, dto.ParentId);
            if (parentError != null)
                return BadRequest(new { error = parentError });

            if (string.IsNullOrWhiteSpace(dto.NameFr) && string.IsNullOrWhiteSpace(dto.NameNl) && string.IsNullOrWhiteSpace(dto.NameEn))
                return BadRequest(new { error = "Au moins un nom (FR/NL/EN) est requis." });

            var now = DateTime.UtcNow;
            var nameFr = (dto.NameFr ?? string.Empty).Trim();
            var nameNl = (dto.NameNl ?? string.Empty).Trim();
            var nameEn = (dto.NameEn ?? string.Empty).Trim();
            var label = !string.IsNullOrEmpty(nameFr) ? nameFr : (!string.IsNullOrEmpty(nameNl) ? nameNl : nameEn);

            var category = new ErpCategory
            {
                Level = level,
                ParentId = level == "MainType" ? null : dto.ParentId,
                ErpExternalId = string.IsNullOrWhiteSpace(dto.ErpExternalId)
                    ? $"MANUAL-{Guid.NewGuid():N}"
                    : dto.ErpExternalId.Trim(),
                NameFr = nameFr,
                NameNl = nameNl,
                NameEn = nameEn,
                SlugFr = string.IsNullOrWhiteSpace(dto.SlugFr) ? Slugify(nameFr.Length > 0 ? nameFr : label) : dto.SlugFr.Trim(),
                SlugNl = string.IsNullOrWhiteSpace(dto.SlugNl) ? Slugify(nameNl.Length > 0 ? nameNl : label) : dto.SlugNl.Trim(),
                SlugEn = string.IsNullOrWhiteSpace(dto.SlugEn) ? Slugify(nameEn.Length > 0 ? nameEn : label) : dto.SlugEn.Trim(),
                SortOrder = dto.SortOrder,
                IsActive = dto.IsActive,
                CreatedAt = now,
                UpdatedAt = now,
                CreatedBy = User.Identity?.Name
            };

            var created = await this.storage.InsertErpCategoryAsync(category);
            return Ok(created);
        }

        [HttpPut("{id:int}")]
        [RequireAnyPermission(Permissions.CategoryUpdate, Permissions.ProductUpdate)]
        public async Task<IActionResult> Update(int id, [FromBody] ErpCategory dto)
        {
            var existing = await this.storage.SelectAllErpCategories()
                .FirstOrDefaultAsync(c => c.Id == id);
            if (existing == null) return NotFound();

            var level = string.IsNullOrWhiteSpace(dto.Level) ? existing.Level : dto.Level.Trim();
            if (level is not ("MainType" or "Type" or "SubType"))
                return BadRequest(new { error = "Level doit être MainType, Type ou SubType." });

            var parentId = level == "MainType" ? null : (dto.ParentId ?? existing.ParentId);
            var parentError = await ValidateParentAsync(level, parentId);
            if (parentError != null)
                return BadRequest(new { error = parentError });

            existing.Level = level;
            existing.ParentId = parentId;
            if (!string.IsNullOrWhiteSpace(dto.ErpExternalId))
                existing.ErpExternalId = dto.ErpExternalId.Trim();

            if (dto.NameFr != null) existing.NameFr = dto.NameFr.Trim();
            if (dto.NameNl != null) existing.NameNl = dto.NameNl.Trim();
            if (dto.NameEn != null) existing.NameEn = dto.NameEn.Trim();

            if (!string.IsNullOrWhiteSpace(dto.SlugFr)) existing.SlugFr = dto.SlugFr.Trim();
            if (!string.IsNullOrWhiteSpace(dto.SlugNl)) existing.SlugNl = dto.SlugNl.Trim();
            if (!string.IsNullOrWhiteSpace(dto.SlugEn)) existing.SlugEn = dto.SlugEn.Trim();

            existing.SortOrder = dto.SortOrder;
            existing.IsActive = dto.IsActive;
            existing.UpdatedAt = DateTime.UtcNow;
            existing.UpdatedBy = User.Identity?.Name;

            var updated = await this.storage.UpdateErpCategoryAsync(existing);
            return Ok(updated);
        }

        [HttpDelete("{id:int}")]
        [RequireAnyPermission(Permissions.CategoryDelete, Permissions.ProductDelete)]
        public async Task<IActionResult> Deactivate(int id)
        {
            var existing = await this.storage.SelectAllErpCategories()
                .FirstOrDefaultAsync(c => c.Id == id);
            if (existing == null) return NotFound();

            existing.IsActive = false;
            existing.UpdatedAt = DateTime.UtcNow;
            existing.UpdatedBy = User.Identity?.Name;
            await this.storage.UpdateErpCategoryAsync(existing);
            return NoContent();
        }

        private async Task<string?> ValidateParentAsync(string level, int? parentId)
        {
            if (level == "MainType")
            {
                if (parentId.HasValue)
                    return "MainType ne doit pas avoir de parent.";
                return null;
            }

            if (!parentId.HasValue)
                return "ParentId requis pour Type et SubType.";

            var parent = await this.storage.SelectAllErpCategories()
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == parentId.Value);
            if (parent == null)
                return "Parent introuvable.";

            if (level == "Type" && parent.Level != "MainType")
                return "Le parent d'un Type doit être un MainType.";
            if (level == "SubType" && parent.Level != "Type")
                return "Le parent d'un SubType doit être un Type.";

            return null;
        }

        private static string Slugify(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return "category";
            var normalized = input.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (var c in normalized)
            {
                var cat = CharUnicodeInfo.GetUnicodeCategory(c);
                if (cat == UnicodeCategory.NonSpacingMark) continue;
                if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
                else if (char.IsWhiteSpace(c) || c == '-' || c == '_') sb.Append('-');
            }
            var slug = sb.ToString().Trim('-');
            while (slug.Contains("--", StringComparison.Ordinal))
                slug = slug.Replace("--", "-", StringComparison.Ordinal);
            return string.IsNullOrEmpty(slug) ? "category" : slug;
        }
    }
}
