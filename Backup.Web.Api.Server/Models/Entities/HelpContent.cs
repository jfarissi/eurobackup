using System;
using Backup.Web.Api.Server.Services.Audit;

namespace Backup.Web.Api.Server.Models.Entities
{
    /// <summary>Contenu d'aide métier versionné (RG-AC1–8).</summary>
    public class HelpContent : IHasAuditTrail
    {
        public int Id { get; set; }
        /// <summary>Clé stable ex. sales.order, field.sales.customer</summary>
        public string HelpKey { get; set; } = string.Empty;
        /// <summary>fr | nl | en</summary>
        public string Lang { get; set; } = "fr";
        public string Title { get; set; } = string.Empty;
        public string? N1 { get; set; }
        public string? Body { get; set; }
        public string? Rules { get; set; }
        public string? Example { get; set; }
        public string? Guide { get; set; }
        public string Version { get; set; } = "v1.0.0";
        /// <summary>Draft | InReview | ValidatedBusiness | ValidatedLegal | Published | Archived</summary>
        public string Status { get; set; } = "Draft";
        public DateTime? ValidFrom { get; set; }
        public DateTime? ValidTo { get; set; }
        /// <summary>IDs RG liés, séparés par virgule (ex. RG-CC1,RG-CC2)</summary>
        public string? RgIds { get; set; }
        public string? DocumentType { get; set; }
        public string? FieldId { get; set; }
        public string? UpdatedBy { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public string? CreatedBy { get; set; }
    }

    public class HelpFeedbackEvent
    {
        public long Id { get; set; }
        public string HelpKey { get; set; } = string.Empty;
        /// <summary>up | down</summary>
        public string Vote { get; set; } = "up";
        public string? Comment { get; set; }
        /// <summary>too_short | wrong_example | obsolete | bad_translation | other</summary>
        public string? Reason { get; set; }
        public string? UserId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class HelpAnalyticsEvent
    {
        public long Id { get; set; }
        public string HelpKey { get; set; } = string.Empty;
        /// <summary>open | search | guide | field | center</summary>
        public string Action { get; set; } = "open";
        public string? UserId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
