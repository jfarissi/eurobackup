using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Services.SalesAssistant;
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
            ISalesCommerceTool commerce)
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
        }

        public async Task<StoreChatResponseDto> ProcessMessageAsync(StoreChatMessageRequest request, CancellationToken ct = default)
        {
            var session = _sessions.GetOrCreate(request.SessionId);
            var intent = (request.ClientIntent ?? string.Empty).Trim();
            _workflow.EnsureConsistent(session);

            var clientReturn = SalesTextGuards.ResolveClientReturnBaseUrl(request.ReturnBaseUrl);
            if (clientReturn != null)
                session.ReturnBaseUrl = clientReturn;

            var clientIntentResponse = await TryHandleClientIntentAsync(session, request, intent, ct);
            if (clientIntentResponse != null)
                return clientIntentResponse;

            var text = (request.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(text) && string.IsNullOrWhiteSpace(request.ImageCaption) && string.IsNullOrWhiteSpace(request.ImageBase64))
                return _turn.Ok(session, "Message vide.", "NONE");

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
                return _turn.Ok(session, "Message vide.", "NONE");

            var guided = _guidedIntent.Detect(text, session);
            _confidence.DetectStyle(text, session);

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
            _sessions.Reset(keepSessionId);
            session = _sessions.GetOrCreate(keepSessionId);
            session.Project.Reset();
            _workflow.ApplyTransition(session, WorkflowActions.Reset);
            _sessions.Save(session);
            await Task.CompletedTask;
            return _turn.Ok(session, "Nouveau projet démarré. Comment puis-je vous aider ?", "NONE");
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
