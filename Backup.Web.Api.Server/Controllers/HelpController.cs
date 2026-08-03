using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Authorization;
using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Models.Entities;
using Backup.Web.Api.Server.Models.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RESTFulSense.Controllers;
using Authorize = Microsoft.AspNetCore.Authorization.AuthorizeAttribute;

namespace Backup.Web.Api.Server.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/help")]
    public class HelpController : RESTFulController
    {
        private readonly IStorageBroker storage;

        public HelpController(IStorageBroker storage)
        {
            this.storage = storage;
        }

        /// <summary>Articles publiés (langue UI) — lecture pour tout utilisateur authentifié.</summary>
        [HttpGet("published")]
        public async Task<IActionResult> GetPublished([FromQuery] string lang = "fr")
        {
            lang = NormalizeLang(lang);
            var now = DateTime.UtcNow;
            var items = await this.storage.SelectAllHelpContents()
                .AsNoTracking()
                .Where(h => h.Lang == lang && h.Status == "Published")
                .Where(h => h.ValidFrom == null || h.ValidFrom <= now)
                .Where(h => h.ValidTo == null || h.ValidTo >= now)
                .OrderBy(h => h.HelpKey)
                .ToListAsync();
            return Ok(items);
        }

        [HttpGet("admin")]
        [RequirePermission(Permissions.HelpManage)]
        public async Task<IActionResult> GetAllAdmin([FromQuery] string? lang = null, [FromQuery] string? status = null)
        {
            var q = this.storage.SelectAllHelpContents().AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(lang)) q = q.Where(h => h.Lang == NormalizeLang(lang));
            if (!string.IsNullOrWhiteSpace(status)) q = q.Where(h => h.Status == status);
            var items = await q.OrderBy(h => h.HelpKey).ThenBy(h => h.Lang).ToListAsync();
            return Ok(items);
        }

        [HttpGet("admin/{id:int}")]
        [RequirePermission(Permissions.HelpManage)]
        public async Task<IActionResult> GetById(int id)
        {
            var item = await this.storage.SelectHelpContentByIdAsync(id);
            if (item == null) return NotFound();
            return Ok(item);
        }

        [HttpPost("admin")]
        [RequirePermission(Permissions.HelpManage)]
        public async Task<IActionResult> Create([FromBody] HelpContent dto)
        {
            if (string.IsNullOrWhiteSpace(dto.HelpKey) || string.IsNullOrWhiteSpace(dto.Title))
                return BadRequest(new { error = "HelpKey et Title sont obligatoires." });

            dto.Lang = NormalizeLang(dto.Lang);
            dto.Status = string.IsNullOrWhiteSpace(dto.Status) ? "Draft" : dto.Status;
            dto.Version = string.IsNullOrWhiteSpace(dto.Version) ? "v1.0.0" : dto.Version;
            dto.CreatedAt = DateTime.UtcNow;
            dto.UpdatedAt = DateTime.UtcNow;
            dto.UpdatedBy = User.Identity?.Name;

            var exists = await this.storage.SelectAllHelpContents()
                .AnyAsync(h => h.HelpKey == dto.HelpKey && h.Lang == dto.Lang);
            if (exists) return Conflict(new { error = "Un contenu existe déjà pour cette clé/langue." });

            var saved = await this.storage.InsertHelpContentAsync(dto);
            return Ok(saved);
        }

        [HttpPut("admin/{id:int}")]
        [RequirePermission(Permissions.HelpManage)]
        public async Task<IActionResult> Update(int id, [FromBody] HelpContent dto)
        {
            var existing = await this.storage.SelectHelpContentByIdAsync(id);
            if (existing == null) return NotFound();

            existing.Title = dto.Title ?? existing.Title;
            existing.N1 = dto.N1;
            existing.Body = dto.Body;
            existing.Rules = dto.Rules;
            existing.Example = dto.Example;
            existing.Guide = dto.Guide;
            existing.Version = string.IsNullOrWhiteSpace(dto.Version) ? existing.Version : dto.Version;
            existing.Status = string.IsNullOrWhiteSpace(dto.Status) ? existing.Status : dto.Status;
            existing.ValidFrom = dto.ValidFrom;
            existing.ValidTo = dto.ValidTo;
            existing.RgIds = dto.RgIds;
            existing.DocumentType = dto.DocumentType;
            existing.FieldId = dto.FieldId;
            existing.UpdatedAt = DateTime.UtcNow;
            existing.UpdatedBy = User.Identity?.Name;

            var saved = await this.storage.UpdateHelpContentAsync(existing);
            return Ok(saved);
        }

        [HttpPost("admin/{id:int}/transition")]
        [RequirePermission(Permissions.HelpManage)]
        public async Task<IActionResult> Transition(int id, [FromBody] HelpTransitionRequest req)
        {
            var existing = await this.storage.SelectHelpContentByIdAsync(id);
            if (existing == null) return NotFound();

            var next = (req.Status ?? "").Trim();
            var allowed = new[] { "Draft", "InReview", "ValidatedBusiness", "ValidatedLegal", "Published", "Archived" };
            if (!allowed.Contains(next)) return BadRequest(new { error = "Statut invalide." });

            existing.Status = next;
            existing.UpdatedAt = DateTime.UtcNow;
            existing.UpdatedBy = User.Identity?.Name;
            if (next == "Published" && existing.ValidFrom == null)
                existing.ValidFrom = DateTime.UtcNow;

            var saved = await this.storage.UpdateHelpContentAsync(existing);
            return Ok(saved);
        }

        [HttpDelete("admin/{id:int}")]
        [RequirePermission(Permissions.HelpManage)]
        public async Task<IActionResult> Delete(int id)
        {
            var existing = await this.storage.SelectHelpContentByIdAsync(id);
            if (existing == null) return NotFound();
            // Soft: archive
            existing.Status = "Archived";
            existing.UpdatedAt = DateTime.UtcNow;
            existing.UpdatedBy = User.Identity?.Name;
            await this.storage.UpdateHelpContentAsync(existing);
            return Ok(new { ok = true });
        }

        [HttpPost("feedback")]
        public async Task<IActionResult> Feedback([FromBody] HelpFeedbackRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.HelpKey) || (req.Vote != "up" && req.Vote != "down"))
                return BadRequest(new { error = "HelpKey et Vote (up|down) requis." });

            var evt = new HelpFeedbackEvent
            {
                HelpKey = req.HelpKey.Trim(),
                Vote = req.Vote,
                Comment = req.Comment,
                Reason = req.Reason,
                UserId = User.FindFirstValue(ClaimTypes.NameIdentifier),
                CreatedAt = DateTime.UtcNow
            };
            await this.storage.InsertHelpFeedbackEventAsync(evt);
            return Ok(new { ok = true });
        }

        [HttpPost("analytics")]
        public async Task<IActionResult> Analytics([FromBody] HelpAnalyticsRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.HelpKey) || string.IsNullOrWhiteSpace(req.Action))
                return BadRequest(new { error = "HelpKey et Action requis." });

            var evt = new HelpAnalyticsEvent
            {
                HelpKey = req.HelpKey.Trim(),
                Action = req.Action.Trim(),
                UserId = User.FindFirstValue(ClaimTypes.NameIdentifier),
                CreatedAt = DateTime.UtcNow
            };
            await this.storage.InsertHelpAnalyticsEventAsync(evt);
            return Ok(new { ok = true });
        }

        [HttpGet("analytics/summary")]
        [RequirePermission(Permissions.HelpManage)]
        public async Task<IActionResult> AnalyticsSummary([FromQuery] int days = 30)
        {
            days = Math.Clamp(days, 1, 365);
            var since = DateTime.UtcNow.AddDays(-days);

            var opens = await this.storage.SelectAllHelpAnalyticsEvents()
                .AsNoTracking()
                .Where(e => e.CreatedAt >= since)
                .GroupBy(e => e.HelpKey)
                .Select(g => new { helpKey = g.Key, opens = g.Count() })
                .ToListAsync();

            var feedback = await this.storage.SelectAllHelpFeedbackEvents()
                .AsNoTracking()
                .Where(e => e.CreatedAt >= since)
                .GroupBy(e => e.HelpKey)
                .Select(g => new
                {
                    helpKey = g.Key,
                    up = g.Count(x => x.Vote == "up"),
                    down = g.Count(x => x.Vote == "down")
                })
                .ToListAsync();

            var publishedCount = await this.storage.SelectAllHelpContents()
                .CountAsync(h => h.Status == "Published");
            var draftCount = await this.storage.SelectAllHelpContents()
                .CountAsync(h => h.Status == "Draft" || h.Status == "InReview");

            var pendingDown = await this.storage.SelectAllHelpFeedbackEvents()
                .CountAsync(e => e.Vote == "down" && e.CreatedAt >= since);

            return Ok(new
            {
                days,
                publishedCount,
                draftCount,
                pendingDownReports = pendingDown,
                byKey = opens.Select(o =>
                {
                    var f = feedback.FirstOrDefault(x => x.helpKey == o.helpKey);
                    var up = f?.up ?? 0;
                    var down = f?.down ?? 0;
                    var total = up + down;
                    return new
                    {
                        o.helpKey,
                        o.opens,
                        up,
                        down,
                        usefulness = total == 0 ? (double?)null : Math.Round(100.0 * up / total, 1)
                    };
                }).OrderByDescending(x => x.opens).ToList()
            });
        }

        private static string NormalizeLang(string? lang)
        {
            var l = (lang ?? "fr").Trim().ToLowerInvariant();
            if (l.StartsWith("nl")) return "nl";
            if (l.StartsWith("en")) return "en";
            return "fr";
        }
    }

    public class HelpTransitionRequest
    {
        public string? Status { get; set; }
    }

    public class HelpFeedbackRequest
    {
        public string HelpKey { get; set; } = string.Empty;
        public string Vote { get; set; } = "up";
        public string? Comment { get; set; }
        public string? Reason { get; set; }
    }

    public class HelpAnalyticsRequest
    {
        public string HelpKey { get; set; } = string.Empty;
        public string Action { get; set; } = "open";
    }
}
