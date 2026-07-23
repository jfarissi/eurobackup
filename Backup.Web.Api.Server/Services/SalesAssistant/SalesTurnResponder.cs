using System;
using System.Collections.Generic;
using System.Linq;
using Backup.Web.Api.Server.Services.StoreChat;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Backup.Web.Api.Server.Services.SalesAssistant
{
    public interface ISalesTurnResponder
    {
        StoreChatResponseDto Finish(
            StoreChatSession session,
            string userText,
            string reply,
            string actionType,
            List<StoreChatProductSuggestionDto>? products,
            GuidedSalesSlots guided);

        StoreChatResponseDto Ok(StoreChatSession session, string text, string actionType);

        StoreChatResponseDto DenyWorkflow(StoreChatSession session, string action);
    }

    public class SalesTurnResponder : ISalesTurnResponder
    {
        private readonly IStoreChatSessionStore _sessions;
        private readonly ISalesWorkflowGuard _workflow;
        private readonly StoreChatOptions _options;
        private readonly ILogger<SalesTurnResponder> _logger;

        public SalesTurnResponder(
            IStoreChatSessionStore sessions,
            ISalesWorkflowGuard workflow,
            IOptions<StoreChatOptions> options,
            ILogger<SalesTurnResponder> logger)
        {
            _sessions = sessions;
            _workflow = workflow;
            _options = options.Value ?? new StoreChatOptions();
            _logger = logger;
        }

        public StoreChatResponseDto Finish(
            StoreChatSession session,
            string userText,
            string reply,
            string actionType,
            List<StoreChatProductSuggestionDto>? products,
            GuidedSalesSlots guided)
        {
            session.History.Add(new StoreChatHistoryMessage { Role = "user", Content = userText });
            session.History.Add(new StoreChatHistoryMessage { Role = "assistant", Content = reply });
            TrimHistory(session);
            if (products is { Count: > 0 })
            {
                session.LastSuggestedProducts = products.ToList();
                _workflow.ApplyTransition(session, WorkflowActions.SearchProducts);
            }

            session.LastActionType = actionType;
            _sessions.Save(session);

            var response = new StoreChatResponseDto
            {
                SessionId = session.SessionId,
                ReplyText = reply,
                HasAction = !string.Equals(actionType, "NONE", StringComparison.OrdinalIgnoreCase),
                ActionType = actionType,
                ActionData = products,
                Products = products,
                ActiveProjectDomainId = session.ActiveProjectDomainId,
                ActiveProjectDomainLabel = session.ActiveProjectDomainLabel,
                SkillLevel = session.SkillLevel,
                BudgetMax = session.BudgetMax,
                WorkflowState = session.WorkflowState.ToString(),
                ProjectSummary = session.Project.SummaryLine(),
                ProjectBaseComplete = session.Project.IsBaseComplete
            };

            if (guided.BudgetMentioned || session.BudgetMax is > 0)
                response.BudgetMax = session.BudgetMax;
            return response;
        }

        public StoreChatResponseDto Ok(StoreChatSession session, string text, string actionType) => new()
        {
            SessionId = session.SessionId,
            ReplyText = text,
            HasAction = !string.Equals(actionType, "NONE", StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(actionType, "WORKFLOW_DENIED", StringComparison.OrdinalIgnoreCase),
            ActionType = actionType,
            ActiveProjectDomainId = session.ActiveProjectDomainId,
            ActiveProjectDomainLabel = session.ActiveProjectDomainLabel,
            WorkflowState = session.WorkflowState.ToString(),
            ProjectSummary = session.Project.SummaryLine(),
            ProjectBaseComplete = session.Project.IsBaseComplete
        };

        public StoreChatResponseDto DenyWorkflow(StoreChatSession session, string action)
        {
            var message = _workflow.DenyMessage(action, session.WorkflowState);
            _logger.LogInformation(
                "Workflow deny action={Action} state={State} session={Session}",
                action, session.WorkflowState, session.SessionId);
            return Ok(session, message, "WORKFLOW_DENIED");
        }

        private void TrimHistory(StoreChatSession session)
        {
            var limit = Math.Max(4, _options.ChatHistoryLimit);
            if (session.History.Count > limit)
                session.History = session.History.TakeLast(limit).ToList();
        }
    }
}
