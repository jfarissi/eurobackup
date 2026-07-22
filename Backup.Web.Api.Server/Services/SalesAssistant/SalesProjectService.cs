using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Models;
using Backup.Web.Api.Server.Services.StoreChat;
using Microsoft.Extensions.Logging;

namespace Backup.Web.Api.Server.Services.SalesAssistant
{
    public interface ISalesProjectService
    {
        Task<SalesProject?> SyncFromSessionAsync(StoreChatSession session, CancellationToken ct = default);
        Task ClearSessionProjectAsync(StoreChatSession session, CancellationToken ct = default);
    }

    public class SalesProjectService : ISalesProjectService
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private readonly IStorageBroker _storage;
        private readonly ILogger<SalesProjectService> _logger;

        public SalesProjectService(IStorageBroker storage, ILogger<SalesProjectService> logger)
        {
            _storage = storage;
            _logger = logger;
        }

        public async Task ClearSessionProjectAsync(StoreChatSession session, CancellationToken ct = default)
        {
            if (session.ActiveSalesProjectId is Guid id)
            {
                var existing = await _storage.SelectSalesProjectByIdAsync(id);
                if (existing != null)
                {
                    existing.Status = "Closed";
                    existing.UpdatedAt = DateTime.UtcNow;
                    await _storage.UpdateSalesProjectAsync(existing);
                }
            }

            session.ActiveSalesProjectId = null;
        }

        public async Task<SalesProject?> SyncFromSessionAsync(StoreChatSession session, CancellationToken ct = default)
        {
            var hasSignal = !string.IsNullOrWhiteSpace(session.PreferredBrand)
                            || session.SearchTypeHints.Count > 0
                            || session.PreferredWeightKg is > 0
                            || !string.IsNullOrWhiteSpace(session.ActiveProjectDomainId)
                            || session.WallLengthM is > 0
                            || session.WallHeightM is > 0;

            if (!hasSignal && session.ActiveSalesProjectId is null)
                return null;

            SalesProject project;
            if (session.ActiveSalesProjectId is Guid existingId)
            {
                var loaded = await _storage.SelectSalesProjectByIdAsync(existingId);
                if (loaded == null || !string.Equals(loaded.SessionId, session.SessionId, StringComparison.Ordinal))
                {
                    project = await CreateProjectAsync(session, ct);
                }
                else
                {
                    project = loaded;
                }
            }
            else
            {
                project = await CreateProjectAsync(session, ct);
            }

            ApplySessionToProject(session, project);
            EnsureWallChecklist(project);
            project.UpdatedAt = DateTime.UtcNow;

            // Checklist items nouveaux : stage puis update projet
            foreach (var item in project.ChecklistItems.Where(i => i.Id == 0))
                await _storage.StageInsertSalesProjectChecklistItemAsync(item);

            await _storage.UpdateSalesProjectAsync(project);
            session.ActiveSalesProjectId = project.Id;

            _logger.LogInformation(
                "SalesProject synced Id={ProjectId} Type={Type} Brand={Brand} Weight={Weight} Surface={Surface}",
                project.Id,
                project.ProjectType,
                project.PreferredBrand,
                project.PreferredWeightKg,
                project.SurfaceM2);

            return project;
        }

        private async Task<SalesProject> CreateProjectAsync(StoreChatSession session, CancellationToken ct)
        {
            var project = new SalesProject
            {
                SessionId = session.SessionId,
                Status = "Active",
                ProjectType = MapProjectType(session.ActiveProjectDomainId),
                Title = session.ActiveProjectDomainLabel ?? "Recherche catalogue",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            ApplySessionToProject(session, project);
            EnsureWallChecklist(project);

            var inserted = await _storage.InsertSalesProjectAsync(project);
            session.ActiveSalesProjectId = inserted.Id;
            return inserted;
        }

        private static void ApplySessionToProject(StoreChatSession session, SalesProject project)
        {
            if (!string.IsNullOrWhiteSpace(session.ActiveProjectDomainId))
                project.ProjectType = MapProjectType(session.ActiveProjectDomainId);

            if (!string.IsNullOrWhiteSpace(session.ActiveProjectDomainLabel))
                project.Title = session.ActiveProjectDomainLabel;
            else if (!string.IsNullOrWhiteSpace(session.PreferredBrand))
                project.Title = $"Recherche {session.PreferredBrand}";

            project.PreferredBrand = session.PreferredBrand;
            project.PreferredWeightKg = session.PreferredWeightKg;
            project.SkillLevel = session.SkillLevel;
            project.BudgetMax = session.BudgetMax;
            project.Style = session.PreferredStyle;
            project.CustomerId = session.CustomerId;
            project.PreferredCategoriesJson = session.SearchTypeHints.Count > 0
                ? JsonSerializer.Serialize(session.SearchTypeHints, JsonOptions)
                : null;
            project.LengthM = session.WallLengthM;
            project.HeightM = session.WallHeightM;
            project.SurfaceM2 = session.WallAreaM2;
        }

        private static string MapProjectType(string? domainId) => domainId switch
        {
            "wall_construction" => "Wall",
            "painting" => "Painting",
            "tiling" => "Tiling",
            "plumbing" => "Plumbing",
            "electrical" => "Electrical",
            "garden_maintenance" or "garden_cleaning" or "garden_landscaping" => "Garden",
            _ => "Other"
        };

        private static void EnsureWallChecklist(SalesProject project)
        {
            if (!string.Equals(project.ProjectType, "Wall", StringComparison.OrdinalIgnoreCase))
                return;

            var templates = new (string Code, string Label, int Order)[]
            {
                ("blocks", "Briques / blocs", 1),
                ("mortar", "Mortier / ciment", 2),
                ("tools", "Outils maçonnerie", 3),
            };

            foreach (var t in templates)
            {
                if (project.ChecklistItems.Any(i =>
                        i.Code.Equals(t.Code, StringComparison.OrdinalIgnoreCase)))
                    continue;

                project.ChecklistItems.Add(new SalesProjectChecklistItem
                {
                    SalesProjectId = project.Id,
                    Code = t.Code,
                    Label = t.Label,
                    Status = "Todo",
                    SortOrder = t.Order
                });
            }
        }
    }
}
