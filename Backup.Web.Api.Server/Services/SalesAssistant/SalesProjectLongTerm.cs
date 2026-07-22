using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Models;
using Backup.Web.Api.Server.Services.StoreChat;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Backup.Web.Api.Server.Services.SalesAssistant
{
    public sealed class SalesLogisticsDto
    {
        public decimal EstimatedWeightKg { get; set; }
        public string Recommendation { get; set; } = string.Empty;
        public bool SuggestDelivery { get; set; }
        public string Summary { get; set; } = string.Empty;
    }

    public interface ISalesLogisticsEngine
    {
        SalesLogisticsDto Evaluate(StoreChatSession session, decimal weightThresholdKg = 40m);
    }

    public class SalesLogisticsEngine : ISalesLogisticsEngine
    {
        public SalesLogisticsDto Evaluate(StoreChatSession session, decimal weightThresholdKg = 40m)
        {
            decimal weight = 0;
            foreach (var line in session.Cart)
            {
                var fromName = TryParseKg(line.Name) ?? 5m; // fallback moyen si inconnu
                weight += fromName * line.Quantity;
            }

            foreach (var p in session.LastSuggestedProducts)
            {
                var kg = TryParseKg(p.Name);
                if (kg is > 0 && p.SuggestedQuantity is > 0)
                    weight += kg.Value * p.SuggestedQuantity.Value * 0.25m; // estimation partielle liste
            }

            weight = Math.Round(weight, 1);
            var delivery = weight >= weightThresholdKg;
            return new SalesLogisticsDto
            {
                EstimatedWeightKg = weight,
                SuggestDelivery = delivery,
                Recommendation = delivery
                    ? "Livraison conseillée (poids / volume élevé)."
                    : "Retrait magasin suffisant pour ce volume.",
                Summary = $"Logistique estimée ≈ {weight:0.#} kg. "
                          + (delivery
                              ? $"Au-delà de {weightThresholdKg:0} kg : privilégiez la livraison."
                              : $"Sous {weightThresholdKg:0} kg : retrait magasin OK.")
            };
        }

        private static decimal? TryParseKg(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return null;
            var m = Regex.Match(name, @"(\d+(?:[.,]\d+)?)\s*kg", RegexOptions.IgnoreCase);
            if (m.Success
                && decimal.TryParse(m.Groups[1].Value.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var kg))
                return kg;
            return null;
        }
    }

    public interface ISalesPlanningEngine
    {
        string BuildPlan(StoreChatSession session);
    }

    public class SalesPlanningEngine : ISalesPlanningEngine
    {
        public string BuildPlan(StoreChatSession session)
        {
            var type = session.ActiveProjectDomainId ?? "other";
            var sb = new StringBuilder();
            sb.AppendLine("Planning chantier (indicatif) :");

            if (type == "wall_construction" || session.WallAreaM2 is > 0)
            {
                sb.AppendLine("1. Préparation support / traçage");
                sb.AppendLine("2. Pose blocs/briques + mortier");
                sb.AppendLine("3. Renforts / linteaux si ouvertures");
                sb.AppendLine("4. Séchage puis finitions (enduit / joint)");
                if (session.WallAreaM2 is > 0)
                    sb.AppendLine($"Surface : ≈ {session.WallAreaM2:0.##} m².");
            }
            else if (type == "painting")
            {
                sb.AppendLine("1. Protection sols / ruban");
                sb.AppendLine("2. Sous-couche");
                sb.AppendLine("3. 1ère couche → séchage → 2ème couche");
                sb.AppendLine("4. Retouches et nettoyage");
            }
            else if (type == "tiling")
            {
                sb.AppendLine("1. Préparation support + primaire");
                sb.AppendLine("2. Pose carrelage + colle");
                sb.AppendLine("3. Joints après séchage");
                sb.AppendLine("4. Nettoyage et silicone points humides");
            }
            else
            {
                sb.AppendLine("1. Définir besoin + budget");
                sb.AppendLine("2. Choisir produits principaux");
                sb.AppendLine("3. Compléments / outils");
                sb.AppendLine("4. Devis → commande → retrait/livraison");
            }

            return sb.ToString().Trim();
        }
    }

    public interface ISalesProjectResumeService
    {
        Task<(bool Ok, string Reply, SalesProject? Project)> TryResumeAsync(
            string text,
            StoreChatSession session,
            CancellationToken ct = default);
        Task UpsertCustomerProfileAsync(StoreChatSession session, CancellationToken ct = default);
    }

    public class SalesProjectResumeService : ISalesProjectResumeService
    {
        private readonly IStorageBroker _storage;
        private readonly ILogger<SalesProjectResumeService> _logger;

        public SalesProjectResumeService(IStorageBroker storage, ILogger<SalesProjectResumeService> logger)
        {
            _storage = storage;
            _logger = logger;
        }

        public async Task<(bool Ok, string Reply, SalesProject? Project)> TryResumeAsync(
            string text,
            StoreChatSession session,
            CancellationToken ct = default)
        {
            var lower = (text ?? string.Empty).ToLowerInvariant();
            if (!ContainsAny(lower, "reprendre projet", "reprise projet", "charger projet", "projet #", "project #", "resume project"))
                return (false, string.Empty, null);

            Guid? id = null;
            var guidMatch = Regex.Match(text ?? string.Empty,
                @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}");
            if (guidMatch.Success && Guid.TryParse(guidMatch.Value, out var g))
                id = g;

            if (id is null)
            {
                var shortMatch = Regex.Match(lower, @"projet\s*#?\s*([0-9a-f]{6,})");
                if (shortMatch.Success)
                {
                    var needle = shortMatch.Groups[1].Value;
                    var hit = await _storage.SelectAllSalesProjects()
                        .AsNoTracking()
                        .Where(p => p.Status == "Active" || p.Status == "Quoted" || p.Status == "Ordered")
                        .OrderByDescending(p => p.UpdatedAt)
                        .Take(50)
                        .ToListAsync(ct);
                    var found = hit.FirstOrDefault(p =>
                        p.Id.ToString("N").StartsWith(needle, StringComparison.OrdinalIgnoreCase)
                        || p.Id.ToString("D").StartsWith(needle, StringComparison.OrdinalIgnoreCase));
                    if (found != null)
                        id = found.Id;
                }
            }

            if (id is null)
            {
                return (true,
                    "Pour reprendre un projet, indiquez son Id (ex. « reprendre projet 3fa85f64-… »).",
                    null);
            }

            var project = await _storage.SelectSalesProjectByIdAsync(id.Value);
            if (project == null)
                return (true, $"Projet {id} introuvable.", null);

            ApplyProjectToSession(session, project);
            _logger.LogInformation("Resumed SalesProject {ProjectId} into session {SessionId}", project.Id, session.SessionId);

            var checklist = project.ChecklistItems.Count > 0
                ? string.Join(", ", project.ChecklistItems.OrderBy(i => i.SortOrder).Select(i => $"{i.Label} [{i.Status}]"))
                : "aucune";

            return (true,
                $"Projet repris : {project.Title ?? project.ProjectType} ({project.Id:D}).\n"
                + $"Type {project.ProjectType}"
                + (project.SurfaceM2 is > 0 ? $", surface {project.SurfaceM2:0.##} m²" : "")
                + (project.BudgetMax is > 0 ? $", budget {project.BudgetMax:N0} €" : "")
                + (string.IsNullOrWhiteSpace(project.PreferredBrand) ? "" : $", marque {project.PreferredBrand}")
                + $".\nChecklist : {checklist}.\nComment continuer ?",
                project);
        }

        public async Task UpsertCustomerProfileAsync(StoreChatSession session, CancellationToken ct = default)
        {
            var customerId = session.CustomerId;
            if (string.IsNullOrWhiteSpace(customerId))
                return;

            var existing = await _storage.SelectAllSalesCustomerProfiles()
                .FirstOrDefaultAsync(p => p.CustomerId == customerId, ct);

            if (existing == null)
            {
                existing = new SalesCustomerProfile
                {
                    CustomerId = customerId,
                    PreferredBrandsJson = session.PreferredBrand != null
                        ? System.Text.Json.JsonSerializer.Serialize(new[] { session.PreferredBrand })
                        : null,
                    AverageBudget = session.BudgetMax,
                    Notes = session.PreferredStyle,
                    UpdatedAt = DateTime.UtcNow
                };
                await _storage.InsertSalesCustomerProfileAsync(existing);
            }
            else
            {
                existing.AverageBudget = session.BudgetMax ?? existing.AverageBudget;
                if (!string.IsNullOrWhiteSpace(session.PreferredBrand))
                {
                    var brands = new List<string>();
                    if (!string.IsNullOrWhiteSpace(existing.PreferredBrandsJson))
                    {
                        try
                        {
                            brands = System.Text.Json.JsonSerializer.Deserialize<List<string>>(existing.PreferredBrandsJson) ?? new();
                        }
                        catch { /* ignore */ }
                    }

                    if (!brands.Contains(session.PreferredBrand, StringComparer.OrdinalIgnoreCase))
                        brands.Add(session.PreferredBrand);
                    existing.PreferredBrandsJson = System.Text.Json.JsonSerializer.Serialize(brands.Take(10));
                }

                existing.Notes = session.PreferredStyle ?? existing.Notes;
                existing.UpdatedAt = DateTime.UtcNow;
                await _storage.UpdateSalesCustomerProfileAsync(existing);
            }
        }

        private static void ApplyProjectToSession(StoreChatSession session, SalesProject project)
        {
            session.ActiveSalesProjectId = project.Id;
            session.PreferredBrand = project.PreferredBrand;
            session.PreferredWeightKg = project.PreferredWeightKg;
            session.BudgetMax = project.BudgetMax;
            session.SkillLevel = project.SkillLevel;
            session.PreferredStyle = project.Style;
            session.WallLengthM = project.LengthM;
            session.WallHeightM = project.HeightM;
            session.ProjectTypeHint = project.ProjectType;
            session.CustomerId = project.CustomerId;

            if (!string.IsNullOrWhiteSpace(project.PreferredCategoriesJson))
            {
                try
                {
                    session.SearchTypeHints = System.Text.Json.JsonSerializer.Deserialize<List<string>>(project.PreferredCategoriesJson)
                                             ?? session.SearchTypeHints;
                }
                catch { /* ignore */ }
            }

            session.ActiveProjectDomainId = project.ProjectType switch
            {
                "Wall" => "wall_construction",
                "Painting" => "painting",
                "Tiling" or "Bathroom" => "tiling",
                "Kitchen" => "tiling",
                _ => session.ActiveProjectDomainId
            };
            session.ActiveProjectDomainLabel = project.Title ?? project.ProjectType;
        }

        private static bool ContainsAny(string hay, params string[] needles) =>
            needles.Any(n => hay.Contains(n, StringComparison.OrdinalIgnoreCase));
    }
}
