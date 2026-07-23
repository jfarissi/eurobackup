using System.Threading;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Services.StoreChat;

namespace Backup.Web.Api.Server.Services.SalesAssistant.Turns
{
    public interface ISalesGuidedTurnHandler
    {
        GuidedSalesIntent Intent { get; }

        /// <summary>Return null to fall through (e.g. WallSchema parse failed).</summary>
        Task<StoreChatResponseDto?> HandleAsync(SalesGuidedTurnContext ctx, CancellationToken ct = default);
    }
}
