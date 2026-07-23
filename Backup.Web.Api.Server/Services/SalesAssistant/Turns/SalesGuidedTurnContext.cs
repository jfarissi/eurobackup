using Backup.Web.Api.Server.Services.StoreChat;

namespace Backup.Web.Api.Server.Services.SalesAssistant.Turns
{
    public sealed class SalesGuidedTurnContext
    {
        public StoreChatSession Session { get; init; } = null!;
        public string Text { get; init; } = string.Empty;
        public GuidedSalesSlots Guided { get; init; } = null!;
    }
}
