using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Services.SalesAssistant;
using Backup.Web.Api.Server.Services.SalesAssistant.Guides;
using Backup.Web.Api.Server.Services.SalesAssistant.Turns;

namespace Backup.Web.Api.Server.Services.StoreChat
{
    public interface IStoreChatService
    {
        Task<StoreChatResponseDto> ProcessMessageAsync(StoreChatMessageRequest request, CancellationToken ct = default);
        Task<StoreChatPaymentResultDto?> GetPaymentResultAsync(Guid orderId, CancellationToken ct = default);
        Task<StoreChatPaymentResultDto?> ConfirmPaymentAsync(Guid orderId, string? stripeSessionId, CancellationToken ct = default);
    }

    public partial class StoreChatService : IStoreChatService
    {
        private readonly IStoreChatSessionStore _sessions;
        private readonly ISalesReplyComposer _replyComposer;
        private readonly ISalesDeterministicReply _deterministicReply;
        private readonly ISalesGuidedIntentDetector _guidedIntent;
        private readonly ISalesRecommendationEngine _recommendations;
        private readonly ISalesConfidenceEngine _confidence;
        private readonly ISalesProjectResumeService _resume;
        private readonly ISalesPhotoClassifier _photoClassifier;
        private readonly ISalesWorkflowGuard _workflow;
        private readonly ISalesTurnResponder _turn;
        private readonly ISalesGuidedTurnDispatcher _guidedTurns;
        private readonly ISalesContextDetector _context;
        private readonly ISalesCatalogSearchTool _catalogSearch;
        private readonly ISalesCommerceTool _commerce;
        private readonly ISalesLlmIntentRouter _llmRouter;
        private readonly StoreChatOptions _options;

        public StoreChatService(
            IStoreChatSessionStore sessions,
            ISalesReplyComposer replyComposer,
            ISalesDeterministicReply deterministicReply,
            ISalesGuidedIntentDetector guidedIntent,
            ISalesRecommendationEngine recommendations,
            ISalesConfidenceEngine confidence,
            ISalesProjectResumeService resume,
            ISalesPhotoClassifier photoClassifier,
            ISalesWorkflowGuard workflow,
            ISalesTurnResponder turn,
            ISalesGuidedTurnDispatcher guidedTurns,
            ISalesContextDetector context,
            ISalesCatalogSearchTool catalogSearch,
            ISalesCommerceTool commerce,
            ISalesLlmIntentRouter llmRouter,
            Microsoft.Extensions.Options.IOptions<StoreChatOptions> options)
        {
            _sessions = sessions;
            _replyComposer = replyComposer;
            _deterministicReply = deterministicReply;
            _guidedIntent = guidedIntent;
            _recommendations = recommendations;
            _confidence = confidence;
            _resume = resume;
            _photoClassifier = photoClassifier;
            _workflow = workflow;
            _turn = turn;
            _guidedTurns = guidedTurns;
            _context = context;
            _catalogSearch = catalogSearch;
            _commerce = commerce;
            _llmRouter = llmRouter;
            _options = options.Value ?? new StoreChatOptions();
        }

        public async Task<StoreChatResponseDto> ProcessMessageAsync(StoreChatMessageRequest request, CancellationToken ct = default)
        {
            var session = _sessions.GetOrCreate(request.SessionId);
            var intent = (request.ClientIntent ?? string.Empty).Trim();
            _workflow.EnsureConsistent(session);

            if (!string.IsNullOrWhiteSpace(request.Language))
                session.PreferredLanguage = request.Language;

            var clientReturn = SalesTextGuards.ResolveClientReturnBaseUrl(request.ReturnBaseUrl);
            if (clientReturn != null)
                session.ReturnBaseUrl = clientReturn;

            var clientIntentResponse = await TryHandleClientIntentAsync(session, request, intent, ct);
            if (clientIntentResponse != null)
                return clientIntentResponse;

            var text = (request.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(text) && string.IsNullOrWhiteSpace(request.ImageCaption) && string.IsNullOrWhiteSpace(request.ImageBase64))
                return _turn.Ok(session, SalesLocale.T(session, "empty_message"), "NONE");

            // P4 photo
            if (!string.IsNullOrWhiteSpace(request.ImageBase64) || !string.IsNullOrWhiteSpace(request.ImageCaption))
            {
                var photo = _photoClassifier.Classify(request.ImageCaption ?? text, request.ImageFileName);
                if (!string.IsNullOrWhiteSpace(photo.DomainId))
                {
                    session.ActiveProjectDomainId = photo.DomainId;
                    session.ActiveProjectDomainLabel = photo.DomainLabel;
                    session.ProjectTypeHint = photo.ProjectHint;
                }

                var photoReply = photo.Summary;
                if (!string.IsNullOrWhiteSpace(text) && text.Length > 3)
                    photoReply += "\n\n" + "Légende prise en compte : " + text;

                return _turn.Finish(session, text.Length > 0 ? text : "(photo)", photoReply, "PHOTO", null,
                    new GuidedSalesSlots { Intent = GuidedSalesIntent.None });
            }

            if (string.IsNullOrWhiteSpace(text))
                return _turn.Ok(session, SalesLocale.T(session, "empty_message"), "NONE");

            var guided = _guidedIntent.Detect(text, session);
            _confidence.DetectStyle(text, session);

            if (guided.Intent == GuidedSalesIntent.None && _options.EnableLlmIntentRouter)
            {
                var routed = await TryHandleLlmRouterAsync(session, text, guided, ct);
                if (routed != null)
                    return routed;
            }

            if (guided.Intent == GuidedSalesIntent.ResumeProject)
            {
                var (ok, resumeReply, project) = await _resume.TryResumeAsync(text, session, ct);
                if (ok)
                {
                    var res = _turn.Finish(session, text, resumeReply, "RESUME_PROJECT", null, guided);
                    if (project != null)
                    {
                        res.SalesProjectId = project.Id;
                        res.SalesProjectTitle = project.Title;
                    }

                    return res;
                }
            }

            await EnrichProjectContextAsync(session, text, ct);

            var guidedResponse = await _guidedTurns.TryHandleAsync(session, text, guided, ct);
            if (guidedResponse != null)
                return guidedResponse;

            return await HandleProductSearchTurnAsync(session, text, guided, ct);
        }

