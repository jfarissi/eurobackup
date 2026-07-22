using System;
using System.Collections.Generic;

namespace Backup.Web.Api.Server.Services.StoreChat
{
    public class StoreChatSession
    {
        public string SessionId { get; set; } = Guid.NewGuid().ToString("N");
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public string? ActiveProjectDomainId { get; set; }
        public string? ActiveProjectDomainLabel { get; set; }
        /// <summary>Projet commercial persisté (SalesProjects).</summary>
        public Guid? ActiveSalesProjectId { get; set; }
        /// <summary>Beginner | Diy | Pro</summary>
        public string? SkillLevel { get; set; }
        public decimal? BudgetMax { get; set; }
        public string? PreferredStyle { get; set; }
        public string? CustomerId { get; set; }
        public string? ProjectTypeHint { get; set; }
        public bool AdvisorMode { get; set; }
        /// <summary>Dernière liste produits (compare / why).</summary>
        public List<StoreChatProductSuggestionDto> LastSuggestedProducts { get; set; } = new();
        /// <summary>Mots-clés matériaux accumulés sur le projet (brique, mortier…).</summary>
        public List<string> MaterialHints { get; set; } = new();
        /// <summary>Marque demandée (ex. Knauf) — prioritaire sur le domaine projet.</summary>
        public string? PreferredBrand { get; set; }
        /// <summary>Types matériaux sticky (ciment, peinture…) pour les tours suivants.</summary>
        public List<string> SearchTypeHints { get; set; } = new();
        /// <summary>Poids demandé sticky (ex. 25 pour 25 kg).</summary>
        public decimal? PreferredWeightKg { get; set; }
        public decimal? WallLengthM { get; set; }
        public decimal? WallHeightM { get; set; }
        public decimal? WallAreaM2 =>
            WallLengthM is > 0 && WallHeightM is > 0
                ? Math.Round(WallLengthM.Value * WallHeightM.Value, 2)
                : null;
        public List<StoreChatHistoryMessage> History { get; set; } = new();
        public List<StoreChatCartItem> Cart { get; set; } = new();
        public Guid? LastOrderId { get; set; }
    }

    public class StoreChatHistoryMessage
    {
        public string Role { get; set; } = "user";
        public string Content { get; set; } = string.Empty;
    }
}
