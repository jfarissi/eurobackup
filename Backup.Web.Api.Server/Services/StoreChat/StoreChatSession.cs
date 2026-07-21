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
        /// <summary>Mots-clés matériaux accumulés sur le projet (brique, mortier…).</summary>
        public List<string> MaterialHints { get; set; } = new();
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
