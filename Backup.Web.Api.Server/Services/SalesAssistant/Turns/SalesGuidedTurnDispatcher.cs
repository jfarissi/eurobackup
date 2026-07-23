using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Services.StoreChat;

namespace Backup.Web.Api.Server.Services.SalesAssistant.Turns
{
    public interface ISalesGuidedTurnDispatcher
    {
        Task<StoreChatResponseDto?> TryHandleAsync(
            StoreChatSession session,
            string text,
            GuidedSalesSlots guided,
            CancellationToken ct = default);
    }

    public sealed class SalesGuidedTurnDispatcher : ISalesGuidedTurnDispatcher
    {
        private readonly IEnumerable<ISalesGuidedTurnHandler> _handlers;
        private readonly ISalesTurnResponder _turn;

        public SalesGuidedTurnDispatcher(
            IEnumerable<ISalesGuidedTurnHandler> handlers,
            ISalesTurnResponder turn)
        {
            _handlers = handlers;
            _turn = turn;
        }

        public async Task<StoreChatResponseDto?> TryHandleAsync(
            StoreChatSession session,
            string text,
            GuidedSalesSlots guided,
            CancellationToken ct = default)
        {
            if (guided.Intent != GuidedSalesIntent.None)
            {
                var handler = _handlers.FirstOrDefault(x => x.Intent == guided.Intent);
                if (handler != null)
                {
                    var response = await handler.HandleAsync(new SalesGuidedTurnContext
                    {
                        Session = session,
                        Text = text,
                        Guided = guided
                    }, ct);
                    if (response != null)
                        return response;
                }
            }

            if (SalesTextGuards.IsBareConfirmation(text)
                && (session.AwaitingComplementConfirm || session.PendingComplementHints.Count > 0
                    || string.Equals(session.LastActionType, "CART_ADVICE", StringComparison.OrdinalIgnoreCase)))
            {
                return _turn.Finish(session, text,
                    "Je cherche les compléments… Réessayez « ok », ou un mot précis : treillis, truelle, auge, gants.",
                    "NONE", null, guided);
            }

            return null;
        }
    }
}
