using System;
using System.Threading;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Services.SalesAssistant;

namespace Backup.Web.Api.Server.Services.StoreChat
{
    public partial class StoreChatService
    {
        private async Task<StoreChatResponseDto?> TryHandleClientIntentAsync(
            StoreChatSession session,
            StoreChatMessageRequest request,
            string intent,
            CancellationToken ct)
        {
            if (intent.Equals("NewProject", StringComparison.OrdinalIgnoreCase)
                || SalesTextGuards.IsNewProjectText(request.Text))
            {
                return await ResetToNewProjectAsync(session, ct);
            }

            if (intent.Equals("AddToCartFromList", StringComparison.OrdinalIgnoreCase))
            {
                if (!_workflow.CanExecute(WorkflowActions.AddToCart, session.WorkflowState))
                    return _turn.DenyWorkflow(session, WorkflowActions.AddToCart);

                await _commerce.AddToCartAsync(session, request.TargetProductId, request.TargetQuantity ?? 1, ct);
                _workflow.ApplyTransition(session, WorkflowActions.AddToCart);
                _sessions.Save(session);
                return _turn.Ok(session, "Produit ajouté au panier.", "CART_UPDATED");
            }

            if (intent.Equals("RemoveFromCartFromList", StringComparison.OrdinalIgnoreCase))
            {
                if (!_workflow.CanExecute(WorkflowActions.RemoveFromCart, session.WorkflowState)
                    && session.Cart.Count == 0)
                    return _turn.DenyWorkflow(session, WorkflowActions.RemoveFromCart);

                _commerce.RemoveFromCart(session, request.TargetProductId);
                _workflow.ApplyTransition(session, WorkflowActions.RemoveFromCart);
                _sessions.Save(session);
                return _turn.Ok(session, "Produit retiré du panier.", "CART_UPDATED");
            }

            if (intent.Equals("CreateQuoteFromTableSelection", StringComparison.OrdinalIgnoreCase))
            {
                await _commerce.ReplaceCartFromTableAsync(session, request.TableCartLines, ct);
                _workflow.EnsureConsistent(session);
                if (!_workflow.CanExecute(WorkflowActions.CreateQuote, session.WorkflowState)
                    || session.Cart.Count == 0)
                    return _turn.DenyWorkflow(session, WorkflowActions.CreateQuote);
                return await _commerce.CreateQuoteAsync(session, ct);
            }

            if (intent.Equals("CreateOrderFromTableSelection", StringComparison.OrdinalIgnoreCase))
            {
                await _commerce.ReplaceCartFromTableAsync(session, request.TableCartLines, ct);
                _workflow.EnsureConsistent(session);
                if (!_workflow.CanExecute(WorkflowActions.CreateOrder, session.WorkflowState)
                    || session.Cart.Count == 0)
                    return _turn.DenyWorkflow(session, WorkflowActions.CreateOrder);
                return await _commerce.CreateOrderAsync(session, ct);
            }

            // CTA UI explicites — progression / revue sans dépendre du phrasé libre.
            if (intent.Equals("ReviewCart", StringComparison.OrdinalIgnoreCase))
                return BuildCartReviewIntentResponse(session, request.Text ?? "review cart");

            if (intent.Equals("WallNextStep", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.Equals(session.ActiveProjectDomainId, "wall_construction", StringComparison.OrdinalIgnoreCase))
                {
                    return _turn.Ok(session,
                        "Aucun parcours mur actif. Décrivez d’abord votre mur (ex. 7 m × 2 m).",
                        "NONE");
                }

                if (SalesProjectGuide.IsWallGuideComplete(session))
                    return BuildCartReviewIntentResponse(session, request.Text ?? "étape suivante");

                return await HandleProductSearchTurnAsync(
                    session,
                    "étape suivante",
                    new GuidedSalesSlots(),
                    ct);
            }

            return null;
        }

        private StoreChatResponseDto BuildCartReviewIntentResponse(StoreChatSession session, string userText)
        {
            var reply = _recommendations.BuildCartReviewReply(session);
            if (string.Equals(session.ActiveProjectDomainId, "wall_construction", StringComparison.OrdinalIgnoreCase))
            {
                var family = SalesProjectGuide.ResolveWallFamily(session, userText);
                var checklist = SalesProjectGuide.BuildWallChecklist(session, family);
                reply = checklist + "\n\n" + reply;
            }

            _sessions.Save(session);
            return _turn.Finish(
                session,
                userText,
                reply,
                "TIPS",
                null,
                new GuidedSalesSlots { Intent = GuidedSalesIntent.Tips });
        }
    }
}
