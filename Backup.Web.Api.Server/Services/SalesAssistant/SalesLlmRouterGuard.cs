using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Backup.Web.Api.Server.Services.StoreChat;

namespace Backup.Web.Api.Server.Services.SalesAssistant
{
    /// <summary>Actions autorisées pour le routeur LLM (liste fermée).</summary>
    public enum SalesLlmRouterAction
    {
        SearchProducts,
        WallNextStep,
        ReviewCart,
        SuggestComplements,
        AskClarification,
        /// <summary>Ne crée pas de devis : invite à utiliser le bouton UI.</summary>
        CreateQuote,
        /// <summary>Toujours bloqué par le guard — jamais exécuté depuis le LLM.</summary>
        ConfirmOrder
    }

    public sealed class SalesLlmRouterDecision
    {
        [JsonPropertyName("action")]
        public string Action { get; set; } = "";

        [JsonPropertyName("query")]
        public string? Query { get; set; }

        [JsonPropertyName("reason")]
        public string? Reason { get; set; }

        public SalesLlmRouterAction? ParsedAction { get; set; }
    }

    /// <summary>État sérialisé injecté dans le prompt (faits C#, pas inventés par le LLM).</summary>
    public sealed class SalesLlmRouterStateDto
    {
        public string? DomainId { get; set; }
        public bool WallGuideComplete { get; set; }
        public bool HasStructure { get; set; }
        public bool HasBinder { get; set; }
        public bool HasReinforcement { get; set; }
        public bool HasTools { get; set; }
        public string? NextWallFamily { get; set; }
        public int CartCount { get; set; }
        public List<string> CartNames { get; set; } = new();
        public List<string> AllowedActions { get; set; } = new();
    }

    public static class SalesLlmRouterGuard
    {
        public static IReadOnlyList<SalesLlmRouterAction> AllowedActions(StoreChatSession session)
        {
            var list = new List<SalesLlmRouterAction>
            {
                SalesLlmRouterAction.SearchProducts,
                SalesLlmRouterAction.AskClarification
            };

            if (session.Cart.Count > 0)
            {
                list.Add(SalesLlmRouterAction.ReviewCart);
                list.Add(SalesLlmRouterAction.SuggestComplements);
                list.Add(SalesLlmRouterAction.CreateQuote);
            }

            // Parcours guidé actif non terminé → étape suivante (alias ProjectNextStep).
            if (Guides.ProjectGuides.TryGet(session, out var guide)
                && guide is not null
                && !guide.IsComplete(session))
            {
                list.Add(SalesLlmRouterAction.WallNextStep);
            }

            // ConfirmOrder volontairement absent.
            return list;
        }

        public static SalesLlmRouterStateDto BuildState(StoreChatSession session)
        {
            var cart = SalesProjectGuide.CartOnlyHay(session);
            var allowed = AllowedActions(session);
            string? next = null;
            if (Guides.ProjectGuides.TryGet(session, out var activeGuide) && activeGuide is not null)
            {
                next = activeGuide is Guides.WallProjectGuide
                    ? SalesProjectGuide.ResolveWallFamily(session, null).ToString()
                    : activeGuide.ResolveNext(session, null).Id;
            }

            return new SalesLlmRouterStateDto
            {
                DomainId = session.ActiveProjectDomainId,
                WallGuideComplete = Guides.ProjectGuides.IsComplete(session),
                HasStructure = SalesProjectGuide.HasStructure(cart),
                HasBinder = SalesProjectGuide.HasBinder(cart),
                HasReinforcement = SalesProjectGuide.HasReinforcement(cart),
                HasTools = SalesProjectGuide.HasTools(cart),
                NextWallFamily = next,
                CartCount = session.Cart.Count,
                CartNames = session.Cart.Select(c => c.Name).Take(8).ToList(),
                AllowedActions = allowed.Select(ToWireName).ToList()
            };
        }

        public static bool IsAllowed(SalesLlmRouterAction action, StoreChatSession session) =>
            AllowedActions(session).Contains(action);

        public static string ToWireName(SalesLlmRouterAction action) => action switch
        {
            SalesLlmRouterAction.SearchProducts => "search_products",
            SalesLlmRouterAction.WallNextStep => "wall_next_step",
            SalesLlmRouterAction.ReviewCart => "review_cart",
            SalesLlmRouterAction.SuggestComplements => "suggest_complements",
            SalesLlmRouterAction.AskClarification => "ask_clarification",
            SalesLlmRouterAction.CreateQuote => "create_quote",
            SalesLlmRouterAction.ConfirmOrder => "confirm_order",
            _ => "ask_clarification"
        };

        public static bool TryParseAction(string? raw, out SalesLlmRouterAction action)
        {
            action = SalesLlmRouterAction.AskClarification;
            if (string.IsNullOrWhiteSpace(raw))
                return false;

            var key = raw.Trim().ToLowerInvariant().Replace('-', '_');
            switch (key)
            {
                case "search_products":
                case "search":
                    action = SalesLlmRouterAction.SearchProducts;
                    return true;
                case "wall_next_step":
                case "next_step":
                    action = SalesLlmRouterAction.WallNextStep;
                    return true;
                case "review_cart":
                case "cart_review":
                    action = SalesLlmRouterAction.ReviewCart;
                    return true;
                case "suggest_complements":
                case "complements":
                    action = SalesLlmRouterAction.SuggestComplements;
                    return true;
                case "ask_clarification":
                case "clarify":
                    action = SalesLlmRouterAction.AskClarification;
                    return true;
                case "create_quote":
                case "quote":
                    action = SalesLlmRouterAction.CreateQuote;
                    return true;
                case "confirm_order":
                case "order":
                    action = SalesLlmRouterAction.ConfirmOrder;
                    return true;
                default:
                    return false;
            }
        }

        public static SalesLlmRouterDecision? TryParseDecision(string? llmText)
        {
            if (string.IsNullOrWhiteSpace(llmText))
                return null;

            var json = ExtractJsonObject(llmText);
            if (json == null)
                return null;

            try
            {
                var decision = JsonSerializer.Deserialize<SalesLlmRouterDecision>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                if (decision == null || !TryParseAction(decision.Action, out var action))
                    return null;

                decision.ParsedAction = action;
                decision.Action = ToWireName(action);
                return decision;
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static string? ExtractJsonObject(string text)
        {
            var t = text.Trim();
            if (t.StartsWith("```", StringComparison.Ordinal))
            {
                var start = t.IndexOf('{');
                var end = t.LastIndexOf('}');
                if (start >= 0 && end > start)
                    return t[start..(end + 1)];
            }

            var i = t.IndexOf('{');
            var j = t.LastIndexOf('}');
            if (i >= 0 && j > i)
                return t[i..(j + 1)];

            return null;
        }
    }
}
