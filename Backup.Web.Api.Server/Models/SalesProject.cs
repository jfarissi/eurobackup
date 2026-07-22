using System;
using System.Collections.Generic;

namespace Backup.Web.Api.Server.Models
{
    /// <summary>Chantier / projet commercial suivi par l'assistant vendeur.</summary>
    public class SalesProject
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string SessionId { get; set; } = string.Empty;
        public string Status { get; set; } = "Active";
        public string ProjectType { get; set; } = "Other";
        public string? Title { get; set; }
        public decimal? SurfaceM2 { get; set; }
        public decimal? LengthM { get; set; }
        public decimal? HeightM { get; set; }
        public decimal? BudgetMax { get; set; }
        public string? PreferredBrand { get; set; }
        public string? PreferredCategoriesJson { get; set; }
        public decimal? PreferredWeightKg { get; set; }
        public string? SkillLevel { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<SalesProjectChecklistItem> ChecklistItems { get; set; } = new List<SalesProjectChecklistItem>();
    }

    public class SalesProjectChecklistItem
    {
        public int Id { get; set; }
        public Guid SalesProjectId { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Status { get; set; } = "Todo";
        public int SortOrder { get; set; }

        public SalesProject SalesProject { get; set; } = null!;
    }
}
