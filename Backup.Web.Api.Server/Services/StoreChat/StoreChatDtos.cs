using System;
using System.Collections.Generic;

namespace Backup.Web.Api.Server.Services.StoreChat
{
    public class StoreChatMessageRequest
    {
        public string? SessionId { get; set; }
        public string? Text { get; set; }
        public string Sender { get; set; } = "user";
        public string? InteractionType { get; set; }
        public string? Language { get; set; }
        public string? ClientIntent { get; set; }
        public string? TargetProductId { get; set; }
        public decimal? TargetQuantity { get; set; }
        public List<StoreChatTableCartLineDto>? TableCartLines { get; set; }
        /// <summary>Photo optionnelle (base64) — P4.</summary>
        public string? ImageBase64 { get; set; }
        public string? ImageFileName { get; set; }
        public string? ImageCaption { get; set; }
        /// <summary>Origine du front (ex. http://localhost:4200) pour les retours Stripe.</summary>
        public string? ReturnBaseUrl { get; set; }
    }

    public class StoreChatTableCartLineDto
    {
        public string ProductId { get; set; } = string.Empty;
        public decimal Quantity { get; set; } = 1;
    }

    public class StoreChatProductSuggestionDto
    {
        public string ProductId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public decimal? Price { get; set; }
        public string? Brand { get; set; }
        public string? Category { get; set; }
        public decimal? SuggestedQuantity { get; set; }
        /// <summary>URL absolue de l'image (port 15022 + PicName).</summary>
        public string? ImageUrl { get; set; }
    }

    public class StoreChatQuotePdfDto
    {
        public string PdfBase64 { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public decimal? Total { get; set; }
        public string? Source { get; set; }
        public string? SourceLabel { get; set; }
    }

    public class StoreChatPaymentLinkDto
    {
        public string Url { get; set; } = string.Empty;
        public decimal? Amount { get; set; }
        public string? Description { get; set; }
        public string? OrderId { get; set; }
        public string? Source { get; set; }
        public string? SourceLabel { get; set; }
    }

    public class StoreChatResponseDto
    {
        public string SessionId { get; set; } = string.Empty;
        public string ReplyText { get; set; } = string.Empty;
        public bool HasAction { get; set; }
        public string? ActionType { get; set; }
        public object? ActionData { get; set; }
        public string? ActiveProjectDomainId { get; set; }
        public string? ActiveProjectDomainLabel { get; set; }
        public Guid? SalesProjectId { get; set; }
        public string? SalesProjectTitle { get; set; }
        /// <summary>Filtre métier appliqué (audit / UI).</summary>
        public SalesAssistant.ProductSearchFilter? SearchFilter { get; set; }
        public string? BudgetAlert { get; set; }
        public string? SkillLevel { get; set; }
        public decimal? BudgetMax { get; set; }
        public SalesAssistant.SalesPackDto? Pack { get; set; }
        public List<SalesAssistant.SalesCompareRowDto>? CompareRows { get; set; }
        public List<SalesAssistant.SalesRecommendationDto>? Recommendations { get; set; }
        public List<SalesAssistant.SalesPromoLineDto>? Promos { get; set; }
        public SalesAssistant.SalesSavingsDto? Savings { get; set; }
        public SalesAssistant.SalesLogisticsDto? Logistics { get; set; }
        public List<StoreChatProductSuggestionDto>? Products { get; set; }
        public StoreChatQuotePdfDto? QuotePdf { get; set; }
        public StoreChatPaymentLinkDto? PaymentLink { get; set; }
        /// <summary>Étape parcours vendeur (Idle, CartBuilding, Quoting…).</summary>
        public string? WorkflowState { get; set; }
        /// <summary>Résumé ProjectContext (domaine · m² · marque · budget…).</summary>
        public string? ProjectSummary { get; set; }
        /// <summary>Structure + liant présents (hints projet).</summary>
        public bool ProjectBaseComplete { get; set; }
    }

    public class StoreChatConfirmPaymentDto
    {
        public Guid OrderId { get; set; }
        public string? SessionId { get; set; }
    }

    public class StoreChatPaymentResultDto
    {
        public string Status { get; set; } = "pending";
        public string? OrderId { get; set; }
        public string? InvoiceNumber { get; set; }
        public StoreChatQuotePdfDto? InvoicePdf { get; set; }
        public bool SuggestNewProject { get; set; } = true;
        public string? Source { get; set; }
        public string? SourceLabel { get; set; }
    }

    public class StoreChatCartItem
    {
        public int ErpProductId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Reference { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice => Math.Round(Quantity * UnitPrice, 2);
    }
}
