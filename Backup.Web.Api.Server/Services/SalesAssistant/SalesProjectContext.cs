using System;
using System.Collections.Generic;
using System.Linq;
using Backup.Web.Api.Server.Services.StoreChat;

namespace Backup.Web.Api.Server.Services.SalesAssistant
{
    /// <summary>
    /// Contexte projet unique (évite MaterialHints / mur / compléments dispersés).
    /// Les propriétés de StoreChatSession délèguent ici pour compatibilité.
    /// </summary>
    public sealed class SalesProjectContext
    {
        public string? DomainId { get; set; }
        public string? DomainLabel { get; set; }
        public Guid? SalesProjectId { get; set; }
        /// <summary>Beginner | Diy | Pro</summary>
        public string? SkillLevel { get; set; }
        public decimal? BudgetMax { get; set; }
        public string? PreferredStyle { get; set; }
        public string? CustomerId { get; set; }
        public string? ProjectTypeHint { get; set; }
        public bool AdvisorMode { get; set; }

        public string? PreferredBrand { get; set; }
        public List<string> SearchTypeHints { get; set; } = new();
        public decimal? PreferredWeightKg { get; set; }
        public List<string> MaterialHints { get; set; } = new();

        public decimal? WallLengthM { get; set; }
        public decimal? WallHeightM { get; set; }
        public decimal? WallAreaM2 =>
            WallLengthM is > 0 && WallHeightM is > 0
                ? Math.Round(WallLengthM.Value * WallHeightM.Value, 2)
                : null;

        public List<StoreChatProductSuggestionDto> LastSuggestedProducts { get; set; } = new();
        public List<string> PendingComplementHints { get; set; } = new();
        public bool AwaitingComplementConfirm { get; set; }

        /// <summary>Structure (briques/blocs) + liant détectés via hints projet.</summary>
        public bool HasStructureHint =>
            MaterialHints.Any(h => IsStructureHint(h));

        public bool HasBinderHint =>
            MaterialHints.Any(h => IsBinderHint(h));

        /// <summary>Hints structure + liant (panier → utiliser SalesComplementRules.IsBaseComplete).</summary>
        public bool IsBaseComplete => HasStructureHint && HasBinderHint;

        public void Reset()
        {
            DomainId = null;
            DomainLabel = null;
            SalesProjectId = null;
            SkillLevel = null;
            BudgetMax = null;
            PreferredStyle = null;
            CustomerId = null;
            ProjectTypeHint = null;
            AdvisorMode = false;
            PreferredBrand = null;
            SearchTypeHints.Clear();
            PreferredWeightKg = null;
            MaterialHints.Clear();
            WallLengthM = null;
            WallHeightM = null;
            LastSuggestedProducts.Clear();
            PendingComplementHints.Clear();
            AwaitingComplementConfirm = false;
        }

        public string SummaryLine()
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(DomainLabel))
                parts.Add(DomainLabel!);
            if (WallAreaM2 is > 0)
                parts.Add($"~{WallAreaM2:0.##} m²");
            if (!string.IsNullOrWhiteSpace(PreferredBrand))
                parts.Add(PreferredBrand!);
            if (BudgetMax is > 0)
                parts.Add($"budget ≤ {BudgetMax:N0} €");
            if (!string.IsNullOrWhiteSpace(SkillLevel))
                parts.Add(SkillLevel!);
            return parts.Count == 0 ? "Projet non défini" : string.Join(" · ", parts);
        }

        private static bool IsStructureHint(string h) =>
            ContainsAny(h, "brique", "bloc", "parpaing", "baksteen", "steen", "silka", "porotherm", "snelbouw");

        private static bool IsBinderHint(string h) =>
            ContainsAny(h, "ciment", "cement", "mortier", "mortel", "lijm");

        private static bool ContainsAny(string hay, params string[] needles) =>
            needles.Any(n => hay.Contains(n, StringComparison.OrdinalIgnoreCase));
    }
}
