using System;
using System.Threading;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Services.SalesAssistant;
using Backup.Web.Api.Server.Services.SalesAssistant.Guides;

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
                return _turn.Ok(session, SalesLocale.T(session, "cart_item_added"), "CART_UPDATED");
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

            if (intent.Equals("ProjectNextStep", StringComparison.OrdinalIgnoreCase)
                || intent.Equals("WallNextStep", StringComparison.OrdinalIgnoreCase))
            {
                // Mission SKU (ampoule…) : pas de parcours — « Suivant » = autres refs du même SKU.
                if (session.SuppressProjectGuide || SalesMission.IsSimpleSku(session))
                {
                    var skuSeed = session.PendingRefineSeed
                                  ?? SalesMission.EnrichSearchText(session, "ampoule led");
                    return await HandleProductSearchTurnAsync(
                        session,
                        skuSeed,
                        new GuidedSalesSlots { Intent = GuidedSalesIntent.MoreProducts },
                        ct);
                }

                if (!ProjectGuides.TryGet(session, out var guide) || guide is null)
                {
                    // Pas de guide pour le domaine → même comportement que « Suivant » générique.
                    var fallback = new GuidedSalesSlots { Intent = GuidedSalesIntent.MoreProducts };
                    return await _guidedTurns.TryHandleAsync(
                        session,
                        string.IsNullOrWhiteSpace(request.Text) ? "autres produits" : request.Text!,
                        fallback,
                        ct);
                }

                // Seed court / texte UI réel — éviter « étape suivante » (faux mismatch NL↔FR).
                var nextSeed = string.IsNullOrWhiteSpace(request.Text) ? "next step" : request.Text!;

                if (guide.IsComplete(session))
                    return BuildCartReviewIntentResponse(session, nextSeed);

                return await HandleProductSearchTurnAsync(
                    session,
                    nextSeed,
                    new GuidedSalesSlots(),
                    ct);
            }

            if (intent.Equals("MoreProducts", StringComparison.OrdinalIgnoreCase))
            {
                if (session.SuppressProjectGuide || SalesMission.IsSimpleSku(session))
                {
                    var skuSeed = session.PendingRefineSeed
                                  ?? SalesMission.EnrichSearchText(session, "ampoule led");
                    return await HandleProductSearchTurnAsync(
                        session,
                        skuSeed,
                        new GuidedSalesSlots { Intent = GuidedSalesIntent.MoreProducts },
                        ct);
                }

                // Parcours guidé actif → « Suivant » = étape suivante (pas le seed jardin).
                if (ProjectGuides.TryGet(session, out var activeGuide) && activeGuide is not null)
                {
                    var nextSeed = string.IsNullOrWhiteSpace(request.Text) ? "next step" : request.Text!;
                    if (activeGuide.IsComplete(session))
                        return BuildCartReviewIntentResponse(session, nextSeed);

                    return await HandleProductSearchTurnAsync(
                        session,
                        nextSeed,
                        new GuidedSalesSlots(),
                        ct);
                }

                var guided = new GuidedSalesSlots { Intent = GuidedSalesIntent.MoreProducts };
                var handled = await _guidedTurns.TryHandleAsync(
                    session,
                    string.IsNullOrWhiteSpace(request.Text) ? "autres produits" : request.Text!,
                    guided,
                    ct);
                return handled;
            }

            return null;
        }

        private StoreChatResponseDto BuildCartReviewIntentResponse(StoreChatSession session, string userText)
        {
            var reply = _recommendations.BuildCartReviewReply(session);
            if (ProjectGuides.TryGet(session, out var guide) && guide is not null)
            {
                var focus = guide.ResolveNext(session, userText);
                reply = guide.BuildChecklist(session, focus) + "\n\n" + reply;
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
