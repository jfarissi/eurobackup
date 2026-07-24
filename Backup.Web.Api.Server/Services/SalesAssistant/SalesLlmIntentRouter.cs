using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Services.StoreChat;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Backup.Web.Api.Server.Services.SalesAssistant
{
    public interface ISalesLlmIntentRouter
    {
        /// <summary>
        /// Null si désactivé, LLM down, JSON invalide, ou action hors whitelist guard.
        /// </summary>
        Task<SalesLlmRouterDecision?> TryDecideAsync(
            StoreChatSession session,
            string userText,
            CancellationToken ct = default);
    }

    /// <summary>
    /// Classifieur d'intention LLM : choisit une action parmi une liste fermée.
    /// N'exécute rien (panier / prix / commande restent C#).
    /// </summary>
    public sealed class SalesLlmIntentRouter : ISalesLlmIntentRouter
    {
        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        private readonly IStoreChatAiClient _ai;
        private readonly StoreChatOptions _options;
        private readonly ILogger<SalesLlmIntentRouter> _logger;

        public SalesLlmIntentRouter(
            IStoreChatAiClient ai,
            IOptions<StoreChatOptions> options,
            ILogger<SalesLlmIntentRouter> logger)
        {
            _ai = ai;
            _options = options.Value ?? new StoreChatOptions();
            _logger = logger;
        }

        public async Task<SalesLlmRouterDecision?> TryDecideAsync(
            StoreChatSession session,
            string userText,
            CancellationToken ct = default)
        {
            if (!_options.EnableLlmIntentRouter)
                return null;

            var state = SalesLlmRouterGuard.BuildState(session);
            var system =
                "Tu es un routeur d'intention pour un assistant magasin. "
                + "Tu réponds UNIQUEMENT par un objet JSON valide, sans markdown, sans texte autour. "
                + "Schema: {\"action\":\"...\",\"query\":null,\"reason\":\"court\"}. "
                + "action DOIT être une des AllowedActions fournies. "
                + "Interdit: inventer des produits, confirmer une commande, calculer des prix. "
                + "Si le panier mur est complet (wallGuideComplete=true) et l'utilisateur valide "
                + "(\"c'est bon\", \"ok\", \"je peux commander\"), choisis review_cart — pas search_products. "
                + "Si l'utilisateur veut avancer dans le parcours mur, wall_next_step. "
                + "create_quote = l'utilisateur veut un devis (le système affichera un rappel bouton). "
                + "confirm_order n'est jamais autorisé.";

            var user =
                "État session (faits C#):\n"
                + JsonSerializer.Serialize(state, JsonOpts)
                + "\n\nMessage client:\n"
                + (userText ?? string.Empty).Trim();

            try
            {
                var raw = await _ai.CompleteSystemUserAsync(system, user, ct);
                var decision = SalesLlmRouterGuard.TryParseDecision(raw);
                if (decision?.ParsedAction is not { } action)
                {
                    _logger.LogInformation("LlmIntentRouter: JSON invalide ou action inconnue: {Raw}",
                        Truncate(raw));
                    return null;
                }

                if (!SalesLlmRouterGuard.IsAllowed(action, session))
                {
                    _logger.LogInformation("LlmIntentRouter: action {Action} rejetée par guard", action);
                    // Remap confirm_order / wall_next when complete → review si panier non vide
                    if (session.Cart.Count > 0
                        && (action == SalesLlmRouterAction.ConfirmOrder
                            || action == SalesLlmRouterAction.WallNextStep
                            || action == SalesLlmRouterAction.CreateQuote))
                    {
                        return new SalesLlmRouterDecision
                        {
                            Action = SalesLlmRouterGuard.ToWireName(SalesLlmRouterAction.ReviewCart),
                            ParsedAction = SalesLlmRouterAction.ReviewCart,
                            Reason = "guard_remap",
                            Query = decision.Query
                        };
                    }

                    return null;
                }

                return decision;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "LlmIntentRouter failed");
                return null;
            }
        }

        private static string Truncate(string? value)
        {
            if (string.IsNullOrEmpty(value))
                return "";
            return value.Length <= 200 ? value : value[..200] + "…";
        }
    }
}
