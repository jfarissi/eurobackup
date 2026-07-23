using System;
using System.Collections.Generic;
using Backup.Web.Api.Server.Services.SalesAssistant;

namespace Backup.Web.Api.Server.Services.StoreChat
{
    public class StoreChatSession
    {
        public string SessionId { get; set; } = Guid.NewGuid().ToString("N");
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>Contexte projet unique (source de vérité).</summary>
        public SalesProjectContext Project { get; set; } = new();

        public List<StoreChatHistoryMessage> History { get; set; } = new();
        public List<StoreChatCartItem> Cart { get; set; } = new();
        public Guid? LastOrderId { get; set; }
        /// <summary>Dernier ActionType renvoyé (CART_ADVICE, SEARCH…).</summary>
        public string? LastActionType { get; set; }
        /// <summary>Origine front pour Stripe success/cancel (ex. http://localhost:4200).</summary>
        public string? ReturnBaseUrl { get; set; }
        /// <summary>Étape parcours vendeur (guard C#).</summary>
        public SalesWorkflowState WorkflowState { get; set; } = SalesWorkflowState.Idle;

        // ── Compat : délègue vers Project (évite de tout réécrire d'un coup) ──

        public string? ActiveProjectDomainId
        {
            get => Project.DomainId;
            set => Project.DomainId = value;
        }

        public string? ActiveProjectDomainLabel
        {
            get => Project.DomainLabel;
            set => Project.DomainLabel = value;
        }

        public Guid? ActiveSalesProjectId
        {
            get => Project.SalesProjectId;
            set => Project.SalesProjectId = value;
        }

        public string? SkillLevel
        {
            get => Project.SkillLevel;
            set => Project.SkillLevel = value;
        }

        public decimal? BudgetMax
        {
            get => Project.BudgetMax;
            set => Project.BudgetMax = value;
        }

        public string? PreferredStyle
        {
            get => Project.PreferredStyle;
            set => Project.PreferredStyle = value;
        }

        public string? CustomerId
        {
            get => Project.CustomerId;
            set => Project.CustomerId = value;
        }

        public string? ProjectTypeHint
        {
            get => Project.ProjectTypeHint;
            set => Project.ProjectTypeHint = value;
        }

        public bool AdvisorMode
        {
            get => Project.AdvisorMode;
            set => Project.AdvisorMode = value;
        }

        public List<StoreChatProductSuggestionDto> LastSuggestedProducts
        {
            get => Project.LastSuggestedProducts;
            set => Project.LastSuggestedProducts = value ?? new();
        }

        public List<string> MaterialHints
        {
            get => Project.MaterialHints;
            set => Project.MaterialHints = value ?? new();
        }

        public string? PreferredBrand
        {
            get => Project.PreferredBrand;
            set => Project.PreferredBrand = value;
        }

        public List<string> SearchTypeHints
        {
            get => Project.SearchTypeHints;
            set => Project.SearchTypeHints = value ?? new();
        }

        public decimal? PreferredWeightKg
        {
            get => Project.PreferredWeightKg;
            set => Project.PreferredWeightKg = value;
        }

        public decimal? WallLengthM
        {
            get => Project.WallLengthM;
            set => Project.WallLengthM = value;
        }

        public decimal? WallHeightM
        {
            get => Project.WallHeightM;
            set => Project.WallHeightM = value;
        }

        public decimal? WallAreaM2 => Project.WallAreaM2;

        public List<string> PendingComplementHints
        {
            get => Project.PendingComplementHints;
            set => Project.PendingComplementHints = value ?? new();
        }

        public bool AwaitingComplementConfirm
        {
            get => Project.AwaitingComplementConfirm;
            set => Project.AwaitingComplementConfirm = value;
        }
    }

    public class StoreChatHistoryMessage
    {
        public string Role { get; set; } = "user";
        public string Content { get; set; } = string.Empty;
    }
}
