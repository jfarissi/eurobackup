using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Services.StoreChat;

namespace Backup.Web.Api.Server.Services.SalesAssistant.Turns
{
    public sealed class CartComplementsHandler : ISalesGuidedTurnHandler
    {
        private readonly ISalesRecommendationEngine _recommendations; private readonly ISalesComplementTool _complements; private readonly ISalesTurnResponder _turn;
        public CartComplementsHandler(ISalesRecommendationEngine recommendations, ISalesComplementTool complements, ISalesTurnResponder turn) { _recommendations = recommendations; _complements = complements; _turn = turn; }
        public GuidedSalesIntent Intent => GuidedSalesIntent.CartComplements;
        public async Task<StoreChatResponseDto?> HandleAsync(SalesGuidedTurnContext ctx, CancellationToken ct = default)
        {
            var session = ctx.Session; var missing = _recommendations.SuggestComplements(session, session.Cart.Select(c => new StoreChatProductSuggestionDto { ProductId = c.ErpProductId.ToString(), Name = c.Name }).ToList());
            session.PendingComplementHints = missing.Select(m => m.SearchHint).Where(h => !string.IsNullOrWhiteSpace(h)).Select(h => h!).Distinct(StringComparer.OrdinalIgnoreCase).Take(5).ToList();
            session.AwaitingComplementConfirm = session.PendingComplementHints.Count > 0;
            var products = await _complements.SearchAsync(session, session.PendingComplementHints, ct);
            var reply = _recommendations.BuildCartComplementsReply(session);
            if (products.Count > 0) { session.AwaitingComplementConfirm = false; session.PendingComplementHints.Clear(); reply += "\n\nVoici des références catalogue pour ces compléments :"; }
            else if (session.AwaitingComplementConfirm) reply += "\n\nRépondez « ok », « d'accord », « oui » ou « vas-y » pour que je cherche ces articles dans le catalogue.";
            var response = _turn.Finish(session, ctx.Text, reply, products.Count > 0 ? "CART_COMPLEMENTS" : "CART_ADVICE", products.Count > 0 ? products : null, ctx.Guided);
            response.Recommendations = missing.ToList(); return response;
        }
    }

    public sealed class ConfirmComplementsHandler : ISalesGuidedTurnHandler
    {
        private readonly ISalesRecommendationEngine _recommendations; private readonly ISalesComplementTool _complements; private readonly ISalesTurnResponder _turn;
        public ConfirmComplementsHandler(ISalesRecommendationEngine recommendations, ISalesComplementTool complements, ISalesTurnResponder turn) { _recommendations = recommendations; _complements = complements; _turn = turn; }
        public GuidedSalesIntent Intent => GuidedSalesIntent.ConfirmComplements;
        public async Task<StoreChatResponseDto?> HandleAsync(SalesGuidedTurnContext ctx, CancellationToken ct = default)
        {
            var session = ctx.Session; var hints = session.PendingComplementHints.ToList();
            if (hints.Count == 0) hints = _recommendations.SuggestComplements(session, session.Cart.Select(c => new StoreChatProductSuggestionDto { ProductId = c.ErpProductId.ToString(), Name = c.Name }).ToList()).Select(m => m.SearchHint).Where(h => !string.IsNullOrWhiteSpace(h)).Select(h => h!).Take(5).ToList();
            var hits = await _complements.SearchAsync(session, hints, ct);
            if (hits.Count == 0) { session.AwaitingComplementConfirm = true; session.PendingComplementHints = hints.ToList(); return _turn.Finish(session, ctx.Text, "Je n'ai pas encore trouvé ces compléments. Réessayez « ok », ou un mot précis : treillis, truelle, auge, gants.", "NONE", null, ctx.Guided); }
            session.AwaitingComplementConfirm = false; session.PendingComplementHints.Clear();
            return _turn.Finish(session, ctx.Text, "Voici les compléments catalogue pour votre panier :\nAjoutez ce dont vous avez besoin, puis devis / commande.", "CART_COMPLEMENTS", hits, ctx.Guided);
        }
    }

