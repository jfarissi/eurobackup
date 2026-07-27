using System;
using System.Collections.Generic;
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
            // Soft refine kelvin pendant mission SKU (ex. « bureau ») sans gate avant tableau.
            if (SalesMission.IsSimpleSku(session))
            {
                SalesCatalogRefine.TryApplyAnswer(session, text);
                if (string.IsNullOrWhiteSpace(session.PendingRefineSeed)
                    && !string.IsNullOrWhiteSpace(text)
                    && text.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).Length > 3)
                    session.PendingRefineSeed = text;
            }
            else if (SalesCatalogSearchTool.IsLightingQueryPublic(text)
                     && text.Contains("e27", StringComparison.OrdinalIgnoreCase))
            {
                session.PendingRefineSeed = text;
            }

            var searchText = SalesMission.EnrichSearchText(session, text);

            var searchMeta = _context.BuildSearchMeta(session, searchText);
            searchMeta.SkillLevel = session.SkillLevel;
            if (session.BudgetMax is > 0)
                searchMeta.MaxUnitPrice = session.BudgetMax;

            // « Suivant » / nouvelle liste : ne pas renvoyer les mêmes références.
            var excludes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var id in session.LastSuggestedProducts.Select(p => p.ProductId).Where(id => !string.IsNullOrWhiteSpace(id)))
                excludes.Add(id!);
            foreach (var id in session.Cart.Select(c => c.ErpProductId.ToString()))
                excludes.Add(id);

            // Mission SKU : pas d’exclusion agressive au 1er tour (on veut les meilleures E27).
            if (SalesMission.IsSimpleSku(session) && session.LastSuggestedProducts.Count == 0)
                excludes.Clear();

            var products = await SearchProductsAsync(
                searchText,
                session,
                searchMeta,
                ct,
                excludes.Count > 0 ? excludes : null);

            // Gate « questions avant tableau » désactivé (flux vendeur : répondre d’abord).
            session.AwaitingCatalogRefine = false;

            var budgetAlert = SalesBudgetFilter.Apply(products, session, searchMeta);
            SalesQuantityEstimator.ApplySuggestedQuantities(products, session);

            var calc = _deterministicReply.BuildCalculationSummary(session);
            var vagueFollowUp = _deterministicReply.BuildVagueDomainFollowUp(session, searchMeta, searchText);
            var facts = SalesReplyFacts.FromSearch(session, products, searchMeta, calc, vagueFollowUp);
            var aiReply = await _replyComposer.ComposeAsync(facts, ct);

            var reply = _deterministicReply.Compose(aiReply, products, session, searchMeta, searchText);
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

            // SKU simple : pas de compléments boîtes/câbles avant le choix.
            var recos = SalesMission.IsSimpleSku(session)
                ? new List<SalesRecommendationDto>()
                : _recommendations.SuggestComplements(session, products);
            if (recos.Count > 0 && products.Count > 0
                && !string.Equals(session.SkillLevel, "Pro", StringComparison.OrdinalIgnoreCase)
                && searchMeta.WallGuideFamily is null)
            {
                reply = reply.TrimEnd()
                        + "\n\n" + SalesLocale.T(session, "complements_inline") + " "
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
                response.SuppressProjectGuide = session.SuppressProjectGuide;
                return response;
            }

            var empty = _turn.Finish(session, text, reply, "NONE", null, guided);
            empty.SearchFilter = searchMeta;
            empty.BudgetAlert = budgetAlert;
            empty.SkillLevel = session.SkillLevel;
            empty.BudgetMax = session.BudgetMax;
            empty.SuppressProjectGuide = session.SuppressProjectGuide;
            return empty;
        }
    }
}
