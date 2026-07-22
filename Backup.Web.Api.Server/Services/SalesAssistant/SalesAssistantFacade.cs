using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Services.StoreChat;
using Microsoft.Extensions.Logging;

namespace Backup.Web.Api.Server.Services.SalesAssistant
{
    public interface ISalesAssistantFacade
    {
        Task<StoreChatResponseDto> ProcessMessageAsync(StoreChatMessageRequest request, CancellationToken ct = default);
        Task<StoreChatPaymentResultDto?> GetPaymentResultAsync(Guid orderId, CancellationToken ct = default);
        Task<StoreChatPaymentResultDto?> ConfirmPaymentAsync(Guid orderId, string? stripeSessionId, CancellationToken ct = default);
    }

    /// <summary>
    /// Entrée unique assistant vendeur. Commerce délègue à StoreChat ;
    /// le filtre + projet sont enrichis après chaque message.
    /// </summary>
    public class SalesAssistantFacade : ISalesAssistantFacade
    {
        private readonly IStoreChatService _storeChat;
        private readonly ISalesProjectService _projects;
        private readonly IStoreChatSessionStore _sessions;
        private readonly ILogger<SalesAssistantFacade> _logger;

        public SalesAssistantFacade(
            IStoreChatService storeChat,
            ISalesProjectService projects,
            IStoreChatSessionStore sessions,
            ILogger<SalesAssistantFacade> logger)
        {
            _storeChat = storeChat;
            _projects = projects;
            _sessions = sessions;
            _logger = logger;
        }

        public async Task<StoreChatResponseDto> ProcessMessageAsync(
            StoreChatMessageRequest request,
            CancellationToken ct = default)
        {
            var intent = (request.ClientIntent ?? string.Empty).Trim();
            if (intent.Equals("NewProject", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(request.SessionId))
            {
                var existing = _sessions.Get(request.SessionId!);
                if (existing != null)
                    await _projects.ClearSessionProjectAsync(existing, ct);
            }

            var response = await _storeChat.ProcessMessageAsync(request, ct);

            var session = _sessions.Get(response.SessionId);
            if (session == null)
                return response;

            if (intent.Equals("NewProject", StringComparison.OrdinalIgnoreCase))
                return response;

            // Commerce intents : pas de sync projet obligatoire
            if (IsCommerceIntent(intent))
            {
                AttachProjectFields(response, session);
                return response;
            }

            var project = await _projects.SyncFromSessionAsync(session, ct);
            _sessions.Save(session);

            response.SalesProjectId = project?.Id ?? session.ActiveSalesProjectId;
            response.SalesProjectTitle = project?.Title
                ?? session.ActiveProjectDomainLabel
                ?? response.ActiveProjectDomainLabel;

            if (response.SearchFilter == null && HasFilterSignal(session))
            {
                response.SearchFilter = new ProductSearchFilter
                {
                    Brand = session.PreferredBrand,
                    Categories = session.SearchTypeHints.ToList(),
                    WeightKg = session.PreferredWeightKg,
                    Intent = response.ActionType
                };
            }

            LogAudit(response);
            return response;
        }

        public Task<StoreChatPaymentResultDto?> GetPaymentResultAsync(Guid orderId, CancellationToken ct = default) =>
            _storeChat.GetPaymentResultAsync(orderId, ct);

        public Task<StoreChatPaymentResultDto?> ConfirmPaymentAsync(
            Guid orderId,
            string? stripeSessionId,
            CancellationToken ct = default) =>
            _storeChat.ConfirmPaymentAsync(orderId, stripeSessionId, ct);

        private static bool IsCommerceIntent(string intent) =>
            intent.Equals("AddToCartFromList", StringComparison.OrdinalIgnoreCase)
            || intent.Equals("RemoveFromCartFromList", StringComparison.OrdinalIgnoreCase)
            || intent.Equals("CreateQuoteFromTableSelection", StringComparison.OrdinalIgnoreCase)
            || intent.Equals("CreateOrderFromTableSelection", StringComparison.OrdinalIgnoreCase);

        private static bool HasFilterSignal(StoreChatSession session) =>
            !string.IsNullOrWhiteSpace(session.PreferredBrand)
            || session.SearchTypeHints.Count > 0
            || session.PreferredWeightKg is > 0;

        private static void AttachProjectFields(StoreChatResponseDto response, StoreChatSession session)
        {
            response.SalesProjectId = session.ActiveSalesProjectId;
            response.SalesProjectTitle ??= session.ActiveProjectDomainLabel;
        }

        private void LogAudit(StoreChatResponseDto response)
        {
            var filter = response.SearchFilter;
            var productIds = response.Products?.Select(p => p.ProductId).ToList() ?? new();
            _logger.LogInformation(
                "SalesAssistant audit Intent={Intent} Brand={Brand} Categories={Categories} WeightKg={Weight} Outcome={Outcome} ProductIds=[{ProductIds}] ProjectId={ProjectId}",
                filter?.Intent ?? response.ActionType,
                filter?.Brand,
                filter == null ? null : string.Join(',', filter.Categories),
                filter?.WeightKg,
                filter?.Outcome.ToString(),
                string.Join(',', productIds),
                response.SalesProjectId);
        }
    }
}