    public sealed class DirectComplementHandler : ISalesGuidedTurnHandler
    {
        private readonly ISalesComplementTool _complements; private readonly ISalesTurnResponder _turn;
        public DirectComplementHandler(ISalesComplementTool complements, ISalesTurnResponder turn) { _complements = complements; _turn = turn; }
        public GuidedSalesIntent Intent => GuidedSalesIntent.DirectComplement;
        public async Task<StoreChatResponseDto?> HandleAsync(SalesGuidedTurnContext ctx, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(ctx.Guided.DirectComplementHint)) return null;
            var hint = ctx.Guided.DirectComplementHint!; var hits = await _complements.SearchAsync(ctx.Session, new[] { hint }, ct);
            return hits.Count == 0 ? _turn.Finish(ctx.Session, ctx.Text, $"Je n'ai pas trouvé de produit pour « {hint} ». Essayez un autre mot (ex. handschoen, truelle, auge).", "NONE", null, ctx.Guided) : _turn.Finish(ctx.Session, ctx.Text, $"Voici des références pour « {hint} » :", "CART_COMPLEMENTS", hits, ctx.Guided);
        }
    }

    public sealed class HesitationHandler : ISalesGuidedTurnHandler
    {
        private readonly ISalesConfidenceEngine _confidence; private readonly ISalesTurnResponder _turn;
        public HesitationHandler(ISalesConfidenceEngine confidence, ISalesTurnResponder turn) { _confidence = confidence; _turn = turn; }
        public GuidedSalesIntent Intent => GuidedSalesIntent.Hesitation;
        public Task<StoreChatResponseDto?> HandleAsync(SalesGuidedTurnContext ctx, CancellationToken ct = default) => Task.FromResult<StoreChatResponseDto?>(_turn.Finish(ctx.Session, ctx.Text, _confidence.BuildAdvisorReply(ctx.Session), "ADVISOR", null, ctx.Guided));
    }
    public sealed class StyleHandler : ISalesGuidedTurnHandler
    {
        private readonly ISalesConfidenceEngine _confidence; private readonly ISalesTurnResponder _turn;
        public StyleHandler(ISalesConfidenceEngine confidence, ISalesTurnResponder turn) { _confidence = confidence; _turn = turn; }
        public GuidedSalesIntent Intent => GuidedSalesIntent.Style;
        public Task<StoreChatResponseDto?> HandleAsync(SalesGuidedTurnContext ctx, CancellationToken ct = default) => Task.FromResult<StoreChatResponseDto?>(_turn.Finish(ctx.Session, ctx.Text, _confidence.StyleAdvice(ctx.Session) ?? "Style enregistré.", "STYLE", null, ctx.Guided));
    }
    public sealed class WallSchemaHandler : ISalesGuidedTurnHandler
    {
        private readonly ISalesWallSchemaParser _parser; private readonly ISalesTurnResponder _turn;
        public WallSchemaHandler(ISalesWallSchemaParser parser, ISalesTurnResponder turn) { _parser = parser; _turn = turn; }
        public GuidedSalesIntent Intent => GuidedSalesIntent.WallSchema;
        public Task<StoreChatResponseDto?> HandleAsync(SalesGuidedTurnContext ctx, CancellationToken ct = default) => Task.FromResult<StoreChatResponseDto?>(_parser.TryParse(ctx.Text, ctx.Session, out var schema) ? _turn.Finish(ctx.Session, ctx.Text, schema.Summary, "WALL_SCHEMA", null, ctx.Guided) : null);
    }
    public sealed class TipsHandler : ISalesGuidedTurnHandler
    {
        private readonly ISalesConfidenceEngine _confidence; private readonly ISalesTurnResponder _turn;
        public TipsHandler(ISalesConfidenceEngine confidence, ISalesTurnResponder turn) { _confidence = confidence; _turn = turn; }
        public GuidedSalesIntent Intent => GuidedSalesIntent.Tips;
        public Task<StoreChatResponseDto?> HandleAsync(SalesGuidedTurnContext ctx, CancellationToken ct = default) => Task.FromResult<StoreChatResponseDto?>(_turn.Finish(ctx.Session, ctx.Text, _confidence.BuildTips(ctx.Session, ctx.Session.LastSuggestedProducts), "TIPS", null, ctx.Guided));
    }
    public sealed class SavingsHandler : ISalesGuidedTurnHandler
    {
        private readonly ISalesConfidenceEngine _confidence; private readonly ISalesTurnResponder _turn;
        public SavingsHandler(ISalesConfidenceEngine confidence, ISalesTurnResponder turn) { _confidence = confidence; _turn = turn; }
        public GuidedSalesIntent Intent => GuidedSalesIntent.Savings;
        public Task<StoreChatResponseDto?> HandleAsync(SalesGuidedTurnContext ctx, CancellationToken ct = default) { var savings = _confidence.BuildSavings(ctx.Session.LastSuggestedProducts); var response = _turn.Finish(ctx.Session, ctx.Text, savings?.Summary ?? "Affichez d'abord 2 produits (recherche ou compare), puis redemandez l'économie A vs B.", "SAVINGS", null, ctx.Guided); response.Savings = savings; return Task.FromResult<StoreChatResponseDto?>(response); }
    }
    public sealed class PromosHandler : ISalesGuidedTurnHandler
    {
        private readonly ISalesPromoService _promos; private readonly ISalesTurnResponder _turn;
        public PromosHandler(ISalesPromoService promos, ISalesTurnResponder turn) { _promos = promos; _turn = turn; }
        public GuidedSalesIntent Intent => GuidedSalesIntent.Promos;
        public async Task<StoreChatResponseDto?> HandleAsync(SalesGuidedTurnContext ctx, CancellationToken ct = default) { var lines = await _promos.GetPromosForCartAsync(ctx.Session.Cart, ct); var reply = lines.Count == 0 ? (ctx.Session.Cart.Count == 0 ? "Panier vide — ajoutez des produits pour voir les promos liées au panier." : "Aucune promo active sur les lignes actuelles du panier.") : "Promos liées à votre panier :\n" + string.Join("\n", lines.Select(p => $"• {p.Name} : {p.PromoPrice:N2} €" + (p.Savings.HasValue && p.Savings.Value > 0 ? $" (−{p.Savings:N2} €)" : ""))); var response = _turn.Finish(ctx.Session, ctx.Text, reply, "PROMOS", null, ctx.Guided); response.Promos = lines; return response; }
    }
    public sealed class LogisticsHandler : ISalesGuidedTurnHandler
    {
        private readonly ISalesLogisticsEngine _logistics; private readonly ISalesTurnResponder _turn;
        public LogisticsHandler(ISalesLogisticsEngine logistics, ISalesTurnResponder turn) { _logistics = logistics; _turn = turn; }
        public GuidedSalesIntent Intent => GuidedSalesIntent.Logistics;
        public Task<StoreChatResponseDto?> HandleAsync(SalesGuidedTurnContext ctx, CancellationToken ct = default) { var logistics = _logistics.Evaluate(ctx.Session); var response = _turn.Finish(ctx.Session, ctx.Text, logistics.Summary, "LOGISTICS", null, ctx.Guided); response.Logistics = logistics; return Task.FromResult<StoreChatResponseDto?>(response); }
    }
    public sealed class PlanningHandler : ISalesGuidedTurnHandler
    {
        private readonly ISalesPlanningEngine _planning; private readonly ISalesTurnResponder _turn;
        public PlanningHandler(ISalesPlanningEngine planning, ISalesTurnResponder turn) { _planning = planning; _turn = turn; }
        public GuidedSalesIntent Intent => GuidedSalesIntent.Planning;
        public Task<StoreChatResponseDto?> HandleAsync(SalesGuidedTurnContext ctx, CancellationToken ct = default) => Task.FromResult<StoreChatResponseDto?>(_turn.Finish(ctx.Session, ctx.Text, _planning.BuildPlan(ctx.Session), "PLANNING", null, ctx.Guided));
    }
    public sealed class SemanticSearchHandler : ISalesGuidedTurnHandler
    {
        private readonly ISalesSemanticSearch _search; private readonly ISalesTurnResponder _turn;
        public SemanticSearchHandler(ISalesSemanticSearch search, ISalesTurnResponder turn) { _search = search; _turn = turn; }
        public GuidedSalesIntent Intent => GuidedSalesIntent.SemanticSearch;
        public async Task<StoreChatResponseDto?> HandleAsync(SalesGuidedTurnContext ctx, CancellationToken ct = default) { var products = await _search.SearchAsync(ctx.Text, 5, ct); return _turn.Finish(ctx.Session, ctx.Text, products.Count == 0 ? "Aucun produit proche trouvé (recherche sémantique légère)." : "Produits proches (similarité libellés) :", "SEMANTIC", products, ctx.Guided); }
    }
    public sealed class WhyProductHandler : ISalesGuidedTurnHandler
    {
        private readonly ISalesJustificationService _justification; private readonly ISalesTurnResponder _turn;
        public WhyProductHandler(ISalesJustificationService justification, ISalesTurnResponder turn) { _justification = justification; _turn = turn; }
        public GuidedSalesIntent Intent => GuidedSalesIntent.WhyProduct;
        public Task<StoreChatResponseDto?> HandleAsync(SalesGuidedTurnContext ctx, CancellationToken ct = default) => Task.FromResult<StoreChatResponseDto?>(_turn.Finish(ctx.Session, ctx.Text, SalesSkillTone.AdaptReply(_justification.Justify(ctx.Text, ctx.Session, ctx.Session.LastSuggestedProducts), ctx.Session), "WHY_PRODUCT", null, ctx.Guided));
    }

    public sealed class CompareHandler : ISalesGuidedTurnHandler
    {
        private readonly ISalesCatalogSearchTool _catalog; private readonly ISalesContextDetector _context; private readonly ISalesCompareEngine _compare; private readonly ISalesTurnResponder _turn;
        public CompareHandler(ISalesCatalogSearchTool catalog, ISalesContextDetector context, ISalesCompareEngine compare, ISalesTurnResponder turn) { _catalog = catalog; _context = context; _compare = compare; _turn = turn; }
        public GuidedSalesIntent Intent => GuidedSalesIntent.Compare;
        public async Task<StoreChatResponseDto?> HandleAsync(SalesGuidedTurnContext ctx, CancellationToken ct = default)
        {
            var source = ctx.Session.LastSuggestedProducts.Count >= 2 ? ctx.Session.LastSuggestedProducts : await _catalog.SearchAsync(ctx.Text, ctx.Session, _context.BuildSearchMeta(ctx.Session, ctx.Text), ct);
            var (reply, rows) = _compare.BuildComparison(source, ctx.Guided.CompareBrands);
            var products = rows.Select(r => new StoreChatProductSuggestionDto { ProductId = r.ProductId, Name = r.Name, Brand = r.Brand, Category = r.Category, Price = r.Price }).ToList();
            var response = _turn.Finish(ctx.Session, ctx.Text, SalesSkillTone.AdaptReply(reply, ctx.Session), "COMPARE", products, ctx.Guided); response.CompareRows = rows; return response;
        }
    }

    public sealed class PackRequestHandler : ISalesGuidedTurnHandler
    {
        private readonly ISalesCatalogSearchTool _catalog; private readonly ISalesContextDetector _context; private readonly ISalesPackEngine _packEngine; private readonly ISalesRecommendationEngine _recommendations; private readonly ISalesTurnResponder _turn;
        public PackRequestHandler(ISalesCatalogSearchTool catalog, ISalesContextDetector context, ISalesPackEngine packEngine, ISalesRecommendationEngine recommendations, ISalesTurnResponder turn) { _catalog = catalog; _context = context; _packEngine = packEngine; _recommendations = recommendations; _turn = turn; }
        public GuidedSalesIntent Intent => GuidedSalesIntent.PackRequest;
        public async Task<StoreChatResponseDto?> HandleAsync(SalesGuidedTurnContext ctx, CancellationToken ct = default)
        {
            var session = ctx.Session; var packType = _packEngine.ResolvePackType(ctx.Text, session);
            switch (packType) { case "Painting": session.ActiveProjectDomainId = "painting"; session.ActiveProjectDomainLabel = "Peinture"; break; case "Bathroom": session.ActiveProjectDomainId = "tiling"; session.ActiveProjectDomainLabel = "Carrelage"; break; default: session.ActiveProjectDomainId = "wall_construction"; session.ActiveProjectDomainLabel = "Construction de mur"; break; }
            var savedBrand = session.PreferredBrand; session.PreferredBrand = null;
            var meta = _context.BuildSearchMeta(session, ctx.Text); meta.SkillLevel = session.SkillLevel; if (session.BudgetMax is > 0) meta.MaxUnitPrice = session.BudgetMax;
            var hits = await _catalog.SearchAsync(ctx.Text, session, meta, ct); session.PreferredBrand = savedBrand;
            SalesQuantityEstimator.ApplySuggestedQuantities(hits, session);
            var pack = _packEngine.BuildPack(packType, session, hits);
            var products = pack.Lines.Where(l => !string.IsNullOrWhiteSpace(l.ProductId)).Select(l => new StoreChatProductSuggestionDto { ProductId = l.ProductId!, Name = l.ProductName ?? l.Label, Price = l.UnitPrice, SuggestedQuantity = l.SuggestedQuantity, Category = l.Label }).ToList();
            var response = _turn.Finish(session, ctx.Text, SalesSkillTone.AdaptReply(SalesPackReplyBuilder.Build(pack, session), session), "PACK", products, ctx.Guided);
            response.Pack = pack; response.BudgetAlert = pack.BudgetNote; response.Recommendations = _recommendations.SuggestComplements(session, products).ToList(); meta.Intent = "PACK"; response.SearchFilter = meta; return response;
        }
    }

    public sealed class MoreProductsHandler : ISalesGuidedTurnHandler
    {
        private readonly ISalesCatalogSearchTool _catalog; private readonly ISalesContextDetector _context; private readonly ISalesRecommendationEngine _recommendations; private readonly ISalesTurnResponder _turn;
        public MoreProductsHandler(ISalesCatalogSearchTool catalog, ISalesContextDetector context, ISalesRecommendationEngine recommendations, ISalesTurnResponder turn) { _catalog = catalog; _context = context; _recommendations = recommendations; _turn = turn; }
        public GuidedSalesIntent Intent => GuidedSalesIntent.MoreProducts;
        public async Task<StoreChatResponseDto?> HandleAsync(SalesGuidedTurnContext ctx, CancellationToken ct = default)
        {
            var session = ctx.Session; var meta = _context.BuildSearchMeta(session, ctx.Text); meta.SkillLevel = session.SkillLevel; if (session.BudgetMax is > 0) meta.MaxUnitPrice = session.BudgetMax;
            var excludes = session.LastSuggestedProducts.Select(p => p.ProductId).Where(id => !string.IsNullOrWhiteSpace(id)).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var seed = !string.IsNullOrWhiteSpace(session.ActiveProjectDomainLabel) ? session.ActiveProjectDomainLabel! : session.History.LastOrDefault(h => h.Role == "user" && !h.Content.Contains("autre", StringComparison.OrdinalIgnoreCase) && h.Content.Length > 8)?.Content ?? "jardin";
            var products = await _catalog.SearchAsync(seed, session, meta, ct, excludes);
            SalesQuantityEstimator.ApplySuggestedQuantities(products, session);
            var budgetAlert = SalesBudgetFilter.Apply(products, session, meta);
            var reply = products.Count == 0 ? "Je n'ai pas d'autres références pertinentes pour l'instant. Précisez (bordure, clôture, gravier…)."
                : "Voici d'autres produits" + (string.IsNullOrWhiteSpace(session.ActiveProjectDomainLabel) ? " :" : $" pour {session.ActiveProjectDomainLabel} :");
            if (!string.IsNullOrWhiteSpace(budgetAlert)) reply = reply.TrimEnd() + "\n\n" + budgetAlert;
            var response = _turn.Finish(session, ctx.Text, reply, "PRODUCT_LIST", products, ctx.Guided);
            response.SearchFilter = meta; response.BudgetAlert = budgetAlert; response.Recommendations = _recommendations.SuggestComplements(session, products).ToList(); return response;
        }
    }
}
