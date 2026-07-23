using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Services.SalesAssistant;

namespace Backup.Web.Api.Server.Services.StoreChat
{
    public partial class StoreChatService
    {
        private async Task<StoreChatResponseDto> HandleProductSearchTurnAsync(
            StoreChatSession session,
            string text,
            GuidedSalesSlots guided,
            CancellationToken ct)
        {
            var searchMeta = _context.BuildSearchMeta(session, text);
            searchMeta.SkillLevel = session.SkillLevel;
            if (session.BudgetMax is > 0)
                searchMeta.MaxUnitPrice = session.BudgetMax;

            var products = await SearchProductsAsync(text, session, searchMeta, ct);
            var budgetAlert = SalesBudgetFilter.Apply(products, session, searchMeta);
            SalesQuantityEstimator.ApplySuggestedQuantities(products, session);

            // LLM = voix uniquement : faits C# → ReplyComposer (jamais d'outils / inventaire libre).
            var calc = _deterministicReply.BuildCalculationSummary(session);
            var vagueFollowUp = _deterministicReply.BuildVagueDomainFollowUp(session, searchMeta, text);
            var facts = SalesReplyFacts.FromSearch(session, products, searchMeta, calc, vagueFollowUp);
            var aiReply = await _replyComposer.ComposeAsync(facts, ct);

            var reply = _deterministicReply.Compose(aiReply, products, session, searchMeta, text);
            if (!string.IsNullOrWhiteSpace(budgetAlert))
                reply = reply.TrimEnd() + "\n\n" + budgetAlert;
            if (guided.BudgetMentioned && session.BudgetMax is > 0 && string.IsNullOrWhiteSpace(budgetAlert))
                reply = reply.TrimEnd() + $"\n\nBudget enregistré : {session.BudgetMax:N2} € (filtre prix unitaire).";
            if (guided.SkillMentioned && !string.IsNullOrWhiteSpace(session.SkillLevel))
                reply = reply.TrimEnd() + $"\n\nProfil : {session.SkillLevel}.";

            var styleAdvice = _confidence.StyleAdvice(session);
            if (!string.IsNullOrWhiteSpace(styleAdvice) && guided.Intent == GuidedSalesIntent.Style)
                reply = reply.TrimEnd() + "\n\n" + styleAdvice;
            else if (!string.IsNullOrWhiteSpace(session.PreferredStyle)
                     && products.Count > 0
                     && (session.ActiveProjectDomainId is "tiling" or "painting"))
            {
                reply = reply.TrimEnd() + "\n\n" + styleAdvice;
            }

            var recos = _recommendations.SuggestComplements(session, products);
            if (recos.Count > 0 && products.Count > 0
                && !string.Equals(session.SkillLevel, "Pro", StringComparison.OrdinalIgnoreCase)
                && searchMeta.WallGuideFamily is null)
            {
                reply = reply.TrimEnd()
                        + "\n\nCompléments utiles : "
                        + string.Join(" · ", recos.Take(3).Select(r => $"{r.Label} ({r.Reason})"));
            }

            if (guided.SkillMentioned || guided.Intent != GuidedSalesIntent.None || guided.BudgetMentioned)
                reply = SalesSkillTone.AdaptReply(reply, session);

            searchMeta.Intent = products.Count > 0 ? "PRODUCT_LIST" : "NONE";
            if (products.Count > 0)
            {
                var response = _turn.Finish(session, text, reply, "PRODUCT_LIST", products, guided);
                response.SearchFilter = searchMeta;
                response.BudgetAlert = budgetAlert;
                response.Recommendations = recos.ToList();
                return response;
            }

            var empty = _turn.Finish(session, text, reply, "NONE", null, guided);
            empty.SearchFilter = searchMeta;
            empty.BudgetAlert = budgetAlert;
            empty.SkillLevel = session.SkillLevel;
            empty.BudgetMax = session.BudgetMax;
            return empty;
        }
    }
}
