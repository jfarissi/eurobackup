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
    [Route("api/erp-brands")]
    public class ErpBrandsController : RESTFulController
    {
        private readonly IStorageBroker storage;

        public ErpBrandsController(IStorageBroker storage)
        {
            this.storage = storage;
        }

        [HttpGet]
        [RequireAnyPermission(Permissions.BrandRead, Permissions.ProductRead)]
        public async Task<IActionResult> GetAll([FromQuery] bool? activeOnly = null)
        {
            // Ne pas synchroniser 100k+ produits à chaque GET (timeout / liste vide côté UI).
            // Seed uniquement si la table est encore vide.
            var hasAny = await this.storage.SelectAllErpBrands().AsNoTracking().AnyAsync();
            if (!hasAny)
                await EnsureBrandsFromProductNamesAsync();

            // Projection sans CreatedAt/UpdatedAt NULL legacy (évite InvalidOperationException).
            var query = this.storage.SelectAllErpBrands().AsNoTracking();
            if (activeOnly == true)
                query = query.Where(b => b.IsActive);

            var list = await query
                .OrderBy(b => b.Name)
                .Select(b => new
                {
                    b.Id,
                    b.Name,
                    b.Slug,
                    b.LogoUrl,
                    b.WebsiteUrl,
                    b.Description,
                    b.IsActive,
                    b.CreatedBy,
                    b.UpdatedBy
                })
                .ToListAsync();
            return Ok(list);
        }

        /// <summary>
        /// Crée les ErpBrands manquants à partir des libellés Brand déjà présents sur les produits.
        /// </summary>
        private async Task EnsureBrandsFromProductNamesAsync()
        {
            try
            {
                var existing = await this.storage.SelectAllErpBrands()
                    .AsNoTracking()
                    .Select(b => new { b.Name, b.Slug })
                    .ToListAsync();
                var existingNames = new HashSet<string>(
                    existing.Where(b => !string.IsNullOrWhiteSpace(b.Name)).Select(b => b.Name.Trim()),
                    StringComparer.OrdinalIgnoreCase);
                var existingSlugs = new HashSet<string>(
                    existing.Where(b => !string.IsNullOrWhiteSpace(b.Slug)).Select(b => b.Slug.Trim()),
                    StringComparer.OrdinalIgnoreCase);

                // Distinct côté SQL — ne jamais charger toute la table produits en mémoire.
                var fromBrand = await this.storage.SelectAllErpProducts()
                    .AsNoTracking()
                    .Where(p => p.Brand != null && p.Brand != "")
                    .Select(p => p.Brand!)
                    .Distinct()
                    .ToListAsync();
                var fromMfr = await this.storage.SelectAllErpProducts()
                    .AsNoTracking()
                    .Where(p => (p.Brand == null || p.Brand == "")
                                && p.Manufacturer != null && p.Manufacturer != "")
                    .Select(p => p.Manufacturer!)
                    .Distinct()
                    .ToListAsync();

                var names = fromBrand.Concat(fromMfr)
                    .Select(n => n.Trim())
                    .Where(n => n.Length > 0)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var now = DateTime.UtcNow;
                foreach (var name in names)
                {
                    if (existingNames.Contains(name))
                        continue;

                    try
                    {
                        var slug = MakeUniqueSlug(name, existingSlugs);
                        await this.storage.InsertErpBrandAsync(new ErpBrand
                        {
                            Name = name,
                            Slug = slug,
                            IsActive = true,
                            CreatedAt = now,
                            UpdatedAt = now
                        });
                        existingNames.Add(name);
                        existingSlugs.Add(slug);
                    }
                    catch
                    {
                        // Concurrence / contrainte : on ignore.
                    }
                }
            }
            catch
            {
                // Ne bloque pas le listing si la synchro échoue.
            }
        }

        private static string MakeUniqueSlug(string name, HashSet<string> existingSlugs)
        {
            var baseSlug = Slugify(name);
            var slug = baseSlug;
            var n = 2;
            while (existingSlugs.Contains(slug))
                slug = $"{baseSlug}-{n++}";
            return slug;
        }

        [HttpGet("{id:int}")]
        [RequireAnyPermission(Permissions.BrandRead, Permissions.ProductRead)]
        public async Task<IActionResult> GetById(int id)
        {
            var brand = await this.storage.SelectAllErpBrands()
                .AsNoTracking()
                .Where(b => b.Id == id)
                .Select(b => new
                {
                    b.Id,
                    b.Name,
                    b.Slug,
                    b.LogoUrl,
                    b.WebsiteUrl,
                    b.Description,
                    b.IsActive,
                    b.CreatedBy,
                    b.UpdatedBy
                })
                .FirstOrDefaultAsync();
            if (brand == null) return NotFound();
            return Ok(brand);
        }

        [HttpPost]
        [RequireAnyPermission(Permissions.BrandCreate, Permissions.ProductCreate)]
        public async Task<IActionResult> Create([FromBody] ErpBrand dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.Name))
                return BadRequest(new { error = "Name requis." });

            var name = dto.Name.Trim();
            var exists = await this.storage.SelectAllErpBrands()
                .AnyAsync(b => b.Name == name);
            if (exists)
                return Conflict(new { error = "Une marque avec ce nom existe déjà." });

            var now = DateTime.UtcNow;
            var brand = new ErpBrand
            {
                Name = name,
                Slug = string.IsNullOrWhiteSpace(dto.Slug) ? Slugify(name) : dto.Slug.Trim(),
                LogoUrl = NullIfWhite(dto.LogoUrl),
                WebsiteUrl = NullIfWhite(dto.WebsiteUrl),
                Description = NullIfWhite(dto.Description),
                IsActive = dto.IsActive,
                CreatedAt = now,
                UpdatedAt = now,
                CreatedBy = User.Identity?.Name
            };

            var created = await this.storage.InsertErpBrandAsync(brand);
            return Ok(created);
        }

        [HttpPut("{id:int}")]
        [RequireAnyPermission(Permissions.BrandUpdate, Permissions.ProductUpdate)]
        public async Task<IActionResult> Update(int id, [FromBody] ErpBrand dto)
        {
            var existing = await this.storage.SelectAllErpBrands()
                .FirstOrDefaultAsync(b => b.Id == id);
            if (existing == null) return NotFound();

            if (string.IsNullOrWhiteSpace(dto.Name))
                return BadRequest(new { error = "Name requis." });

            var name = dto.Name.Trim();
            var conflict = await this.storage.SelectAllErpBrands()
                .AnyAsync(b => b.Name == name && b.Id != id);
            if (conflict)
                return Conflict(new { error = "Une marque avec ce nom existe déjà." });

            existing.Name = name;
            existing.Slug = string.IsNullOrWhiteSpace(dto.Slug) ? existing.Slug : dto.Slug.Trim();
            if (string.IsNullOrWhiteSpace(existing.Slug))
                existing.Slug = Slugify(name);
            existing.LogoUrl = NullIfWhite(dto.LogoUrl);
            existing.WebsiteUrl = NullIfWhite(dto.WebsiteUrl);
            existing.Description = NullIfWhite(dto.Description);
            existing.IsActive = dto.IsActive;
            existing.UpdatedAt = DateTime.UtcNow;
            existing.UpdatedBy = User.Identity?.Name;

            var updated = await this.storage.UpdateErpBrandAsync(existing);
            return Ok(updated);
        }

        [HttpDelete("{id:int}")]
        [RequireAnyPermission(Permissions.BrandDelete, Permissions.ProductDelete)]
        public async Task<IActionResult> Deactivate(int id)
        {
            var existing = await this.storage.SelectAllErpBrands()
                .FirstOrDefaultAsync(b => b.Id == id);
            if (existing == null) return NotFound();

            existing.IsActive = false;
            existing.UpdatedAt = DateTime.UtcNow;
            existing.UpdatedBy = User.Identity?.Name;
            await this.storage.UpdateErpBrandAsync(existing);
            return NoContent();
        }

        private static string? NullIfWhite(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private static string Slugify(string input)
        {
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
            return string.IsNullOrEmpty(slug) ? "brand" : slug;
        }
    }
}