        /// <summary>
        /// Routeur LLM (optionnel) : uniquement si détecteur déterministe = None.
        /// Retourne une réponse immédiate ou mute <paramref name="guided"/> pour le dispatcher.
        /// </summary>
        private async Task<StoreChatResponseDto?> TryHandleLlmRouterAsync(
            StoreChatSession session,
            string text,
            GuidedSalesSlots guided,
            CancellationToken ct)
        {
            var decision = await _llmRouter.TryDecideAsync(session, text, ct);
            if (decision?.ParsedAction is not { } action)
                return null;

            switch (action)
            {
                case SalesLlmRouterAction.ReviewCart:
                    return BuildCartReviewIntentResponse(session, text);

                case SalesLlmRouterAction.WallNextStep:
                    if (!ProjectGuides.HasGuide(session))
                        return null;
                    if (ProjectGuides.IsComplete(session))
                        return BuildCartReviewIntentResponse(session, text);
                    // Texte utilisateur réel (sinon seed neutre) — évite faux lang_mismatch FR.
                    var nextSeed = string.IsNullOrWhiteSpace(text) ? "next step" : text;
                    return await HandleProductSearchTurnAsync(session, nextSeed, guided, ct);

                case SalesLlmRouterAction.SuggestComplements:
                    guided.Intent = GuidedSalesIntent.CartComplements;
                    return null;

                case SalesLlmRouterAction.CreateQuote:
                    return _turn.Ok(session,
                        "Pour un devis chiffré, utilisez le bouton « Demander un devis » (totaux ERP, pas le chat).",
                        "TIPS");

                case SalesLlmRouterAction.AskClarification:
                    var hint = string.IsNullOrWhiteSpace(decision.Reason)
                        ? "Pouvez-vous préciser le produit, la marque ou le projet ?"
                        : decision.Reason!;
                    return _turn.Ok(session, hint, "NONE");

                case SalesLlmRouterAction.SearchProducts:
                default:
                    return null;
            }
        }

        public Task<StoreChatPaymentResultDto?> GetPaymentResultAsync(Guid orderId, CancellationToken ct = default) =>
            _commerce.GetPaymentResultAsync(orderId, ct);

        public Task<StoreChatPaymentResultDto?> ConfirmPaymentAsync(
            Guid orderId,
            string? stripeSessionId,
            CancellationToken ct = default) =>
            _commerce.ConfirmPaymentAsync(orderId, stripeSessionId, ct);

        private async Task<StoreChatResponseDto> ResetToNewProjectAsync(
            StoreChatSession session,
            CancellationToken ct)
        {
            var keepSessionId = session.SessionId;
            var keepLanguage = session.PreferredLanguage;
            var keepReturnBaseUrl = session.ReturnBaseUrl;
            _sessions.Reset(keepSessionId);
            session = _sessions.GetOrCreate(keepSessionId);
            session.Project.Reset();
            session.PreferredLanguage = keepLanguage;
            if (!string.IsNullOrWhiteSpace(keepReturnBaseUrl))
                session.ReturnBaseUrl = keepReturnBaseUrl;
            _workflow.ApplyTransition(session, WorkflowActions.Reset);
            _sessions.Save(session);
            await Task.CompletedTask;
            return _turn.Ok(session, SalesLocale.T(session, "new_project"), "NONE");
        }

        private Task<List<StoreChatProductSuggestionDto>> SearchProductsAsync(
            string text,
            StoreChatSession session,
            ProductSearchFilter meta,
            CancellationToken ct,
            HashSet<string>? excludeProductIds = null) =>
            _catalogSearch.SearchAsync(text, session, meta, ct, excludeProductIds);
    }
}
