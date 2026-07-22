using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Models;
using Backup.Web.Api.Server.Services.ErpSync;
using Backup.Web.Api.Server.Services.SalesAssistant;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Backup.Web.Api.Server.Services.StoreChat
{
    public interface IStoreChatService
    {
        Task<StoreChatResponseDto> ProcessMessageAsync(StoreChatMessageRequest request, CancellationToken ct = default);
        Task<StoreChatPaymentResultDto?> GetPaymentResultAsync(Guid orderId, CancellationToken ct = default);
        Task<StoreChatPaymentResultDto?> ConfirmPaymentAsync(Guid orderId, string? stripeSessionId, CancellationToken ct = default);
    }

    public class StoreChatService : IStoreChatService
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private readonly IStorageBroker _storage;
        private readonly IStoreChatSessionStore _sessions;
        private readonly IStoreChatAiClient _ai;
        private readonly IStoreChatPdfService _pdf;
        private readonly IStoreChatStripeService _stripe;
        private readonly ISalesGuidedIntentDetector _guidedIntent;
        private readonly ISalesPackEngine _packEngine;
        private readonly ISalesRecommendationEngine _recommendations;
        private readonly ISalesCompareEngine _compareEngine;
        private readonly ISalesJustificationService _justification;
        private readonly ISalesConfidenceEngine _confidence;
        private readonly ISalesPromoService _promos;
        private readonly ISalesLogisticsEngine _logistics;
        private readonly ISalesPlanningEngine _planning;
        private readonly ISalesProjectResumeService _resume;
        private readonly ISalesPhotoClassifier _photoClassifier;
        private readonly ISalesWallSchemaParser _wallSchema;
        private readonly ISalesSemanticSearch _semanticSearch;
        private readonly StoreChatOptions _options;
        private readonly ErpSyncOptions _erpOptions;
        private readonly ILogger<StoreChatService> _logger;

        public StoreChatService(
            IStorageBroker storage,
            IStoreChatSessionStore sessions,
            IStoreChatAiClient ai,
            IStoreChatPdfService pdf,
            IStoreChatStripeService stripe,
            ISalesGuidedIntentDetector guidedIntent,
            ISalesPackEngine packEngine,
            ISalesRecommendationEngine recommendations,
            ISalesCompareEngine compareEngine,
            ISalesJustificationService justification,
            ISalesConfidenceEngine confidence,
            ISalesPromoService promos,
            ISalesLogisticsEngine logistics,
            ISalesPlanningEngine planning,
            ISalesProjectResumeService resume,
            ISalesPhotoClassifier photoClassifier,
            ISalesWallSchemaParser wallSchema,
            ISalesSemanticSearch semanticSearch,
            IOptions<StoreChatOptions> options,
            IOptions<ErpSyncOptions> erpOptions,
            ILogger<StoreChatService> logger)
        {
            _storage = storage;
            _sessions = sessions;
            _ai = ai;
            _pdf = pdf;
            _stripe = stripe;
            _guidedIntent = guidedIntent;
            _packEngine = packEngine;
            _recommendations = recommendations;
            _compareEngine = compareEngine;
            _justification = justification;
            _confidence = confidence;
            _promos = promos;
            _logistics = logistics;
            _planning = planning;
            _resume = resume;
            _photoClassifier = photoClassifier;
            _wallSchema = wallSchema;
            _semanticSearch = semanticSearch;
            _options = options.Value ?? new StoreChatOptions();
            _erpOptions = erpOptions.Value ?? new ErpSyncOptions();
            _logger = logger;
        }

        public async Task<StoreChatResponseDto> ProcessMessageAsync(StoreChatMessageRequest request, CancellationToken ct = default)
        {
            var session = _sessions.GetOrCreate(request.SessionId);
            var intent = (request.ClientIntent ?? string.Empty).Trim();

            if (intent.Equals("NewProject", StringComparison.OrdinalIgnoreCase)
                || IsNewProjectText(request.Text))
            {
                return await ResetToNewProjectAsync(session, ct);
            }

            if (intent.Equals("AddToCartFromList", StringComparison.OrdinalIgnoreCase))
            {
                await AddToCartAsync(session, request.TargetProductId, request.TargetQuantity ?? 1, ct);
                _sessions.Save(session);
                return Ok(session, "Produit ajouté au panier.", "CART_UPDATED");
            }

            if (intent.Equals("RemoveFromCartFromList", StringComparison.OrdinalIgnoreCase))
            {
                RemoveFromCart(session, request.TargetProductId);
                _sessions.Save(session);
                return Ok(session, "Produit retiré du panier.", "CART_UPDATED");
            }

            if (intent.Equals("CreateQuoteFromTableSelection", StringComparison.OrdinalIgnoreCase))
            {
                await ReplaceCartFromTableAsync(session, request.TableCartLines, ct);
                return await CreateQuoteAsync(session, ct);
            }

            if (intent.Equals("CreateOrderFromTableSelection", StringComparison.OrdinalIgnoreCase))
            {
                await ReplaceCartFromTableAsync(session, request.TableCartLines, ct);
                return await CreateOrderAsync(session, ct);
            }

            var text = (request.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(text) && string.IsNullOrWhiteSpace(request.ImageCaption) && string.IsNullOrWhiteSpace(request.ImageBase64))
                return Ok(session, "Message vide.", "NONE");

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

                return FinishTextReply(session, text.Length > 0 ? text : "(photo)", photoReply, "PHOTO", null,
                    new GuidedSalesSlots { Intent = GuidedSalesIntent.None });
            }

            if (string.IsNullOrWhiteSpace(text))
                return Ok(session, "Message vide.", "NONE");

            var guided = _guidedIntent.Detect(text, session);
            _confidence.DetectStyle(text, session);

            if (guided.Intent == GuidedSalesIntent.ResumeProject)
            {
                var (ok, resumeReply, project) = await _resume.TryResumeAsync(text, session, ct);
                if (ok)
                {
                    var res = FinishTextReply(session, text, resumeReply, "RESUME_PROJECT", null, guided);
                    if (project != null)
                    {
                        res.SalesProjectId = project.Id;
                        res.SalesProjectTitle = project.Title;
                    }

                    return res;
                }
            }

            var previousBrand = session.PreferredBrand;
            await DetectBrandAsync(session, text, ct);
            if (!string.IsNullOrWhiteSpace(session.PreferredBrand)
                && !string.IsNullOrWhiteSpace(previousBrand)
                && !string.Equals(previousBrand, session.PreferredBrand, StringComparison.OrdinalIgnoreCase))
            {
                session.SearchTypeHints.Clear();
                session.PreferredWeightKg = null;
            }

            DetectDomain(session, text);
            if (!string.IsNullOrWhiteSpace(session.PreferredBrand) && !IsExplicitWallIntent(text))
            {
                // Évite qu'un ancien projet « mur » pollue une recherche marque.
                if (string.Equals(session.ActiveProjectDomainId, "wall_construction", StringComparison.OrdinalIgnoreCase))
                {
                    session.ActiveProjectDomainId = null;
                    session.ActiveProjectDomainLabel = null;
                }
            }
            ParseWallDimensions(session, text);
            CollectMaterialHints(session, text);
            UpdateStickySearchFilters(session, text);

            if (guided.Intent == GuidedSalesIntent.CartComplements)
            {
                var cartReply = _recommendations.BuildCartComplementsReply(session);
                var missing = _recommendations.SuggestComplements(
                    session,
                    session.Cart.Select(c => new StoreChatProductSuggestionDto
                    {
                        ProductId = c.ErpProductId.ToString(),
                        Name = c.Name
                    }).ToList());

                session.PendingComplementHints = missing
                    .Select(m => m.SearchHint)
                    .Where(h => !string.IsNullOrWhiteSpace(h))
                    .Select(h => h!)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(5)
                    .ToList();
                session.AwaitingComplementConfirm = session.PendingComplementHints.Count > 0;

                // Essaie déjà d'afficher des produits ; sinon « ok go » les chargera.
                var complementProducts = await SearchComplementProductsAsync(session, session.PendingComplementHints, ct);
                if (complementProducts.Count > 0)
                {
                    session.AwaitingComplementConfirm = false;
                    session.PendingComplementHints.Clear();
                    cartReply += "\n\nVoici des références catalogue pour ces compléments :";
                }
                else if (session.AwaitingComplementConfirm)
                {
                    cartReply += "\n\nRépondez « ok », « d'accord », « oui » ou « vas-y » pour que je cherche ces articles dans le catalogue.";
                }

                var cartResponse = FinishTextReply(
                    session,
                    text,
                    cartReply,
                    complementProducts.Count > 0 ? "CART_COMPLEMENTS" : "CART_ADVICE",
                    complementProducts.Count > 0 ? complementProducts : null,
                    guided);
                cartResponse.Recommendations = missing.ToList();
                return cartResponse;
            }

            if (guided.Intent == GuidedSalesIntent.ConfirmComplements)
            {
                var hints = session.PendingComplementHints.ToList();
                if (hints.Count == 0)
                {
                    // Reconstruit depuis le panier si la session a perdu les hints.
                    hints = _recommendations.SuggestComplements(
                            session,
                            session.Cart.Select(c => new StoreChatProductSuggestionDto
                            {
                                ProductId = c.ErpProductId.ToString(),
                                Name = c.Name
                            }).ToList())
                        .Select(m => m.SearchHint)
                        .Where(h => !string.IsNullOrWhiteSpace(h))
                        .Select(h => h!)
                        .Take(5)
                        .ToList();
                }

                var complementHits = await SearchComplementProductsAsync(session, hints, ct);
                if (complementHits.Count == 0)
                {
                    // Garde l'attente : l'utilisateur peut réessayer (ok / treillis…).
                    session.AwaitingComplementConfirm = true;
                    session.PendingComplementHints = hints.ToList();
                    return FinishTextReply(
                        session,
                        text,
                        "Je n'ai pas encore trouvé ces compléments. Réessayez « ok », ou un mot précis : treillis, truelle, auge, gants.",
                        "NONE",
                        null,
                        guided);
                }

                session.AwaitingComplementConfirm = false;
                session.PendingComplementHints.Clear();

                var confirmReply = "Voici les compléments catalogue pour votre panier :\n"
                            + "Ajoutez ce dont vous avez besoin, puis devis / commande.";
                return FinishTextReply(session, text, confirmReply, "CART_COMPLEMENTS", complementHits, guided);
            }

            if (guided.Intent == GuidedSalesIntent.DirectComplement
                && !string.IsNullOrWhiteSpace(guided.DirectComplementHint))
            {
                var hint = guided.DirectComplementHint!;
                var hits = await SearchComplementProductsAsync(session, new[] { hint }, ct);
                if (hits.Count == 0)
                {
                    return FinishTextReply(
                        session,
                        text,
                        $"Je n'ai pas trouvé de produit pour « {hint} ». Essayez un autre mot (ex. handschoen, truelle, auge).",
                        "NONE",
                        null,
                        guided);
                }

                return FinishTextReply(
                    session,
                    text,
                    $"Voici des références pour « {hint} » :",
                    "CART_COMPLEMENTS",
                    hits,
                    guided);
            }

            if (guided.Intent == GuidedSalesIntent.Hesitation)
            {
                var hesitateReply = _confidence.BuildAdvisorReply(session);
                return FinishTextReply(session, text, hesitateReply, "ADVISOR", null, guided);
            }

            if (guided.Intent == GuidedSalesIntent.Style)
            {
                var styleReply = _confidence.StyleAdvice(session) ?? "Style enregistré.";
                return FinishTextReply(session, text, styleReply, "STYLE", null, guided);
            }

            if (guided.Intent == GuidedSalesIntent.WallSchema
                && _wallSchema.TryParse(text, session, out var schema))
            {
                return FinishTextReply(session, text, schema.Summary, "WALL_SCHEMA", null, guided);
            }

            if (guided.Intent == GuidedSalesIntent.Tips)
            {
                var tips = _confidence.BuildTips(session, session.LastSuggestedProducts);
                return FinishTextReply(session, text, tips, "TIPS", null, guided);
            }

            if (guided.Intent == GuidedSalesIntent.Savings)
            {
                var savings = _confidence.BuildSavings(session.LastSuggestedProducts);
                var savingsReply = savings?.Summary
                    ?? "Affichez d'abord 2 produits (recherche ou compare), puis redemandez l'économie A vs B.";
                var savingsResponse = FinishTextReply(session, text, savingsReply, "SAVINGS", null, guided);
                savingsResponse.Savings = savings;
                return savingsResponse;
            }

            if (guided.Intent == GuidedSalesIntent.Promos)
            {
                var promoLines = await _promos.GetPromosForCartAsync(session.Cart, ct);
                var promoReply = promoLines.Count == 0
                    ? (session.Cart.Count == 0
                        ? "Panier vide — ajoutez des produits pour voir les promos liées au panier."
                        : "Aucune promo active sur les lignes actuelles du panier.")
                    : "Promos liées à votre panier :\n"
                      + string.Join("\n", promoLines.Select(p =>
                          $"• {p.Name} : {p.PromoPrice:N2} €"
                          + (p.Savings.HasValue && p.Savings.Value > 0 ? $" (−{p.Savings:N2} €)" : "")));
                var promoResponse = FinishTextReply(session, text, promoReply, "PROMOS", null, guided);
                promoResponse.Promos = promoLines;
                return promoResponse;
            }

            if (guided.Intent == GuidedSalesIntent.Logistics)
            {
                var log = _logistics.Evaluate(session);
                var logResponse = FinishTextReply(session, text, log.Summary, "LOGISTICS", null, guided);
                logResponse.Logistics = log;
                return logResponse;
            }

            if (guided.Intent == GuidedSalesIntent.Planning)
            {
                var plan = _planning.BuildPlan(session);
                return FinishTextReply(session, text, plan, "PLANNING", null, guided);
            }

            if (guided.Intent == GuidedSalesIntent.SemanticSearch)
            {
                var semantic = await _semanticSearch.SearchAsync(text, 5, ct);
                var semReply = semantic.Count == 0
                    ? "Aucun produit proche trouvé (recherche sémantique légère)."
                    : "Produits proches (similarité libellés) :";
                var semResponse = FinishTextReply(session, text, semReply, "SEMANTIC", semantic, guided);
                return semResponse;
            }

            if (guided.Intent == GuidedSalesIntent.WhyProduct)
            {
                var why = _justification.Justify(text, session, session.LastSuggestedProducts);
                why = SalesSkillTone.AdaptReply(why, session);
                return FinishTextReply(session, text, why, "WHY_PRODUCT", null, guided);
            }

            if (guided.Intent == GuidedSalesIntent.Compare)
            {
                var source = session.LastSuggestedProducts.Count >= 2
                    ? session.LastSuggestedProducts
                    : await SearchProductsAsync(text, session, BuildSearchMeta(session, text), ct);
                var (compareReply, rows) = _compareEngine.BuildComparison(source, guided.CompareBrands);
                compareReply = SalesSkillTone.AdaptReply(compareReply, session);
                var compareProducts = rows.Select(r => new StoreChatProductSuggestionDto
                {
                    ProductId = r.ProductId,
                    Name = r.Name,
                    Brand = r.Brand,
                    Category = r.Category,
                    Price = r.Price
                }).ToList();
                var compareResponse = FinishTextReply(session, text, compareReply, "COMPARE", compareProducts, guided);
                compareResponse.CompareRows = rows;
                return compareResponse;
            }

            var searchMeta = BuildSearchMeta(session, text);
            searchMeta.SkillLevel = session.SkillLevel;
            if (session.BudgetMax is > 0)
                searchMeta.MaxUnitPrice = session.BudgetMax;

            if (guided.Intent == GuidedSalesIntent.PackRequest)
            {
                var packType = _packEngine.ResolvePackType(text, session);
                switch (packType)
                {
                    case "Painting":
                        session.ActiveProjectDomainId = "painting";
                        session.ActiveProjectDomainLabel = "Peinture";
                        break;
                    case "Bathroom":
                        session.ActiveProjectDomainId = "tiling";
                        session.ActiveProjectDomainLabel = "Carrelage";
                        break;
                    default:
                        session.ActiveProjectDomainId = "wall_construction";
                        session.ActiveProjectDomainLabel = "Construction de mur";
                        break;
                }

                // Pack = kit chantier : ne pas restreindre à une marque sticky.
                var savedBrand = session.PreferredBrand;
                session.PreferredBrand = null;
                var packMeta = BuildSearchMeta(session, text);
                packMeta.SkillLevel = session.SkillLevel;
                if (session.BudgetMax is > 0)
                    packMeta.MaxUnitPrice = session.BudgetMax;
                var packHits = await SearchProductsAsync(text, session, packMeta, ct);
                session.PreferredBrand = savedBrand;
                ApplySuggestedQuantities(packHits, session);
                var pack = _packEngine.BuildPack(packType, session, packHits);
                var packProducts = pack.Lines
                    .Where(l => !string.IsNullOrWhiteSpace(l.ProductId))
                    .Select(l => new StoreChatProductSuggestionDto
                    {
                        ProductId = l.ProductId!,
                        Name = l.ProductName ?? l.Label,
                        Price = l.UnitPrice,
                        SuggestedQuantity = l.SuggestedQuantity,
                        Category = l.Label
                    })
                    .ToList();

                var packReply = BuildPackReply(pack, session);
                packReply = SalesSkillTone.AdaptReply(packReply, session);
                var packResponse = FinishTextReply(session, text, packReply, "PACK", packProducts, guided);
                packResponse.Pack = pack;
                packResponse.BudgetAlert = pack.BudgetNote;
                packResponse.Recommendations = _recommendations.SuggestComplements(session, packProducts).ToList();
                packMeta.Intent = "PACK";
                packResponse.SearchFilter = packMeta;
                return packResponse;
            }

            // Garde-fou : confirmation courte ne doit jamais relancer une recherche mur/marque.
            if (IsBareConfirmation(text)
                && (session.AwaitingComplementConfirm
                    || session.PendingComplementHints.Count > 0
                    || string.Equals(session.LastActionType, "CART_ADVICE", StringComparison.OrdinalIgnoreCase)))
            {
                return FinishTextReply(
                    session,
                    text,
                    "Je cherche les compléments… Réessayez « ok », ou un mot précis : treillis, truelle, auge, gants.",
                    "NONE",
                    null,
                    guided);
            }

            var products = await SearchProductsAsync(text, session, searchMeta, ct);
            var budgetAlert = ApplyBudgetFilter(products, session, searchMeta);
            ApplySuggestedQuantities(products, session);
            var catalogContext = BuildCatalogContext(products, searchMeta);
            var aiReply = await _ai.CompleteAsync(session.History, text, catalogContext, ct);

            session.History.Add(new StoreChatHistoryMessage { Role = "user", Content = text });
            var reply = BuildUserFacingReply(aiReply, products, session, searchMeta);
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
                && !string.Equals(session.SkillLevel, "Pro", StringComparison.OrdinalIgnoreCase))
            {
                reply = reply.TrimEnd()
                        + "\n\nCompléments utiles : "
                        + string.Join(" · ", recos.Take(3).Select(r => $"{r.Label} ({r.Reason})"));
            }

            if (guided.SkillMentioned
                || guided.Intent != GuidedSalesIntent.None
                || guided.BudgetMentioned)
            {
                reply = SalesSkillTone.AdaptReply(reply, session);
            }

            session.History.Add(new StoreChatHistoryMessage { Role = "assistant", Content = reply });
            TrimHistory(session);
            if (products.Count > 0)
                session.LastSuggestedProducts = products.ToList();
            session.LastActionType = products.Count > 0 ? "PRODUCT_LIST" : "NONE";
            _sessions.Save(session);

            searchMeta.Intent = products.Count > 0 ? "PRODUCT_LIST" : "NONE";

            if (products.Count > 0)
            {
                return new StoreChatResponseDto
                {
                    SessionId = session.SessionId,
                    ReplyText = reply,
                    HasAction = true,
                    ActionType = "PRODUCT_LIST",
                    ActionData = products,
                    Products = products,
                    ActiveProjectDomainId = session.ActiveProjectDomainId,
                    ActiveProjectDomainLabel = session.ActiveProjectDomainLabel,
                    SearchFilter = searchMeta,
                    BudgetAlert = budgetAlert,
                    SkillLevel = session.SkillLevel,
                    BudgetMax = session.BudgetMax,
                    Recommendations = recos.ToList()
                };
            }

            var empty = Ok(session, reply, "NONE");
            empty.SearchFilter = searchMeta;
            empty.BudgetAlert = budgetAlert;
            empty.SkillLevel = session.SkillLevel;
            empty.BudgetMax = session.BudgetMax;
            return empty;
        }

        private StoreChatResponseDto FinishTextReply(
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
                session.LastSuggestedProducts = products.ToList();
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
                BudgetMax = session.BudgetMax
            };

            if (guided.BudgetMentioned || session.BudgetMax is > 0)
                response.BudgetMax = session.BudgetMax;
            return response;
        }

        private static string BuildPackReply(SalesPackDto pack, StoreChatSession session)
        {
            var lines = string.Join("\n", pack.Lines.Select(l =>
                l.ProductName != null
                    ? $"• {l.Label} : {l.ProductName} × {l.SuggestedQuantity:0.##}"
                      + (l.UnitPrice is > 0 ? $" ({l.UnitPrice:N2} €)" : "")
                    : $"• {l.Label} : à choisir (qté ~{l.SuggestedQuantity:0.##})"));

            var total = pack.EstimatedTotal is > 0 ? $"\nTotal estimé : {pack.EstimatedTotal:N2} €." : "";
            var budget = string.IsNullOrWhiteSpace(pack.BudgetNote) ? "" : "\n" + pack.BudgetNote;
            var skill = string.Equals(session.SkillLevel, "Pro", StringComparison.OrdinalIgnoreCase)
                ? "\nValidez le pack puis devis PDF."
                : "\nVous pouvez ajuster les quantités puis ajouter au panier.";

            return $"{pack.Title} — {pack.Lines.Count} lignes :\n{lines}{total}{budget}{skill}";
        }

        /// <summary>
        /// Évite de reproposer structure/liant déjà couverts par le panier lors des compléments.
        /// </summary>
        private static bool ContainsCartStructureNoise(string hay, StoreChatSession session)
        {
            var cart = string.Join(' ', session.Cart.Select(c => c.Name)).ToLowerInvariant();
            var cartHasStructure = cart.Contains("brique") || cart.Contains("baksteen") || cart.Contains("blok")
                                   || cart.Contains("steen") || cart.Contains("porotherm") || cart.Contains("silka");
            var cartHasBinder = cart.Contains("ciment") || cart.Contains("cement") || cart.Contains("mortier")
                                || cart.Contains("mortel");

            if (cartHasStructure && (hay.Contains("brique") || hay.Contains("baksteen") || hay.Contains("lijmblok")
                                     || hay.Contains("kalkzand") || hay.Contains("porotherm") || hay.Contains("snelbouw")))
                return true;

            if (cartHasBinder && (hay.Contains("ciment") || hay.Contains("cement") || hay.Contains("mortier")
                                  || hay.Contains("mortel")))
                return true;

            return false;
        }

        private async Task<List<StoreChatProductSuggestionDto>> SearchComplementProductsAsync(
            StoreChatSession session,
            IReadOnlyList<string> hints,
            CancellationToken ct)
        {
            var complementProducts = new List<StoreChatProductSuggestionDto>();
            foreach (var hint in hints.Take(4))
            {
                var terms = ExpandComplementSearchTerms(hint);
                var hits = await SearchProductsByTermsAsync(terms, ct);

                // Appoint sémantique uniquement si SQL vide — puis filtre strict.
                if (hits.Count == 0)
                {
                    foreach (var term in terms.Take(2))
                    {
                        var sem = await _semanticSearch.SearchAsync(term, 6, ct);
                        hits.AddRange(sem);
                    }
                }

                var best = hits
                    .Where(h => complementProducts.All(p => p.ProductId != h.ProductId))
                    .Where(h => session.Cart.All(c => c.ErpProductId.ToString() != h.ProductId))
                    .Select(h =>
                    {
                        var hay = $"{h.Name} {h.Category} {h.Brand}".ToLowerInvariant();
                        return new { Product = h, Hay = hay, Score = ScoreComplementHit(hay, hint) };
                    })
                    .Where(x => x.Score > 0)
                    .Where(x => !ContainsCartStructureNoise(x.Hay, session))
                    .OrderByDescending(x => x.Score)
                    .ThenBy(x => x.Product.Name)
                    .Select(x => x.Product)
                    .FirstOrDefault();

                if (best == null)
                    continue;

                best.SuggestedQuantity ??= 1;
                complementProducts.Add(best);
                if (complementProducts.Count >= 4)
                    return complementProducts;
            }

            return complementProducts;
        }

        /// <summary>Termes SQL stricts (pas de synonymes trop larges type « niveau »).</summary>
        private static IReadOnlyList<string> ExpandComplementSearchTerms(string hint)
        {
            return hint.Trim().ToLowerInvariant() switch
            {
                "truelle" => new[] { "truelle", "troffel", "truweel", "metseltroffel", "waterpas" },
                "treillis" => new[] { "treillis", "wapeningsnet", "wapeningsgaas", "bewapeningsnet", "wapening", "mesh" },
                "auge" => new[] { "auge", "mortelkuip", "speciekuip", "mengkuip", "emmer", "seau", "kuip" },
                // handschoen en tête : existe en base NL, prioritaire sur "gants" FR / gloves USAG.
                "gants" => new[] { "handschoen", "handschoenen", "werkhandschoen", "gants", "gloves" },
                "ciment" => new[] { "ciment", "cement", "mortier", "mortel", "metselspecie" },
                "seau" => new[] { "seau", "auge", "emmer", "mortelkuip", "kuip" },
                "rouleau" => new[] { "rouleau", "roller", "verfroller" },
                "ruban" => new[] { "ruban", "masking", "schilderstape" },
                "sous-couche" => new[] { "sous-couche", "primer", "grondverf" },
                "colle carrelage" => new[] { "tegellijm", "colle carrelage", "colle" },
                "joint" => new[] { "voegsel", "voeg", "joint" },
                "primaire" => new[] { "primaire", "primer", "grondverf" },
                _ => new[] { hint }
            };
        }

        private async Task<List<StoreChatProductSuggestionDto>> SearchProductsByTermsAsync(
            IReadOnlyList<string> terms,
            CancellationToken ct)
        {
            var results = new List<StoreChatProductSuggestionDto>();
            var seen = new HashSet<int>();

            // Ne pas couper trop tôt : chaque terme (ex. handschoen) doit pouvoir remonter.
            foreach (var term in terms.Where(t => t.Length >= 3).Take(8))
            {
                var needle = term.ToLowerInvariant();
                var rows = await _storage.SelectAllErpProducts()
                    .AsNoTracking()
                    .Where(p =>
                        (p.Name != null && p.Name.ToLower().Contains(needle))
                        || (p.Name2 != null && p.Name2.ToLower().Contains(needle))
                        || (p.Reference != null && p.Reference.ToLower().Contains(needle))
                        || (p.Brand != null && p.Brand.ToLower().Contains(needle))
                        || (p.TypeName != null && p.TypeName.ToLower().Contains(needle))
                        || (p.SubTypeName != null && p.SubTypeName.ToLower().Contains(needle))
                        || (p.MainTypeName != null && p.MainTypeName.ToLower().Contains(needle)))
                    .OrderBy(p => p.Name)
                    .Take(20)
                    .Select(p => new
                    {
                        p.Id,
                        p.Name,
                        p.Name2,
                        p.Brand,
                        p.UnitPrice,
                        p.PriceHT,
                        p.MainTypeName,
                        p.TypeName,
                        p.SubTypeName,
                        p.PicName,
                        p.Reference
                    })
                    .ToListAsync(ct);

                foreach (var p in rows)
                {
                    if (!seen.Add(p.Id))
                        continue;

                    results.Add(new StoreChatProductSuggestionDto
                    {
                        ProductId = p.Id.ToString(CultureInfo.InvariantCulture),
                        Name = FormatProductDisplayName(p.Name, p.Name2, p.Reference, p.Id),
                        Brand = p.Brand,
                        Price = p.UnitPrice ?? p.PriceHT,
                        Category = string.Join(" / ", new[] { p.MainTypeName, p.TypeName, p.SubTypeName }
                            .Where(x => !string.IsNullOrWhiteSpace(x))),
                        SuggestedQuantity = 1,
                        ImageUrl = BuildProductImageUrl(p.PicName)
                    });
                }

                if (results.Count >= 40)
                    break;
            }

            return results;
        }

        /// <summary>Score &gt; 0 = accepté. Pénalise les faux positifs (Gauge⊃auge, Fuel Gauge…).</summary>
        private static int ScoreComplementHit(string hay, string hint)
        {
            if (string.IsNullOrWhiteSpace(hay))
                return 0;

            // Bruit hors chantier (faux positifs sémantiques / synonymes larges).
            if (ContainsAnyLocal(hay, "fuel gauge", "flotteur", "remover", "bagues du", "blow gun", "gonflage"))
                return 0;

            // « gauge » contient la sous-chaîne « auge » → faux positif classique.
            if (hay.Contains("gauge", StringComparison.OrdinalIgnoreCase)
                && hint.Trim().Equals("auge", StringComparison.OrdinalIgnoreCase))
                return 0;

            return hint.Trim().ToLowerInvariant() switch
            {
                "truelle" => ScoreAny(hay,
                    ("truelle", 100), ("troffel", 100), ("truweel", 100), ("metseltroffel", 100),
                    ("waterpas", 70), ("spatel", 40)),
                "treillis" => ScoreAny(hay,
                    ("bewapeningsnet", 100), ("wapeningsnet", 100), ("wapeningsgaas", 100),
                    ("treillis", 90), ("wapening", 80), ("mesh", 60)),
                "auge" or "seau" => ScoreAuge(hay),
                "gants" => ScoreGloves(hay),
                "ciment" => ScoreAny(hay, ("ciment", 100), ("cement", 100), ("mortier", 80), ("mortel", 80)),
                _ => hay.Contains(hint, StringComparison.OrdinalIgnoreCase) ? 50 : 0
            };
        }

        private static int ScoreAuge(string hay)
        {
            var score = ScoreAny(hay,
                ("mortelkuip", 100), ("speciekuip", 100), ("mengkuip", 100),
                ("emmer", 80), ("seau", 80));

            // « auge » / « kuip » : mot entier uniquement (évite gauge ⊃ auge).
            if (HasWord(hay, "auge"))
                score = Math.Max(score, 90);
            if (HasWord(hay, "kuip") || hay.Contains("kuip", StringComparison.OrdinalIgnoreCase))
                score = Math.Max(score, 70);

            return score;
        }

        private static bool HasWord(string hay, string word) =>
            Regex.IsMatch(hay, $@"\b{Regex.Escape(word)}\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static int ScoreGloves(string hay)
        {
            // Exige un vrai marqueur gant — évite "bagues", etc.
            if (!ContainsAnyLocal(hay, "handschoen", "gant", "glove"))
                return 0;

            var score = 0;
            if (hay.Contains("handschoen", StringComparison.OrdinalIgnoreCase))
                score += 100;
            if (hay.Contains("werkhandschoen", StringComparison.OrdinalIgnoreCase))
                score += 30;
            if (hay.Contains("gant", StringComparison.OrdinalIgnoreCase))
                score += 40;
            if (hay.Contains("glove", StringComparison.OrdinalIgnoreCase))
                score += 30;

            // Gants isolés électriques : moins adaptés au malaxage ciment.
            if (ContainsAnyLocal(hay, "isolé", "isole", "insulated", "éléctrique", "electrique"))
                score -= 50;

            return score > 0 ? score : 0;
        }

        private static int ScoreAny(string hay, params (string Needle, int Score)[] scored)
        {
            var best = 0;
            foreach (var (needle, score) in scored)
            {
                if (hay.Contains(needle, StringComparison.OrdinalIgnoreCase) && score > best)
                    best = score;
            }

            return best;
        }

        private static bool ContainsAnyLocal(string hay, params string[] needles) =>
            needles.Any(n => hay.Contains(n, StringComparison.OrdinalIgnoreCase));

        private static bool IsBareConfirmation(string text)
        {
            var trimmed = Regex.Replace((text ?? string.Empty).Trim().ToLowerInvariant(), @"[!?.…]+$", "").Trim();
            trimmed = Regex.Replace(trimmed, @"\s+", " ");
            return trimmed is "ok" or "okay" or "oké" or "oke" or "oui" or "ouais" or "yes" or "go"
                or "d'accord" or "daccord" or "vas-y" or "vas y" or "vasy" or "merci"
                or "ok go" or "parfait" or "nickel";
        }

        private static string? ApplyBudgetFilter(
            List<StoreChatProductSuggestionDto> products,
            StoreChatSession session,
            ProductSearchFilter meta)
        {
            if (session.BudgetMax is not > 0 || products.Count == 0)
                return null;

            var budget = session.BudgetMax.Value;
            meta.MaxUnitPrice = budget;

            var over = products.Where(p => p.Price.HasValue && p.Price.Value > budget).ToList();
            var within = products.Where(p => !p.Price.HasValue || p.Price.Value <= budget).ToList();

            if (within.Count == 0)
            {
                // Garder la liste mais alerter : aucune reco unitaire dans le budget.
                return $"Alerte budget : aucune référence unitaire ≤ {budget:N2} €. "
                       + $"Les propositions affichées dépassent le budget unitaire.";
            }

            if (over.Count > 0)
            {
                products.Clear();
                products.AddRange(within);
                return $"Alerte budget : {over.Count} référence(s) exclue(s) car prix unitaire > {budget:N2} €.";
            }

            return null;
        }

        public async Task<StoreChatPaymentResultDto?> GetPaymentResultAsync(Guid orderId, CancellationToken ct = default)
        {
            var order = await _storage.SelectAllStoreChatOrders()
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.Id == orderId, ct);
            if (order == null)
                return null;

            return ToPaymentResult(order);
        }

        public async Task<StoreChatPaymentResultDto?> ConfirmPaymentAsync(
            Guid orderId,
            string? stripeSessionId,
            CancellationToken ct = default)
        {
            var order = await _storage.SelectAllStoreChatOrders()
                .FirstOrDefaultAsync(o => o.Id == orderId, ct);
            if (order == null)
                return null;

            if (string.Equals(order.Status, "paid", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(order.InvoicePdfBase64))
            {
                return ToPaymentResult(order);
            }

            var status = "paid";
            if (_stripe.IsEnabled)
            {
                var sid = stripeSessionId ?? order.StripeSessionId;
                if (!string.IsNullOrWhiteSpace(sid))
                {
                    var stripeStatus = await _stripe.GetCheckoutSessionStatusAsync(sid, ct);
                    if (!string.Equals(stripeStatus, "paid", StringComparison.OrdinalIgnoreCase))
                    {
                        order.Status = "pending";
                        await _storage.UpdateStoreChatOrderAsync(order);
                        return ToPaymentResult(order);
                    }
                }
                else if (!string.IsNullOrWhiteSpace(stripeSessionId))
                {
                    order.StripeSessionId = stripeSessionId;
                }
            }

            var items = DeserializeCart(order.LinesJson);
            var invoiceNumber = order.InvoiceNumber
                ?? $"FAC-{DateTime.UtcNow:yyyyMMdd}-{order.Id.ToString("N")[..6].ToUpperInvariant()}";
            var invoice = _pdf.GenerateInvoice(items, invoiceNumber, "Client magasin");

            order.Status = status;
            order.PaidAt = DateTime.UtcNow;
            order.InvoiceNumber = invoiceNumber;
            order.InvoicePdfBase64 = invoice.PdfBase64;
            order.InvoiceFileName = invoice.FileName;
            if (!string.IsNullOrWhiteSpace(stripeSessionId))
                order.StripeSessionId = stripeSessionId;

            await _storage.UpdateStoreChatOrderAsync(order);
            return ToPaymentResult(order);
        }

        private async Task<StoreChatResponseDto> CreateQuoteAsync(StoreChatSession session, CancellationToken ct)
        {
            if (session.Cart.Count == 0)
                return Ok(session, "Panier vide. Ajoutez des produits avant de demander un devis.", "NONE");

            var pdf = _pdf.GenerateQuote(session.Cart, "Client magasin");
            var quote = new StoreChatQuote
            {
                SessionId = session.SessionId,
                Number = $"DEV-{DateTime.UtcNow:yyyyMMddHHmmss}",
                TotalAmount = pdf.Total ?? 0,
                PdfBase64 = pdf.PdfBase64,
                FileName = pdf.FileName,
                LinesJson = JsonSerializer.Serialize(session.Cart, JsonOptions),
                SalesProjectId = session.ActiveSalesProjectId
            };
            await _storage.InsertStoreChatQuoteAsync(quote);
            _sessions.Save(session);

            var logistics = _logistics.Evaluate(session);
            var quoteReply = $"Devis prêt ({pdf.Total:N2} €). Vous pouvez le télécharger.";
            if (session.ActiveSalesProjectId is Guid pid)
                quoteReply += $"\nRattaché au projet {pid:D}.";
            quoteReply += "\n" + logistics.Summary;

            return new StoreChatResponseDto
            {
                SessionId = session.SessionId,
                ReplyText = quoteReply,
                HasAction = true,
                ActionType = "QUOTE_PDF",
                QuotePdf = pdf,
                ActiveProjectDomainId = session.ActiveProjectDomainId,
                ActiveProjectDomainLabel = session.ActiveProjectDomainLabel,
                SalesProjectId = session.ActiveSalesProjectId,
                Logistics = logistics
            };
        }

        private async Task<StoreChatResponseDto> CreateOrderAsync(StoreChatSession session, CancellationToken ct)
        {
            if (session.Cart.Count == 0)
                return Ok(session, "Panier vide. Ajoutez des produits avant de commander.", "NONE");

            var order = new StoreChatOrder
            {
                SessionId = session.SessionId,
                Status = "pending",
                TotalAmount = session.Cart.Sum(c => c.TotalPrice),
                LinesJson = JsonSerializer.Serialize(session.Cart, JsonOptions),
                SalesProjectId = session.ActiveSalesProjectId
            };
            await _storage.InsertStoreChatOrderAsync(order);
            session.LastOrderId = order.Id;

            if (_stripe.IsEnabled)
            {
                var link = await _stripe.CreateCheckoutAsync(order.Id, session.Cart, session.SessionId, ct);
                if (link != null)
                {
                    order.StripeSessionId = link.Source; // Stripe Checkout Session Id
                    await _storage.UpdateStoreChatOrderAsync(order);
                    _sessions.Save(session);

                    return new StoreChatResponseDto
                    {
                        SessionId = session.SessionId,
                        ReplyText = $"Commande créée ({order.TotalAmount:N2} €). Payez par carte pour finaliser.",
                        HasAction = true,
                        ActionType = "PAYMENT_LINK",
                        PaymentLink = new StoreChatPaymentLinkDto
                        {
                            Url = link.Url,
                            Amount = link.Amount,
                            Description = link.Description,
                            OrderId = link.OrderId,
                            Source = "stripe",
                            SourceLabel = link.SourceLabel
                        },
                        ActiveProjectDomainId = session.ActiveProjectDomainId,
                        ActiveProjectDomainLabel = session.ActiveProjectDomainLabel
                    };
                }
            }

            // Mode démo sans Stripe : confirme directement et génère facture.
            var confirmed = await ConfirmPaymentAsync(order.Id, null, ct);
            _sessions.Save(session);
            return new StoreChatResponseDto
            {
                SessionId = session.SessionId,
                ReplyText = "Commande enregistrée (mode démo sans Stripe). Facture disponible.",
                HasAction = true,
                ActionType = "INVOICE_PDF",
                QuotePdf = confirmed?.InvoicePdf,
                ActiveProjectDomainId = session.ActiveProjectDomainId,
                ActiveProjectDomainLabel = session.ActiveProjectDomainLabel
            };
        }

        private async Task<StoreChatResponseDto> ResetToNewProjectAsync(
            StoreChatSession session,
            CancellationToken ct)
        {
            var keepSessionId = session.SessionId;
            _sessions.Reset(keepSessionId);
            session = _sessions.GetOrCreate(keepSessionId);
            session.ActiveSalesProjectId = null;
            session.LastSuggestedProducts.Clear();
            _sessions.Save(session);
            await Task.CompletedTask;
            return Ok(session, "Nouveau projet démarré. Comment puis-je vous aider ?", "NONE");
        }

        private static bool IsNewProjectText(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;

            var lower = text.Trim().ToLowerInvariant();
            if (lower is "nouveau projet" or "new project" or "nieuw project" or "reset" or "recommencer")
                return true;

            return Regex.IsMatch(lower, @"^(bonjour[,!]?\s+)?(je\s+(veux|voudrais)\s+)?(démarrer|demarrer|commencer|lancer)?\s*(un\s+)?nouveau\s+projet\b")
                   || Regex.IsMatch(lower, @"\b(start|new)\s+project\b");
        }

        private async Task AddToCartAsync(StoreChatSession session, string? productId, decimal qty, CancellationToken ct)
        {
            if (!int.TryParse(productId, out var id) || qty <= 0)
                return;

            var product = await _storage.SelectAllErpProducts()
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == id, ct);
            if (product == null)
                return;

            var existing = session.Cart.FirstOrDefault(c => c.ErpProductId == id);
            if (existing != null)
            {
                existing.Quantity = qty;
                return;
            }

            session.Cart.Add(new StoreChatCartItem
            {
                ErpProductId = product.Id,
                Name = FormatProductDisplayName(product.Name, product.Name2, product.Reference, product.Id),
                Reference = product.Reference,
                Quantity = qty,
                UnitPrice = product.UnitPrice ?? product.PriceHT ?? 0
            });
        }

        private static void RemoveFromCart(StoreChatSession session, string? productId)
        {
            if (!int.TryParse(productId, out var id))
                return;
            session.Cart.RemoveAll(c => c.ErpProductId == id);
        }

        private async Task ReplaceCartFromTableAsync(
            StoreChatSession session,
            List<StoreChatTableCartLineDto>? lines,
            CancellationToken ct)
        {
            if (lines == null || lines.Count == 0)
                return;

            session.Cart.Clear();
            foreach (var line in lines)
                await AddToCartAsync(session, line.ProductId, line.Quantity <= 0 ? 1 : line.Quantity, ct);
        }

        // Estimations magasin (ordre de grandeur) pour un mur plein.
        private const decimal BricksPerM2 = 55m;
        private const decimal ParpaingsPerM2 = 12.5m;
        private const decimal MortarKgPerM2 = 30m;
        private const decimal DefaultBagKg = 25m;

        private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
        {
            "je", "tu", "il", "on", "nous", "vous", "les", "des", "une", "un", "de", "du", "la", "le",
            "et", "ou", "pour", "avec", "sans", "dans", "sur", "par", "pas", "plus", "très", "tres",
            "veux", "voudrais", "besoin", "cherche", "trouver", "acheter", "faire", "aide", "aider",
            "projet", "svp", "s'il", "sil", "vous", "plait", "ça", "ca", "est", "que", "qui", "quoi",
            "comme", "aussi", "donc", "alors", "mais", "mon", "ma", "mes", "ton", "votre", "notre",
            "mètre", "metre", "metres", "mètres", "cm", "mm", "haut", "haute", "hauteur", "longeur",
            "longueur", "large", "largeur", "de", "d", "l", "à", "a", "au", "aux", "construire", "mur",
            "produit", "produits", "marque", "ont", "avoir", "avez", "suis", "cherche", "rechercher",
            "voudrais", "souhaite", "souhaitez", "donne", "donner", "liste", "voir", "montre", "montrer",
            "est-ce", "quelque", "chose", "choses", "s'il", "svp", "merci", "bonjour", "salut",
            "nouveau", "nouvelle", "nouveaux", "nouvelles", "new", "nieuw", "nieuwe", "démarré", "demarre",
            "démarrer", "demarrer", "commencer", "start"
        };

        private static readonly Dictionary<string, string[]> MaterialSynonyms = new(StringComparer.OrdinalIgnoreCase)
        {
            // FR / NL / EN — les libellés ERP sont multilingues
            ["brique"] = new[]
            {
                "brique", "briques", "briquetage",
                "baksteen", "bakstenen", "snelbouwsteen", "snelbouwstenen", "metselsteen", "metselstenen",
                "brick", "bricks"
            },
            ["mortier"] = new[]
            {
                "mortier", "mortiers",
                "mortel", "metselmortel", "voegmortel", "lijmmortel",
                "mortar"
            },
            ["ciment"] = new[]
            {
                "ciment", "ciments", "ciment portland",
                "cement", "portlandcement"
            },
            // Éviter le mot seul "block" (trop de faux positifs).
            ["parpaing"] = new[]
            {
                "parpaing", "parpaings", "agglo", "aggloméré", "agglomere", "hourdis",
                "betonblok", "betonblokken", "snelbouwblok", "snelbouwblokken", "cellenblok", "cellenbeton",
                "kalkzandsteen", "ytong",
                "concrete block", "cinder block", "breeze block"
            },
            ["pierre"] = new[]
            {
                "pierre", "pierres", "moellon", "moellons",
                "natuursteen", "steen",
                "stone", "masonry"
            },
            ["ferraillage"] = new[]
            {
                "ferraille", "ferraillage", "armature", "treillis",
                "wapening", "betonijzer", "draadmat",
                "rebar", "reinforcement", "mesh"
            },
            ["sable"] = new[]
            {
                "sable", "gravier",
                "zand", "grind",
                "sand", "gravel"
            },
            ["carrelage"] = new[]
            {
                "carrelage", "carreau", "carreaux", "faïence", "faience",
                "tegel", "tegels", "vloertegel", "wandtegel",
                "tile", "tiles", "tiling"
            },
            ["peinture"] = new[]
            {
                "peinture", "peintures", "lasurer", "enduit",
                "verf", "muurverf", "latex", "beits",
                "paint", "coating", "plaster"
            },
        };

        private static readonly Dictionary<string, string[]> DomainSearchTerms = new(StringComparer.OrdinalIgnoreCase)
        {
            ["wall_construction"] = new[]
            {
                "parpaing", "brique", "mortier", "ciment", "agglo", "moellon",
                "baksteen", "snelbouwsteen", "betonblok", "mortel", "cement", "metselwerk",
                "brick", "mortar", "concrete block"
            },
            ["painting"] = new[]
            {
                "peinture", "rouleau", "enduit", "sous-couche",
                "verf", "muurverf", "kwast", "roller",
                "paint", "roller", "primer"
            },
            ["tiling"] = new[]
            {
                "carrelage", "colle", "joint", "carreau",
                "tegel", "tegellijm", "voegsel",
                "tile", "adhesive", "grout"
            },
            ["plumbing"] = new[]
            {
                "robinet", "tuyau", "siphon", "pvc",
                "kraan", "leiding", "buis",
                "tap", "pipe", "plumbing"
            },
            ["electrical"] = new[]
            {
                "prise", "interrupteur", "câble", "cable", "led",
                "stopcontact", "schakelaar", "draad",
                "socket", "switch", "wire"
            },
            ["garden_maintenance"] = new[]
            {
                "jardin", "tondeuse", "gazon", "haie",
                "tuin", "grasmaaier", "gazon", "haag",
                "garden", "lawnmower", "hedge"
            },
        };

        private static readonly string[] MasonryPositive = new[]
        {
            // FR
            "parpaing", "brique", "briques", "mortier", "ciment", "agglo", "agglom", "moellon",
            "hourdis", "maçon", "macon", "béton", "beton", "chaux", "sable", "gravier",
            // NL — matériaux (pas outils)
            "baksteen", "snelbouwsteen", "metselsteen", "betonblok", "snelbouwblok", "cellenblok",
            "cellenbeton", "kalkzandsteen", "lijmblok", "mortel", "metselmortel", "voegmortel", "cement",
            "metselwerk", "bouwmaterialen", "bouwmaterial", "zand", "grind", "wapening", "stenen",
            // EN
            "brick", "bricks", "mortar", "cement", "concrete block", "cinder block", "masonry",
            "sand", "gravel", "rebar"
        };

        private static readonly string[] MasonryNoise = new[]
        {
            "flexi", "schuur", "sandpaper", "duracell", "battery", "batterij", "trolley",
            "module", "affuter", "frees", "filter", "kool", "charbon", "bague", "blocage",
            "trowel floreffe", "humiblock", "hellico", "helico", "bijl", "monoblock", "monobloc",
            "schuurblok", "sanding block", "power block",
            // Outils qui citent baksteen/beton comme usage
            "boor voor", "boren voor", "zaagblad", "reciprozaag", "lijnspanner", "carbide tip",
            "set boren", "drill bit", "saw blade", "stuc primer", "stuc-primer", "primer"
        };

        private static readonly string[] ToolMarkers = new[]
        {
            "boor", "boren", "zaagblad", "zaagbladen", "recipro", "lijnspanner", "carbide",
            "drill", "spanner", "frees", "beitel", "hamer", "truelle", "trowel", "kwast",
            "roller", "primer", "schuur", "sandpaper", "set boren", "gereedschap"
        };

        private static readonly string[] MaterialUnitMarkers = new[]
        {
            "lijmblok", "kalkzandsteen", "betonblok", "snelbouwblok", "cellenblok", "cellenbeton",
            "baksteen", "snelbouwsteen", "metselsteen", "parpaing", "hourdis", "ytong",
            "mortel", "metselmortel", "voegmortel", "mortier", "ciment", "cement", "mortar",
            "brique", "brick", "concrete block", "moellon", "agglo"
        };

        private async Task<List<StoreChatProductSuggestionDto>> SearchProductsAsync(
            string text,
            StoreChatSession session,
            ProductSearchFilter meta,
            CancellationToken ct)
        {
            var brandMode = !string.IsNullOrWhiteSpace(meta.Brand);
            // Marque demandée → ne jamais basculer en mode mur (évite Silka/Coeck pour « Knauf ciment »).
            var wallMode = !brandMode
                && string.Equals(session.ActiveProjectDomainId, "wall_construction", StringComparison.OrdinalIgnoreCase);

            var terms = BuildSearchTerms(text, session, brandMode);
            var scores = new Dictionary<int, ScoredProduct>();

            if (brandMode)
            {
                await AccumulateBrandSearchAsync(scores, meta.Brand!, ct);
                foreach (var typeTerm in meta.TypeHints.SelectMany(ExpandTypeHintTerms).Distinct(StringComparer.OrdinalIgnoreCase).Take(12))
                    await AccumulateSearchTermAsync(scores, typeTerm, ct);
            }
            else
            {
                if (terms.Count == 0)
                    return new List<StoreChatProductSuggestionDto>();

                foreach (var term in terms.Take(24))
                    await AccumulateSearchTermAsync(scores, term, ct);

                if (wallMode)
                    await EnrichWallCatalogCandidatesAsync(scores, ct);
            }

            if (scores.Count == 0)
                return new List<StoreChatProductSuggestionDto>();

            IEnumerable<ScoredProduct> ranked = scores.Values;

            if (brandMode)
            {
                foreach (var p in scores.Values)
                    p.Score += BrandMatchBoost(p, meta.Brand!);

                var brandMatches = scores.Values
                    .Where(p => MatchesBrand(p, meta.Brand!))
                    .ToList();

                if (brandMatches.Count == 0)
                {
                    meta.Outcome = ProductSearchOutcome.BrandNotFound;
                    return new List<StoreChatProductSuggestionDto>();
                }

                if (meta.TypeHints.Count > 0)
                {
                    var typed = brandMatches
                        .Where(p => MatchesTypeHints(p, meta.TypeHints))
                        .ToList();

                    // Ciment : préférer les vrais ciments (pas colles/joints seuls).
                    if (meta.TypeHints.Any(t => t.Equals("ciment", StringComparison.OrdinalIgnoreCase)))
                    {
                        var strictCement = typed.Where(IsCementProduct).ToList();
                        if (strictCement.Count > 0)
                            typed = strictCement;
                    }

                    typed = typed
                        .OrderByDescending(p => p.Score + TypeExactBoost(p, meta.TypeHints))
                        .ThenBy(p => p.Name)
                        .ToList();

                    if (typed.Count > 0)
                    {
                        meta.Outcome = ProductSearchOutcome.BrandAndType;
                        ranked = typed;
                    }
                    else
                    {
                        meta.Outcome = ProductSearchOutcome.BrandWithoutType;
                        ranked = brandMatches
                            .OrderByDescending(p => p.Score)
                            .ThenBy(p => p.Name);
                    }
                }
                else
                {
                    meta.Outcome = ProductSearchOutcome.BrandOnly;
                    ranked = brandMatches
                        .OrderByDescending(p => p.Score)
                        .ThenBy(p => p.Name);
                }
            }
            else if (wallMode)
            {
                var classified = scores.Values
                    .Select(p =>
                    {
                        var kind = ClassifyWallProduct(p);
                        p.Score += MasonryBoost(p);
                        return (Product: p, Kind: kind);
                    })
                    .Where(x => x.Kind is WallProductKind.Block or WallProductKind.Brick or WallProductKind.Mortar)
                    .ToList();

                // Quotas : moins de variants Silka, plus de briques + ciment.
                var max = Math.Max(12, _options.MaxProductResults);
                var brickSlots = Math.Min(7, Math.Max(4, max / 3));
                var mortarSlots = Math.Min(6, Math.Max(4, max / 3));
                var blockSlots = Math.Max(4, max - brickSlots - mortarSlots);

                var blocks = DeduplicateWallVariants(
                        classified
                            .Where(x => x.Kind == WallProductKind.Block)
                            .OrderByDescending(x => x.Product.Score)
                            .ThenBy(x => x.Product.Name)
                            .Select(x => x.Product))
                    .Take(blockSlots)
                    .ToList();

                var bricks = DeduplicateWallVariants(
                        classified
                            .Where(x => x.Kind == WallProductKind.Brick)
                            .OrderByDescending(x => x.Product.Score)
                            .ThenBy(x => x.Product.Name)
                            .Select(x => x.Product))
                    .Take(brickSlots)
                    .ToList();

                var mortars = DeduplicateWallVariants(
                        classified
                            .Where(x => x.Kind == WallProductKind.Mortar)
                            .OrderByDescending(x => MortarPriority(x.Product))
                            .ThenByDescending(x => x.Product.Score)
                            .ThenBy(x => x.Product.Name)
                            .Select(x => x.Product))
                    .Take(mortarSlots)
                    .ToList();

                // Round-robin : 1 bloc, 1 brique, 1 mortier… (évite 5 Silka identiques en tête).
                ranked = InterleaveWallKinds(blocks, bricks, mortars);
                meta.Outcome = ProductSearchOutcome.Domain;
            }
            else
            {
                ranked = scores.Values
                    .OrderByDescending(p => p.Score)
                    .ThenBy(p => p.Name);
                meta.Outcome = ProductSearchOutcome.Generic;
            }

            var filtered = ranked.ToList();
            if (meta.WeightKg is > 0)
            {
                var byWeight = filtered.Where(p => MatchesWeightKg(p, meta.WeightKg.Value)).ToList();
                if (byWeight.Count > 0)
                {
                    filtered = byWeight;
                    meta.WeightApplied = true;
                }
                else
                {
                    meta.Outcome = ProductSearchOutcome.WeightNotFound;
                    meta.WeightApplied = true;
                    // Garder le contexte marque/type pour le message, sans polluer avec d'autres poids.
                    filtered = new List<ScoredProduct>();
                }
            }

            meta.TotalMatches = filtered.Count;
            // Marque : max 3. Mur : au moins 6 pour montrer blocs + briques + mortier.
            var take = brandMode
                ? Math.Max(1, Math.Min(3, _options.InitialProductResults > 0 ? _options.InitialProductResults : 3))
                : wallMode
                    ? Math.Max(6, Math.Min(9, Math.Max(_options.MaxProductResults, 6)))
                    : Math.Max(1, Math.Min(_options.MaxProductResults, Math.Max(3, _options.InitialProductResults)));

            return filtered
                .Take(take)
                .Select(p =>
                {
                    var kind = ClassifyWallProduct(p);
                    return new StoreChatProductSuggestionDto
                    {
                        ProductId = p.Id.ToString(CultureInfo.InvariantCulture),
                        Name = FormatProductDisplayName(p.Name, p.Name2, p.Reference, p.Id),
                        Price = p.UnitPrice ?? p.PriceHT,
                        Brand = p.Brand,
                        Category = string.Join(" / ", new[] { p.MainTypeName, p.TypeName, p.SubTypeName }
                            .Where(x => !string.IsNullOrWhiteSpace(x))),
                        SuggestedQuantity = wallMode
                            ? EstimateQuantityForKind(kind, p.Name, p.Name2, session.WallAreaM2)
                            : 1,
                        ImageUrl = BuildProductImageUrl(p.PicName)
                    };
                })
                .ToList();
        }

        private async Task AccumulateSearchTermAsync(
            Dictionary<int, ScoredProduct> scores,
            string term,
            CancellationToken ct)
        {
            var t = term;
            var rows = await _storage.SelectAllErpProducts()
                .AsNoTracking()
                .Where(p =>
                    (p.Name != null && p.Name.ToLower().Contains(t))
                    || (p.Name2 != null && p.Name2.ToLower().Contains(t))
                    || (p.Reference != null && p.Reference.ToLower().Contains(t))
                    || (p.Brand != null && p.Brand.ToLower().Contains(t))
                    || (p.TypeName != null && p.TypeName.ToLower().Contains(t))
                    || (p.SubTypeName != null && p.SubTypeName.ToLower().Contains(t))
                    || (p.MainTypeName != null && p.MainTypeName.ToLower().Contains(t)))
                .Take(80)
                .Select(p => new ScoredProduct
                {
                    Id = p.Id,
                    Name = p.Name,
                    Name2 = p.Name2,
                    Reference = p.Reference,
                    Brand = p.Brand,
                    UnitPrice = p.UnitPrice,
                    PriceHT = p.PriceHT,
                    MainTypeName = p.MainTypeName,
                    TypeName = p.TypeName,
                    SubTypeName = p.SubTypeName,
                    PicName = p.PicName,
                    Score = 1
                })
                .ToListAsync(ct);

            foreach (var p in rows)
                AddOrBumpScore(scores, p, t);
        }

        private async Task AccumulateBrandSearchAsync(
            Dictionary<int, ScoredProduct> scores,
            string brand,
            CancellationToken ct)
        {
            var b = brand.Trim().ToLowerInvariant();
            if (b.Length < 2)
                return;

            var rows = await _storage.SelectAllErpProducts()
                .AsNoTracking()
                .Where(p =>
                    (p.Brand != null && p.Brand.ToLower().Contains(b))
                    || (p.Manufacturer != null && p.Manufacturer.ToLower().Contains(b))
                    || (p.TypeName != null && p.TypeName.ToLower().Contains(b))
                    || (p.SubTypeName != null && p.SubTypeName.ToLower().Contains(b))
                    || (p.MainTypeName != null && p.MainTypeName.ToLower().Contains(b))
                    || (p.Name != null && p.Name.ToLower().Contains(b))
                    || (p.Name2 != null && p.Name2.ToLower().Contains(b)))
                .Take(200)
                .Select(p => new ScoredProduct
                {
                    Id = p.Id,
                    Name = p.Name,
                    Name2 = p.Name2,
                    Reference = p.Reference,
                    Brand = p.Brand,
                    UnitPrice = p.UnitPrice,
                    PriceHT = p.PriceHT,
                    MainTypeName = p.MainTypeName,
                    TypeName = p.TypeName,
                    SubTypeName = p.SubTypeName,
                    PicName = p.PicName,
                    Score = 5
                })
                .ToListAsync(ct);

            foreach (var p in rows)
                AddOrBumpScore(scores, p, b, bonus: 8);
        }

        /// <summary>
        /// Requêtes ciblées mur. Ne jamais filtrer SubTypeName avec "%bouw%" seul
        /// (inbouw/opbouw saturent le Take et masquent Snelbouwstenen).
        /// </summary>
        private async Task EnrichWallCatalogCandidatesAsync(
            Dictionary<int, ScoredProduct> scores,
            CancellationToken ct)
        {
            var brickRows = await _storage.SelectAllErpProducts()
                .AsNoTracking()
                .Where(p =>
                    (p.SubTypeName != null && (
                        p.SubTypeName.ToLower().Contains("snelbouw")
                        || p.SubTypeName.ToLower().Contains("baksteen")
                        || p.SubTypeName.ToLower().Contains("metselsteen")))
                    || (p.Name != null && (
                        p.Name.ToLower().Contains("snelbouw")
                        || p.Name.ToLower().Contains("porotherm")
                        || p.Name.ToLower().Contains("thermobrick")
                        || p.Name.ToLower().Contains("baksteen")
                        || p.Name.ToLower().Contains("boerkes")
                        || p.Name.ToLower().Contains("kalkzandsteen")
                        || p.Name.ToLower().Contains("lijmblok"))))
                .Take(100)
                .Select(p => new ScoredProduct
                {
                    Id = p.Id,
                    Name = p.Name,
                    Name2 = p.Name2,
                    Reference = p.Reference,
                    Brand = p.Brand,
                    UnitPrice = p.UnitPrice,
                    PriceHT = p.PriceHT,
                    MainTypeName = p.MainTypeName,
                    TypeName = p.TypeName,
                    SubTypeName = p.SubTypeName,
                    PicName = p.PicName,
                    Score = 3
                })
                .ToListAsync(ct);

            foreach (var p in brickRows)
            {
                if (IsExcludedWallNoise(p))
                    continue;
                var kind = ClassifyWallProduct(p);
                if (kind == WallProductKind.Brick)
                    AddOrBumpScore(scores, p, "baksteen", bonus: 10);
                else if (kind == WallProductKind.Block)
                    AddOrBumpScore(scores, p, "blok", bonus: 5);
            }

            var cementRows = await _storage.SelectAllErpProducts()
                .AsNoTracking()
                .Where(p =>
                    (p.SubTypeName != null
                        && (p.SubTypeName.ToLower().Contains("cement")
                            || p.SubTypeName.ToLower().Contains("mortel")
                            || p.SubTypeName.ToLower().Contains("snelcement"))
                        && !p.SubTypeName.ToLower().Contains("gips")
                        && !p.SubTypeName.ToLower().Contains("flexcement"))
                    || (p.TypeName != null && p.TypeName.ToLower().Contains("cement en mortel"))
                    || (p.Name != null && (
                        p.Name.ToLower().Contains("cement cem")
                        || p.Name.ToLower().Contains("snelcement")
                        || p.Name.ToLower().StartsWith("cement ")
                        || p.Name.ToLower().Contains("betonmortel")
                        || p.Name.ToLower().Contains("metselmortel")
                        || p.Name.ToLower().Contains("mixcement")
                        || p.Name.ToLower().Contains("gietmortel"))))
                .Take(80)
                .Select(p => new ScoredProduct
                {
                    Id = p.Id,
                    Name = p.Name,
                    Name2 = p.Name2,
                    Reference = p.Reference,
                    Brand = p.Brand,
                    UnitPrice = p.UnitPrice,
                    PriceHT = p.PriceHT,
                    MainTypeName = p.MainTypeName,
                    TypeName = p.TypeName,
                    SubTypeName = p.SubTypeName,
                    PicName = p.PicName,
                    Score = 3
                })
                .ToListAsync(ct);

            foreach (var p in cementRows)
            {
                if (IsExcludedWallNoise(p))
                    continue;
                if (ClassifyWallProduct(p) == WallProductKind.Mortar)
                    AddOrBumpScore(scores, p, "cement", bonus: 8);
            }
        }

        private static bool IsExcludedWallNoise(ScoredProduct p)
        {
            var hay = $"{p.Name} {p.Name2} {p.TypeName} {p.SubTypeName}".ToLowerInvariant();
            return ContainsAny(hay,
                "wastafel", "gootsteen", "inbouwdoos", "inbouwdozen", "opbouwdoos", "inbouwnis",
                "opbouwnis", "sifon", "lavabo", "tegelprofiel", "tegel ", "tegels", "cementino",
                "flexcement", "vezelcement", "cirkelzaag", "steendrager", "steenbeitel",
                "waarborgpallet", "leddle", "hydro inbouw", "afvoer", "natuursteenstrip",
                "aanrechtblad", "kantband", "underc anti",
                "snelgips", "gips ", "plamuur", "290ml", "drystone", " 1 kg", "1kg");
        }

        private static void AddOrBumpScore(
            Dictionary<int, ScoredProduct> scores,
            ScoredProduct p,
            string term,
            int bonus = 0)
        {
            var inProductName =
                (!string.IsNullOrEmpty(p.Name) && p.Name.Contains(term, StringComparison.OrdinalIgnoreCase))
                || (!string.IsNullOrEmpty(p.Name2) && p.Name2.Contains(term, StringComparison.OrdinalIgnoreCase));
            var hitScore = (inProductName ? 2 : 1) + bonus;

            if (scores.TryGetValue(p.Id, out var existing))
            {
                existing.Score += hitScore;
                if (string.IsNullOrWhiteSpace(existing.PicName) && !string.IsNullOrWhiteSpace(p.PicName))
                    existing.PicName = p.PicName;
            }
            else
            {
                p.Score = hitScore;
                scores[p.Id] = p;
            }
        }

        private static int MortarPriority(ScoredProduct p)
        {
            var name = (p.Name ?? string.Empty).ToLowerInvariant();
            var type = (p.TypeName ?? string.Empty).ToLowerInvariant();
            var sub = (p.SubTypeName ?? string.Empty).ToLowerInvariant();

            // Préférer sacs 20–25 kg (pas les mini 5 kg → qté 84).
            var bagBonus = 0;
            if (ContainsAny(name, "25kg", "25 kg", "20kg", "20 kg"))
                bagBonus = 10;
            else if (ContainsAny(name, "05kg", "5kg", "5 kg", "1kg", "1 kg", "10kg", "10 kg"))
                bagBonus = -20;

            if (type.Contains("cement en mortel") || sub.Contains("cement papieren") || sub.Contains("cement coeck")
                || sub.StartsWith("cement ") || name.Contains("cement cem") || name.Contains("snelcement")
                || name.StartsWith("cement "))
                return 30 + bagBonus;
            if (ContainsAny(name, "metselmortel", "mortel") || ContainsAny($"{p.Name2}", "metselmortel"))
                return 20 + bagBonus;
            if (ContainsAny(name, "voegmortel", "filler"))
                return 5 + bagBonus;
            return 10 + bagBonus;
        }

        private static List<ScoredProduct> InterleaveWallKinds(
            IReadOnlyList<ScoredProduct> blocks,
            IReadOnlyList<ScoredProduct> bricks,
            IReadOnlyList<ScoredProduct> mortars)
        {
            var result = new List<ScoredProduct>();
            var max = Math.Max(blocks.Count, Math.Max(bricks.Count, mortars.Count));
            for (var i = 0; i < max; i++)
            {
                if (i < blocks.Count)
                    result.Add(blocks[i]);
                if (i < bricks.Count)
                    result.Add(bricks[i]);
                if (i < mortars.Count)
                    result.Add(mortars[i]);
            }

            return result;
        }

        /// <summary>
        /// Une seule variante par famille (ex. 3 Silka 100/150/198 mm → garde le meilleur score).
        /// </summary>
        private static IEnumerable<ScoredProduct> DeduplicateWallVariants(IEnumerable<ScoredProduct> products)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in products)
            {
                var key = WallVariantFamilyKey(p);
                if (!seen.Add(key))
                    continue;
                yield return p;
            }
        }

        private static string WallVariantFamilyKey(ScoredProduct p)
        {
            var raw = $"{p.Brand} {p.Name}".ToLowerInvariant();
            // Retire dimensions / formats fréquents pour regrouper les variants.
            raw = Regex.Replace(raw, @"\d+\s*[x×*]\s*\d+(?:\s*[x×*]\s*\d+)?\s*(?:mm|cm|m)?", " ");
            raw = Regex.Replace(raw, @"\b\d+\s*(?:mm|cm)\b", " ");
            raw = Regex.Replace(raw, @"\s+", " ").Trim();
            if (raw.Length < 8)
                raw = $"{p.Brand}|{p.Id}";
            return raw;
        }

        private enum WallProductKind
        {
            Exclude,
            Block,
            Brick,
            Mortar,
            Other
        }

        /// <summary>
        /// Blocs/briques : type lu surtout dans Name.
        /// Mortier/ciment : Name OU Name2 (souvent le type métier est dans Name2).
        /// </summary>
        private static WallProductKind ClassifyWallProduct(ScoredProduct p)
        {
            var name = (p.Name ?? string.Empty).ToLowerInvariant();
            var name2 = (p.Name2 ?? string.Empty).ToLowerInvariant();
            var category = $"{p.MainTypeName} {p.TypeName} {p.SubTypeName}".ToLowerInvariant();

            if (IsToolProduct(p))
                return WallProductKind.Exclude;

            // Finitions / colles / étanchéité : hors structure mur.
            if (ContainsAny(name, "epoxy", "plamuur", "polyfilla", "varioflex", "tegellijm", "primer",
                    "coating", "kelder", "afwerkplamuur", "kit ", " silicon"))
                return WallProductKind.Exclude;
            if (ContainsAny(name, "verf", "paint", "latex") && !ContainsAny(name, "baksteen", "lijmblok"))
                return WallProductKind.Exclude;

            // Mortier / ciment d'abord (Name2 / catégorie "Cement en Mortels").
            if (IsMortarOrCementProduct(name, name2, category))
                return WallProductKind.Mortar;

            // Blocs structurels — intitulés produit, pas "… baksteen / cellenbeton" sur une lame.
            if (ContainsAny(name, "lijmblok", "kalkzandsteen", "snelbouwblok", "parpaing",
                    "hourdis", "ytong", "concrete block", "cinder block"))
                return WallProductKind.Block;

            if (ContainsAny(name, "betonblok", "cellenblok", "cellenbeton", "cementblok")
                && !ContainsAny(name, "zaag", "blad", "plug", "boor", "boren", "snijden", "recipro", "spanner"))
                return WallProductKind.Block;

            if (category.Contains("stenen")
                && ContainsAny(name, "blok")
                && !ContainsAny(name, "boor", "zaag", "blad", "plug", "voor ", "snijden")
                && !category.Contains("snelbouwsteen"))
                return WallProductKind.Block;

            // Briques — Name, Name2 ("holle baksteen") ou sous-type Snelbouwstenen.
            if (IsBrickProduct(name, name2, category))
                return WallProductKind.Brick;

            return WallProductKind.Other;
        }

        private static bool IsBrickProduct(string name, string name2, string category)
        {
            if (ContainsAny(name, "boor", "zaag", "blad", "plug", "fixations", "epoxy", "verf", "waarborgpallet",
                    "wastafel", "gootsteen", "steendrager", "steenbeitel"))
                return false;
            if (ContainsAny(category, "wastafel", "gootsteen", "inbouwdoos", "opbouwdoos", "inbouwnis", "sifon"))
                return false;

            if (ContainsAny(name, "snelbouwsteen", "snelbouw ", "metselsteen", "brique", "briquetage", "baksteen",
                    "thermobrick", "porotherm", "boerkes")
                || (name.Contains("brick") && !ContainsAny(name, "drill", "saw")))
                return true;

            // Sous-types prod (requête GROUP BY SubTypeName … %steen%/%bouw%).
            if (category.Contains("snelbouw") || category.Contains("baksteen") || category.Contains("metselsteen"))
                return true;
            if (category.Contains("steen") && !ContainsAny(category, "opbouw", "inbouw", "goot", "wastafel", "strip")
                && !ContainsAny(name, "profiel", "tegel", "werkblad"))
                return true;

            if (ContainsAny(name2, "holle baksteen", "volle baksteen", "metselbaksteen", "klassieke holle baksteen"))
                return true;

            if (name2.Contains("baksteen")
                && ContainsAny(name2, "waarmee je", "binnen als buiten", "holle ", "volle ", "klassieke")
                && !ContainsAny(name2, "ideaal voor gebruik in", "geschikt voor gebruik", "coating", "epoxy", "fixations"))
                return true;

            return false;
        }

        private static bool IsMortarOrCementProduct(string name, string name2, string category)
        {
            var hay = $"{name} {name2} {category}";

            if (ContainsAny(name, "epoxy", "plamuur", "polyfilla", "varioflex", "tegellijm", "primer",
                    "coating", "kelder", "cementino", "flexcement", "vezelcement", "cirkelzaag", "tegel"))
                return false;
            if (ContainsAny(hay, "cementblok", "cementering", "cementdekvloer", "cementgrijs - 290ml")
                && !ContainsAny(hay, "mortel", "mortier", "voegmortel", "metselmortel", "cement cem", "snelcement",
                    "betonmortel", "mixcement", "gietmortel"))
                return false;

            // Sous-types prod : Cement & Gips, Betonmortel, Rapolith Snelcement, Mixcement…
            if (ContainsAny(name, "snelgips", "gips"))
                return false;
            if (ContainsAny(name, "ml") && !ContainsAny(name, "kg"))
                return false;

            if (ContainsAny(category, "cement en mortel", "cement papieren", "cement coeck",
                    "cement wit", "rapolith snelcement", "betonmortel", "gietmortel", "mixcement", "snelcement"))
                return true;
            // "Cement & Gips" : garder ciment, exclure gips (déjà filtré sur Name).
            if (category.Contains("cement & gips") || (category.Contains("cement") && !category.Contains("cementino")))
                return !ContainsAny(name, "gips");
            if (category.Contains("mortel"))
                return true;

            if (ContainsAny(hay, "metselmortel", "voegmortel", "lijmmortel", "metserijmortel",
                    "betonmortel", "gietmortel", "mixcement", "mortier", "mortel", "mortar", "snelcement",
                    "portlandcement", "cement cem"))
                return true;

            if ((name.StartsWith("cement ") || ContainsAny(name, "ciment "))
                && ContainsAny(hay, "kg", "zak", "sac", "bag", "pallet")
                && !ContainsAny(name, "cementino", "sand "))
                return true;

            if (ContainsAny(name2, "portlandcement", "portlandklinker")
                && ContainsAny(hay, "kg", "cement")
                && !ContainsAny(name, "tegel", "epoxy"))
                return true;

            if (ContainsAny(name, "filler") && ContainsAny(name2, "voegmortel", "mortel", "metselmortel"))
                return true;

            return false;
        }

        private static string FormatProductDisplayName(string? name, string? name2, string? reference, int id)
        {
            var n1 = name?.Trim();
            var n2 = name2?.Trim();
            // Name2 long = fiche descriptive : on l’utilise pour la recherche, pas pour polluer l’UI.
            var name2IsLabel = !string.IsNullOrWhiteSpace(n2)
                               && n2!.Length <= 80
                               && !n2.Contains("wordt gebruikt", StringComparison.OrdinalIgnoreCase)
                               && !n2.Contains("geschikt voor", StringComparison.OrdinalIgnoreCase)
                               && !n2.Contains("toepasbaar", StringComparison.OrdinalIgnoreCase);

            if (!string.IsNullOrWhiteSpace(n1) && name2IsLabel
                && !string.Equals(n1, n2, StringComparison.OrdinalIgnoreCase))
                return $"{n1} — {n2}";
            if (!string.IsNullOrWhiteSpace(n1))
                return n1!;
            if (!string.IsNullOrWhiteSpace(n2))
                return n2!.Length <= 120 ? n2 : n2[..117] + "…";
            return reference ?? $"Produit {id}";
        }

        private static bool IsToolProduct(ScoredProduct p)
        {
            var name = (p.Name ?? string.Empty).ToLowerInvariant();
            var category = $"{p.MainTypeName} {p.TypeName} {p.SubTypeName}".ToLowerInvariant();

            // Contient (pas \b) : "reciprozaagbladen" est un seul mot.
            if (ContainsAny(name,
                    "zaagblad", "zaagbladen", "reciprozaag", "recipro", "cellenbetonzaag", "betonzaag",
                    "lijnspanner", "carbide", "drill bit", "cellenbetonplug", "betonplug",
                    "snijden", "boor voor", "set boren", "gereedschap"))
                return true;

            if (ContainsAny(name, "zaag", "boor", "boren", "plug", "blad")
                && ContainsAny(name, "baksteen", "cellenbeton", "cementblok", "betonblok", "beton"))
                return true;

            if (category.Contains("gereedschap") || category.Contains("lames") || category.Contains("tools"))
                return true;

            if (ToolMarkers.Any(m => name.Contains(m))
                && !ContainsAny(name, "lijmblok", "kalkzandsteen", "snelbouwsteen", "mortel", "cement "))
                return true;

            return false;
        }

        private static string ProductHaystack(ScoredProduct p) =>
            $"{p.Name} {p.Name2} {p.Brand} {p.MainTypeName} {p.TypeName} {p.SubTypeName}".ToLowerInvariant();

        private static int MasonryBoost(ScoredProduct p)
        {
            var kind = ClassifyWallProduct(p);
            var name = (p.Name ?? string.Empty).ToLowerInvariant();
            var boost = kind switch
            {
                WallProductKind.Block => 10,
                WallProductKind.Brick => 10,
                WallProductKind.Mortar => 6,
                _ => 0
            };
            if (name.Contains("lijmblok") || name.Contains("kalkzandsteen") || name.Contains("snelbouwsteen"))
                boost += 4;
            if (ContainsAny($"{p.Name2}", "metselmortel", "voegmortel", "lijmmortel", "portlandcement"))
                boost += 5;
            if ((p.MainTypeName ?? "").Contains("Stenen", StringComparison.OrdinalIgnoreCase)
                || (p.TypeName ?? "").Contains("Stenen", StringComparison.OrdinalIgnoreCase))
                boost += 3;
            return boost;
        }

        private sealed class ScoredProduct
        {
            public int Id { get; set; }
            public string? Name { get; set; }
            public string? Name2 { get; set; }
            public string? Reference { get; set; }
            public string? Brand { get; set; }
            public decimal? UnitPrice { get; set; }
            public decimal? PriceHT { get; set; }
            public string? MainTypeName { get; set; }
            public string? TypeName { get; set; }
            public string? SubTypeName { get; set; }
            public string? PicName { get; set; }
            public int Score { get; set; }
        }

        private string? BuildProductImageUrl(string? picName)
        {
            if (string.IsNullOrWhiteSpace(picName))
                return null;

            var file = picName.Trim().Replace('\\', '/');
            if (file.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || file.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return file;
            }

            var baseUrl = (_erpOptions.ImageBaseUrl ?? string.Empty).TrimEnd('/');
            if (string.IsNullOrWhiteSpace(baseUrl))
                return null;

            while (file.StartsWith('/'))
                file = file[1..];

            return $"{baseUrl}/{file}";
        }

        private static List<string> BuildSearchTerms(string text, StoreChatSession session, bool brandMode)
        {
            var terms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (!brandMode)
            {
                foreach (var hint in session.MaterialHints)
                    AddTermWithSynonyms(terms, hint);

                if (!string.IsNullOrWhiteSpace(session.ActiveProjectDomainId)
                    && DomainSearchTerms.TryGetValue(session.ActiveProjectDomainId, out var domainTerms))
                {
                    foreach (var t in domainTerms)
                        terms.Add(t);
                }
            }
            else if (!string.IsNullOrWhiteSpace(session.PreferredBrand))
            {
                terms.Add(session.PreferredBrand);
            }

            foreach (var raw in text.Split(new[] { ' ', ',', ';', '.', '!', '?', '/', '\\', '\n', '\t', '\'', '’' },
                         StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var token = raw.Trim().ToLowerInvariant();
                if (token.Length < 3 || StopWords.Contains(token) || token.Any(char.IsDigit))
                    continue;
                if (token is "bloc" or "block" or "blocks")
                    continue;
                AddTermWithSynonyms(terms, token);
            }

            terms.Remove("bloc");
            terms.Remove("block");
            terms.Remove("blocks");

            return terms.Take(24).ToList();
        }

        private static void AddTermWithSynonyms(HashSet<string> terms, string token)
        {
            terms.Add(token);
            foreach (var kv in MaterialSynonyms)
            {
                if (token.Contains(kv.Key, StringComparison.OrdinalIgnoreCase)
                    || kv.Value.Any(s => token.Contains(s, StringComparison.OrdinalIgnoreCase)))
                {
                    foreach (var s in kv.Value)
                        terms.Add(s);
                }
            }
        }

        private static void CollectMaterialHints(StoreChatSession session, string text)
        {
            var lower = text.ToLowerInvariant();
            var brandMode = !string.IsNullOrWhiteSpace(session.PreferredBrand);

            foreach (var kv in MaterialSynonyms)
            {
                if (kv.Value.Any(s => lower.Contains(s, StringComparison.OrdinalIgnoreCase))
                    || lower.Contains(kv.Key, StringComparison.OrdinalIgnoreCase))
                {
                    if (!session.MaterialHints.Contains(kv.Key, StringComparer.OrdinalIgnoreCase))
                        session.MaterialHints.Add(kv.Key);
                }
            }

            // Ne pas injecter automatiquement tout le kit mur si on cherche une marque.
            if (brandMode)
                return;

            if ((lower.Contains("construire") && lower.Contains("mur"))
                || string.Equals(session.ActiveProjectDomainId, "wall_construction", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var hint in new[] { "parpaing", "brique", "mortier", "ciment" })
                {
                    if (!session.MaterialHints.Contains(hint, StringComparer.OrdinalIgnoreCase))
                        session.MaterialHints.Add(hint);
                }
            }
        }

        private static void ParseWallDimensions(StoreChatSession session, string text)
        {
            var lower = text.ToLowerInvariant().Replace(',', '.');

            decimal? length = null;
            decimal? height = null;

            var lengthMatch = Regex.Match(lower, @"(?:longueur|longeur|long|lengte|length|l)\s*(?:de|:)?\s*(\d+(?:\.\d+)?)\s*(?:m|metres?|mètres?|mettres?)");
            var heightMatch = Regex.Match(lower, @"(?:hauteur|haut|hoogte|height|h)\s*(?:de|:)?\s*(\d+(?:\.\d+)?)\s*(?:m|metres?|mètres?|mettres?)");
            if (lengthMatch.Success && decimal.TryParse(lengthMatch.Groups[1].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var l1))
                length = l1;
            if (heightMatch.Success && decimal.TryParse(heightMatch.Groups[1].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var h1))
                height = h1;

            if (length is null || height is null)
            {
                var pair = Regex.Match(lower, @"(\d+(?:\.\d+)?)\s*(?:m|metres?|mètres?|mettres?)\D{0,32}(\d+(?:\.\d+)?)\s*(?:m|metres?|mètres?|mettres?)");
                if (pair.Success
                    && decimal.TryParse(pair.Groups[1].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var a)
                    && decimal.TryParse(pair.Groups[2].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var b))
                {
                    // Convention : première valeur = longueur, seconde = hauteur si non déjà trouvées.
                    length ??= a;
                    height ??= b;
                }
            }

            if (length is > 0)
                session.WallLengthM = length;
            if (height is > 0)
                session.WallHeightM = height;
        }

        private static void ApplySuggestedQuantities(
            List<StoreChatProductSuggestionDto> products,
            StoreChatSession session)
        {
            // Quantités déjà posées dans SearchProductsAsync (classification Name/Name2).
            // Garde-fou si un autre chemin crée des suggestions sans qté.
            var area = session.WallAreaM2;
            foreach (var p in products)
            {
                if (p.SuggestedQuantity is > 1)
                    continue;
                var nameOnly = (p.Name ?? string.Empty).Split('—')[0].Trim().ToLowerInvariant();
                if (ContainsAny(nameOnly, "lijmblok", "kalkzandsteen", "betonblok", "snelbouwblok"))
                    p.SuggestedQuantity = EstimateQuantityForKind(WallProductKind.Block, nameOnly, null, area);
                else if (ContainsAny(nameOnly, "baksteen", "snelbouwsteen", "brique", "brick"))
                    p.SuggestedQuantity = EstimateQuantityForKind(WallProductKind.Brick, nameOnly, null, area);
                else if (ContainsAny(nameOnly, "mortel", "mortier", "cement", "ciment", "filler", "voegmortel"))
                    p.SuggestedQuantity = EstimateQuantityForKind(WallProductKind.Mortar, nameOnly, p.Name, area);
            }
        }

        private static decimal EstimateQuantityForKind(
            WallProductKind kind,
            string? name,
            string? name2,
            decimal? areaM2)
        {
            if (areaM2 is null or <= 0)
                return 1;

            var area = areaM2.Value;
            return kind switch
            {
                WallProductKind.Block => Math.Max(1, Math.Ceiling(area * ParpaingsPerM2)),
                WallProductKind.Brick => Math.Max(1, Math.Ceiling(area * BricksPerM2)),
                WallProductKind.Mortar => EstimateMortarBags(name, name2, area),
                _ => 1
            };
        }

        private static decimal EstimateMortarBags(string? name, string? name2, decimal area)
        {
            var hay = $"{name} {name2}".ToLowerInvariant();
            var bagKg = TryParseBagKg(hay) ?? DefaultBagKg;
            return Math.Max(1, Math.Ceiling(area * MortarKgPerM2 / bagKg));
        }

        private static decimal? TryParseBagKg(string hay)
        {
            var m = Regex.Match(hay, @"(\d+(?:[.,]\d+)?)\s*kg");
            if (m.Success
                && decimal.TryParse(m.Groups[1].Value.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var kg)
                && kg > 0)
                return kg;
            return null;
        }

        private static bool ContainsAny(string hay, params string[] keys) =>
            keys.Any(k => hay.Contains(k, StringComparison.OrdinalIgnoreCase));

        private static string BuildCatalogContext(
            IReadOnlyList<StoreChatProductSuggestionDto> products,
            ProductSearchFilter meta)
        {
            if (products.Count == 0)
            {
                if (meta.Outcome == ProductSearchOutcome.BrandNotFound && !string.IsNullOrWhiteSpace(meta.Brand))
                    return $"(aucun produit de la marque {meta.Brand} dans le catalogue — dis-le clairement, n'invente rien)";

                if (meta.Outcome == ProductSearchOutcome.BrandWithoutType
                    && !string.IsNullOrWhiteSpace(meta.Brand)
                    && meta.TypeHints.Count > 0)
                {
                    return $"(la marque {meta.Brand} existe, mais aucun produit {string.Join('/', meta.TypeHints)} "
                           + "de cette marque — dis-le clairement, n'invente aucune gamme)";
                }

                return "(aucun produit trouvé dans le catalogue local — ne propose aucun produit inventé; demande un autre mot-clé marque/type)";
            }

            var header = meta.Outcome switch
            {
                ProductSearchOutcome.BrandAndType =>
                    $"Produits {meta.Brand} correspondant à la demande ({string.Join('/', meta.TypeHints)}) — UNIQUEMENT ceux-ci:\n",
                ProductSearchOutcome.BrandWithoutType =>
                    $"Produits marque {meta.Brand} (aucun {string.Join('/', meta.TypeHints)} Knauf/marque trouvé) — UNIQUEMENT ceux-ci:\n"
                        .Replace("Knauf/marque", meta.Brand ?? "marque"),
                ProductSearchOutcome.BrandOnly =>
                    $"Produits marque {meta.Brand} — UNIQUEMENT ceux-ci:\n",
                _ => "Produits catalogue à recommander UNIQUEMENT (quantités déjà estimées):\n"
            };

            return header
                   + string.Join("\n", products.Take(12).Select(p =>
                       $"- [{p.ProductId}] {p.Name} | qté estimée {p.SuggestedQuantity:0} | {p.Brand} | {p.Category} | {p.Price:N2} €"));
        }

        private static string BuildUserFacingReply(
            string? aiReply,
            IReadOnlyList<StoreChatProductSuggestionDto> products,
            StoreChatSession session,
            ProductSearchFilter meta)
        {
            var calc = BuildCalculationSummary(session);
            var brand = meta.Brand;
            var typeLabel = meta.TypeHints.Count > 0 ? string.Join(" / ", meta.TypeHints) : null;
            var weightLabel = meta.WeightKg is > 0 ? $"{meta.WeightKg:0.##} kg" : null;

            if (meta.Outcome == ProductSearchOutcome.WeightNotFound)
            {
                return $"Je n'ai pas trouvé de"
                       + (typeLabel != null ? $" {typeLabel}" : " produit")
                       + (brand != null ? $" {brand}" : "")
                       + (weightLabel != null ? $" en {weightLabel}" : "")
                       + " dans le catalogue."
                       + (brand != null && typeLabel != null
                           ? $" Souhaitez-vous voir d'autres formats {brand} ({typeLabel}) ?"
                           : " Affinez marque, type ou poids.");
            }

            if (products.Count > 0)
            {
                string intro;
                if (!string.IsNullOrWhiteSpace(calc) && meta.Outcome is ProductSearchOutcome.Domain)
                {
                    intro = calc;
                }
                else if (meta.IsYesNoBrandQuestion
                         && meta.Outcome is ProductSearchOutcome.BrandAndType or ProductSearchOutcome.BrandOnly)
                {
                    var samples = string.Join(", ", products.Take(2).Select(p => p.Name));
                    intro = $"Oui. {brand} propose "
                            + (typeLabel != null ? $"du {typeLabel} " : "ces produits ")
                            + "dans notre catalogue"
                            + (samples.Length > 0 ? $", notamment : {samples}." : ".");

                    if (meta.WeightKg is null or <= 0)
                        intro += "\n\nCherchez-vous un petit format (ex. 5 kg) ou un sac chantier (25 kg) ?";
                }
                else if (meta.Outcome == ProductSearchOutcome.BrandAndType)
                {
                    intro = weightLabel != null
                        ? $"Voici {products.Count} référence(s) {brand} — {typeLabel} — {weightLabel}."
                        : $"Voici {products.Count} référence(s) {brand} liées à « {typeLabel} ».";
                }
                else if (meta.Outcome == ProductSearchOutcome.BrandWithoutType)
                {
                    intro = $"Je n'ai pas trouvé de {typeLabel} de la marque {brand} dans le catalogue. "
                            + $"Voici d'autres produits {brand} :";
                }
                else if (meta.Outcome == ProductSearchOutcome.BrandOnly)
                {
                    intro = weightLabel != null
                        ? $"Voici {products.Count} produit(s) {brand} en {weightLabel}."
                        : $"Voici {products.Count} produit(s) de la marque {brand}.";
                }
                else
                {
                    intro = $"Voici {products.Count} produit(s) du catalogue"
                            + (string.IsNullOrWhiteSpace(session.ActiveProjectDomainLabel)
                                ? "."
                                : $" pour {session.ActiveProjectDomainLabel}.");
                }

                if (meta.TotalMatches > products.Count)
                    intro += $"\n(Affichage des {products.Count} meilleures sur {meta.TotalMatches} — précisez pour affiner.)";

                var isBrandPath = meta.Outcome is ProductSearchOutcome.BrandOnly
                    or ProductSearchOutcome.BrandAndType
                    or ProductSearchOutcome.BrandWithoutType
                    || meta.IsYesNoBrandQuestion;

                if (isBrandPath)
                {
                    if (meta.IsYesNoBrandQuestion)
                    {
                        // La question de suivi (poids) est déjà dans l'intro si besoin.
                        return intro.Trim();
                    }

                    if (meta.WeightKg is null or <= 0
                        && meta.Outcome is ProductSearchOutcome.BrandAndType or ProductSearchOutcome.BrandOnly)
                    {
                        intro += "\n\nCherchez-vous un petit format (ex. 5 kg) ou un sac chantier (25 kg) ?";
                    }
                    else
                    {
                        intro += "\n\nAjustez les quantités puis ajoutez au panier / devis / commande.";
                    }

                    return intro.Trim();
                }

                intro += "\n\nAjustez les quantités puis ajoutez au panier / devis / commande.";

                if (!string.IsNullOrWhiteSpace(aiReply)
                    && aiReply!.Length < 400
                    && !LooksLikeInventedProductList(aiReply)
                    && !LooksLikeHallucinatedBrandClaim(aiReply, products, brand))
                {
                    intro = (!string.IsNullOrWhiteSpace(calc) ? calc + "\n\n" : "")
                            + aiReply.Trim()
                            + "\n\nLes quantités proposées sont préremplies dans le tableau.";
                }

                return intro.Trim();
            }

            if (meta.Outcome == ProductSearchOutcome.BrandNotFound && !string.IsNullOrWhiteSpace(brand))
            {
                return $"Je n'ai trouvé aucun produit de la marque {brand} dans le catalogue. "
                       + "Vérifiez l'orthographe ou essayez une autre marque / un type de produit.";
            }

            if (meta.Outcome == ProductSearchOutcome.BrandWithoutType
                && !string.IsNullOrWhiteSpace(brand)
                && typeLabel != null)
            {
                return $"La marque {brand} est présente, mais je n'ai pas de {typeLabel} {brand} dans le catalogue. "
                       + "Précisez un autre type (plâtre, plaque, colle…) ou une autre marque.";
            }

            if (!string.IsNullOrWhiteSpace(calc))
                return calc + "\n\nJe n'ai pas trouvé de parpaings/briques/mortier/ciment correspondants dans le catalogue. Affinez avec un matériau précis.";

            if (!string.IsNullOrWhiteSpace(aiReply) && !LooksLikeInventedProductList(aiReply))
                return aiReply!.Trim();

            return "Je n'ai pas trouvé de produit correspondant dans le catalogue. "
                   + "Indiquez un matériau ou une marque précise (ex. Knauf, parpaing, brique, mortier, ciment).";
        }

        private static string BuildCalculationSummary(StoreChatSession session)
        {
            if (session.WallLengthM is not > 0 || session.WallHeightM is not > 0 || session.WallAreaM2 is not > 0)
                return string.Empty;

            var area = session.WallAreaM2!.Value;
            var bricks = Math.Ceiling(area * BricksPerM2);
            var parpaings = Math.Ceiling(area * ParpaingsPerM2);
            var mortarBags = Math.Ceiling(area * MortarKgPerM2 / DefaultBagKg);

            return $"Mur {session.WallLengthM:0.##} m × {session.WallHeightM:0.##} m → surface ≈ {area:0.##} m².\n"
                   + $"Estimations (ordre de grandeur) : ~{bricks:0} briques, ou ~{parpaings:0} parpaings, "
                   + $"et ~{mortarBags:0} sac(s) de mortier/ciment ({DefaultBagKg:0} kg).";
        }

        private static bool LooksLikeInventedProductList(string reply)
        {
            var lower = reply.ToLowerInvariant();
            return lower.Contains("voici quelques suggestions")
                   || lower.Contains("griffes pour murs")
                   || lower.Contains("griffe pour murs")
                   || lower.Contains("gamme de produits qui incluent")
                   || lower.Contains("ciments pour plâtre")
                   || lower.Contains("ciments pour mortier")
                   || lower.Contains("ciments pour béton")
                   || lower.Contains("ciments pour beton")
                   || (lower.Contains("matériaux suivants") && lower.Contains("*"))
                   || (lower.Contains("tels que") && lower.Contains("*"));
        }

        private static bool LooksLikeHallucinatedBrandClaim(
            string reply,
            IReadOnlyList<StoreChatProductSuggestionDto> products,
            string? brand)
        {
            if (string.IsNullOrWhiteSpace(brand))
                return false;

            var lower = reply.ToLowerInvariant();
            var brandLower = brand.ToLowerInvariant();
            if (!lower.Contains(brandLower))
                return false;

            // L'IA affirme une offre marque + produit alors que la liste ne le confirme pas.
            if ((lower.Contains("ciment") || lower.Contains("cement"))
                && products.All(p => !MatchesTypeHints(
                    new ScoredProduct
                    {
                        Name = p.Name,
                        Brand = p.Brand,
                        MainTypeName = p.Category,
                        TypeName = p.Category,
                        SubTypeName = p.Category
                    },
                    new List<string> { "ciment" })))
            {
                return true;
            }

            return false;
        }

        /// <summary>Typos fréquents seulement — les vraies marques viennent de ErpBrands.</summary>
        private static readonly Dictionary<string, string> BrandTypoAliases = new(StringComparer.OrdinalIgnoreCase)
        {
            ["knau"] = "Knauf",
            ["knauff"] = "Knauf",
            ["silka"] = "Silka",
            ["poroterm"] = "Porotherm",
        };

        private static List<string>? _cachedBrandNames;
        private static DateTime _cachedBrandNamesAtUtc = DateTime.MinValue;
        private static readonly TimeSpan BrandCacheTtl = TimeSpan.FromMinutes(10);

        private static bool IsExplicitWallIntent(string text)
        {
            var lower = text.ToLowerInvariant();
            return lower.Contains("construire un mur")
                   || lower.Contains("construction de mur")
                   || lower.Contains("mur de séparation")
                   || lower.Contains("mur de separation")
                   || lower.Contains("faire un mur")
                   || lower.Contains("monter un mur")
                   || lower.Contains("muur bouwen")
                   || lower.Contains("build a wall")
                   || lower.Contains("brick wall")
                   || (lower.Contains("mur") && (lower.Contains("construire") || Regex.IsMatch(lower, @"\d+\s*m")));
        }

        private async Task DetectBrandAsync(StoreChatSession session, string text, CancellationToken ct)
        {
            var lower = text.ToLowerInvariant();
            var brands = await GetCatalogBrandNamesAsync(ct);
            if (brands.Count == 0)
                return;

            // 1) Correspondance exacte (mot entier), marques les plus longues d'abord
            //    (évite qu'un sous-mot court gagne sur une marque composée).
            foreach (var brand in brands.OrderByDescending(b => b.Length))
            {
                var needle = brand.ToLowerInvariant();
                if (needle.Length < 2)
                    continue;

                if (Regex.IsMatch(lower, $@"\b{Regex.Escape(needle)}\b", RegexOptions.IgnoreCase)
                    || lower.Contains(needle, StringComparison.OrdinalIgnoreCase))
                {
                    session.PreferredBrand = brand;
                    return;
                }
            }

            // 2) Typos connus → marque catalogue si elle existe
            foreach (var alias in BrandTypoAliases.OrderByDescending(kv => kv.Key.Length))
            {
                if (!Regex.IsMatch(lower, $@"\b{Regex.Escape(alias.Key)}\b", RegexOptions.IgnoreCase)
                    && !lower.Contains(alias.Key, StringComparison.OrdinalIgnoreCase))
                    continue;

                var resolved = brands.FirstOrDefault(b =>
                    b.Equals(alias.Value, StringComparison.OrdinalIgnoreCase)
                    || b.StartsWith(alias.Value, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrWhiteSpace(resolved))
                {
                    session.PreferredBrand = resolved;
                    return;
                }
            }

            // 3) Préfixe : « knau » → Knauf si une seule marque catalogue commence ainsi
            var tokens = Regex.Matches(lower, @"[a-z0-9][\w-]{2,}")
                .Select(m => m.Value)
                .Where(t => !StopWords.Contains(t) && !MaterialSynonyms.ContainsKey(t))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var token in tokens.OrderByDescending(t => t.Length))
            {
                var startsWith = brands
                    .Where(b => b.StartsWith(token, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                if (startsWith.Count == 1)
                {
                    session.PreferredBrand = startsWith[0];
                    return;
                }
            }

            // 4) « marque X »
            var marqueMatch = Regex.Match(lower, @"\b(?:marque|brand)\s+([a-z0-9][\w-]{2,})\b");
            if (marqueMatch.Success)
            {
                var token = marqueMatch.Groups[1].Value;
                var hit = brands.FirstOrDefault(b =>
                    b.Equals(token, StringComparison.OrdinalIgnoreCase)
                    || b.StartsWith(token, StringComparison.OrdinalIgnoreCase)
                    || b.Contains(token, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrWhiteSpace(hit))
                    session.PreferredBrand = hit;
            }
        }

        private async Task<List<string>> GetCatalogBrandNamesAsync(CancellationToken ct)
        {
            if (_cachedBrandNames != null
                && DateTime.UtcNow - _cachedBrandNamesAtUtc < BrandCacheTtl)
            {
                return _cachedBrandNames;
            }

            var fromTable = await _storage.SelectAllErpBrands()
                .AsNoTracking()
                .Where(b => b.IsActive && b.Name != null && b.Name != "")
                .Select(b => b.Name)
                .ToListAsync(ct);

            List<string> names;
            if (fromTable.Count > 0)
            {
                names = fromTable
                    .Select(n => n.Trim())
                    .Where(n => n.Length >= 2)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderByDescending(n => n.Length)
                    .ToList();
            }
            else
            {
                // Fallback si ErpBrands pas encore reconstruit
                names = await _storage.SelectAllErpProducts()
                    .AsNoTracking()
                    .Where(p => p.Brand != null && p.Brand != "")
                    .Select(p => p.Brand!)
                    .Distinct()
                    .ToListAsync(ct);

                names = names
                    .Select(n => n.Trim())
                    .Where(n => n.Length >= 2)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderByDescending(n => n.Length)
                    .ToList();
            }

            _cachedBrandNames = names;
            _cachedBrandNamesAtUtc = DateTime.UtcNow;
            return names;
        }

        private static void UpdateStickySearchFilters(StoreChatSession session, string text)
        {
            var weight = ParseWeightKgFromText(text);
            if (weight is > 0)
                session.PreferredWeightKg = weight;

            foreach (var hint in ExtractTypeHints(text))
            {
                if (!session.SearchTypeHints.Contains(hint, StringComparer.OrdinalIgnoreCase))
                    session.SearchTypeHints.Add(hint);
            }
        }

        private static ProductSearchFilter BuildSearchMeta(StoreChatSession session, string text)
        {
            var fromText = ExtractTypeHints(text);
            var types = fromText.Count > 0
                ? fromText
                : session.SearchTypeHints.ToList();

            return new ProductSearchFilter
            {
                Brand = session.PreferredBrand,
                Categories = types,
                WeightKg = ParseWeightKgFromText(text) ?? session.PreferredWeightKg,
                IsYesNoBrandQuestion = IsYesNoBrandQuestion(text)
            };
        }

        private static bool IsYesNoBrandQuestion(string text)
        {
            var lower = text.ToLowerInvariant();
            return lower.Contains("est-ce que")
                   || lower.Contains("est ce que")
                   || lower.Contains("avez-vous")
                   || lower.Contains("a-t-il")
                   || lower.Contains("a t il")
                   || Regex.IsMatch(lower, @"\b(ont|a)\s+du\b")
                   || lower.Contains("propose") && (lower.Contains("?") || lower.Contains("ciment") || lower.Contains("produit"));
        }

        private static decimal? ParseWeightKgFromText(string text)
        {
            var lower = text.ToLowerInvariant().Replace(',', '.');
            var m = Regex.Match(lower, @"(\d+(?:\.\d+)?)\s*kg");
            if (m.Success
                && decimal.TryParse(m.Groups[1].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var kg)
                && kg > 0)
                return kg;

            // « sacs de 25 » / « sac 25 »
            m = Regex.Match(lower, @"\b(?:sacs?|zakken?|bags?)\s*(?:de|van|of)?\s*(\d+(?:\.\d+)?)\b");
            if (m.Success
                && decimal.TryParse(m.Groups[1].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var sac)
                && sac >= 1 && sac <= 100)
                return sac;

            return null;
        }

        private static bool MatchesWeightKg(ScoredProduct p, decimal kg)
        {
            var hay = $"{p.Name} {p.Name2}";
            var parsed = TryParseBagKg(hay);
            if (parsed.HasValue)
                return Math.Abs(parsed.Value - kg) < 0.05m;

            // Formats fréquents sans espace : 25kg, 25KG
            var token = kg == Math.Truncate(kg)
                ? ((int)kg).ToString(CultureInfo.InvariantCulture)
                : kg.ToString("0.##", CultureInfo.InvariantCulture);
            return Regex.IsMatch(hay, $@"\b{Regex.Escape(token)}\s*kg\b", RegexOptions.IgnoreCase)
                   || hay.Contains(token + "kg", StringComparison.OrdinalIgnoreCase)
                   || hay.Contains(token + " kg", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsCementProduct(ScoredProduct p)
        {
            var hay = $"{p.Name} {p.Name2} {p.TypeName} {p.SubTypeName}".ToLowerInvariant();
            return ContainsAny(hay, "cement", "ciment", "portlandcement")
                   && !ContainsAny(hay, "tegellijm", "voegmortel", "voegen", "lijm ", "blokkenlijm");
        }

        private static int TypeExactBoost(ScoredProduct p, IReadOnlyList<string> typeHints)
        {
            if (typeHints.Any(t => t.Equals("ciment", StringComparison.OrdinalIgnoreCase)) && IsCementProduct(p))
                return 30;
            return MatchesTypeHints(p, typeHints) ? 5 : 0;
        }

        private static List<string> ExtractTypeHints(string text)
        {
            var lower = text.ToLowerInvariant();
            var hints = new List<string>();
            foreach (var kv in MaterialSynonyms)
            {
                if (kv.Value.Any(s => lower.Contains(s, StringComparison.OrdinalIgnoreCase))
                    || lower.Contains(kv.Key, StringComparison.OrdinalIgnoreCase))
                {
                    hints.Add(kv.Key);
                }
            }

            return hints;
        }

        private static IEnumerable<string> ExpandTypeHintTerms(string hint)
        {
            yield return hint;
            if (MaterialSynonyms.TryGetValue(hint, out var syns))
            {
                foreach (var s in syns)
                    yield return s;
            }
        }

        private static bool MatchesBrand(ScoredProduct p, string brand)
        {
            var b = brand.Trim();
            if (b.Length == 0)
                return false;

            return ContainsIgnoreCase(p.Brand, b)
                   || ContainsIgnoreCase(p.Name, b)
                   || ContainsIgnoreCase(p.Name2, b)
                   || ContainsIgnoreCase(p.TypeName, b)
                   || ContainsIgnoreCase(p.SubTypeName, b)
                   || ContainsIgnoreCase(p.MainTypeName, b);
        }

        private static int BrandMatchBoost(ScoredProduct p, string brand)
        {
            if (ContainsIgnoreCase(p.Brand, brand))
                return 20;
            if (ContainsIgnoreCase(p.TypeName, brand) || ContainsIgnoreCase(p.SubTypeName, brand))
                return 12;
            if (ContainsIgnoreCase(p.Name, brand))
                return 8;
            return 0;
        }

        private static bool MatchesTypeHints(ScoredProduct p, IReadOnlyList<string> typeHints)
        {
            if (typeHints.Count == 0)
                return true;

            var hay = $"{p.Name} {p.Name2} {p.Brand} {p.MainTypeName} {p.TypeName} {p.SubTypeName}"
                .ToLowerInvariant();

            foreach (var hint in typeHints)
            {
                var keys = ExpandTypeHintTerms(hint).Select(x => x.ToLowerInvariant()).ToList();
                if (keys.Any(k => hay.Contains(k, StringComparison.OrdinalIgnoreCase)))
                    return true;
            }

            return false;
        }

        private static bool ContainsIgnoreCase(string? hay, string needle) =>
            !string.IsNullOrWhiteSpace(hay)
            && hay.Contains(needle, StringComparison.OrdinalIgnoreCase);

        private static void DetectDomain(StoreChatSession session, string text)
        {
            var lower = text.ToLowerInvariant();
            var brandQuery = !string.IsNullOrWhiteSpace(session.PreferredBrand);

            // Intention mur EXPLICITE uniquement (pas « ciment » / « brique » seuls —
            // sinon « Knauf ciment » bascule à tort sur Construction de mur).
            (string id, string label, string[] keys)[] domains =
            {
                ("wall_construction", "Construction de mur", new[]
                {
                    "construire un mur", "construction de mur", "mur de séparation", "mur de separation",
                    "mur de soutènement", "mur de souteinement", "briquetage", "maçonner un mur", "maconner un mur",
                    "muur bouwen", "metselwerk", "build a wall", "brick wall", "masonry wall",
                    "je construis un mur", "faire un mur", "monter un mur"
                }),
                ("painting", "Peinture", new[] { "peinture", "peindre", "rouleau à peindre", "sous-couche", "lasurer" }),
                ("tiling", "Carrelage", new[] { "carrelage", "carreau", "faïence", "faience" }),
                ("plumbing", "Plomberie", new[] { "plomberie", "robinet", "tuyau", "wc", "siphon" }),
                ("electrical", "Électricité", new[] { "électri", "electri", "prise", "interrupteur", "câble", "cable", "led" }),
                ("garden_maintenance", "Entretien jardin", new[] { "jardin", "tondeuse", "haie", "gazon" }),
            };

            foreach (var d in domains)
            {
                if (d.keys.Any(k => lower.Contains(k)))
                {
                    // Recherche marque + matériau : ne pas écraser avec un autre domaine.
                    if (brandQuery && d.id == "wall_construction")
                        continue;

                    session.ActiveProjectDomainId = d.id;
                    session.ActiveProjectDomainLabel = d.label;
                    return;
                }
            }

            // Fallback : "mur" + dimensions / construire → maçonnerie (sauf requête marque)
            if (!brandQuery
                && lower.Contains("mur")
                && (lower.Contains("construire") || Regex.IsMatch(lower, @"\d+\s*m")))
            {
                session.ActiveProjectDomainId = "wall_construction";
                session.ActiveProjectDomainLabel = "Construction de mur";
            }
        }

        private void TrimHistory(StoreChatSession session)
        {
            var limit = Math.Max(4, _options.ChatHistoryLimit);
            if (session.History.Count > limit)
                session.History = session.History.TakeLast(limit).ToList();
        }

        private static StoreChatResponseDto Ok(StoreChatSession session, string text, string actionType) => new()
        {
            SessionId = session.SessionId,
            ReplyText = text,
            HasAction = !string.Equals(actionType, "NONE", StringComparison.OrdinalIgnoreCase),
            ActionType = actionType,
            ActiveProjectDomainId = session.ActiveProjectDomainId,
            ActiveProjectDomainLabel = session.ActiveProjectDomainLabel
        };

        private static StoreChatPaymentResultDto ToPaymentResult(StoreChatOrder order) => new()
        {
            Status = order.Status,
            OrderId = order.Id.ToString("D"),
            InvoiceNumber = order.InvoiceNumber,
            InvoicePdf = string.IsNullOrWhiteSpace(order.InvoicePdfBase64)
                ? null
                : new StoreChatQuotePdfDto
                {
                    PdfBase64 = order.InvoicePdfBase64!,
                    FileName = order.InvoiceFileName ?? $"facture-{order.InvoiceNumber}.pdf",
                    Total = order.TotalAmount,
                    Source = "invoice",
                    SourceLabel = "Facture"
                },
            SuggestNewProject = true,
            Source = "store-chat",
            SourceLabel = "Assistant magasin"
        };

        private static List<StoreChatCartItem> DeserializeCart(string json)
        {
            try
            {
                return JsonSerializer.Deserialize<List<StoreChatCartItem>>(json, JsonOptions)
                       ?? new List<StoreChatCartItem>();
            }
            catch
            {
                return new List<StoreChatCartItem>();
            }
        }
    }
}
