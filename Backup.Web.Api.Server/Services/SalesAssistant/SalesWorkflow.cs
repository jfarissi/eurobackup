using System;
using System.Collections.Generic;
using Backup.Web.Api.Server.Services.StoreChat;

namespace Backup.Web.Api.Server.Services.SalesAssistant
{
    /// <summary>
    /// Étapes du parcours vendeur. Décidé en C# — jamais par le LLM.
    /// </summary>
    public enum SalesWorkflowState
    {
        Idle = 0,
        ProjectIdentified = 1,
        Calculated = 2,
        Searching = 3,
        CartBuilding = 4,
        Quoting = 5,
        AwaitingPayment = 6,
        Ordered = 7
    }

    /// <summary>Actions métier contrôlées par le WorkflowGuard.</summary>
    public static class WorkflowActions
    {
        public const string IdentifyProject = "identify_project";
        public const string SearchProducts = "search_products";
        public const string CalculateSurface = "calculate_surface";
        public const string AddToCart = "add_to_cart";
        public const string RemoveFromCart = "remove_from_cart";
        public const string CreateQuote = "create_quote";
        public const string CreateOrder = "create_order";
        public const string ConfirmPayment = "confirm_payment";
        public const string Ask = "ask";
        public const string Reset = "reset";
    }

    public interface ISalesWorkflowGuard
    {
        bool CanExecute(string action, SalesWorkflowState state);
        string DenyMessage(string action, SalesWorkflowState state);
        void ApplyTransition(StoreChatSession session, string action);
        void EnsureConsistent(StoreChatSession session);
    }

    /// <summary>
    /// Filet de sécurité métier : empêche devis/commande hors étape ou sans panier cohérent.
    /// </summary>
    public sealed class SalesWorkflowGuard : ISalesWorkflowGuard
    {
        private static readonly Dictionary<SalesWorkflowState, HashSet<string>> Allowed = new()
        {
            [SalesWorkflowState.Idle] = new(StringComparer.OrdinalIgnoreCase)
            {
                WorkflowActions.IdentifyProject, WorkflowActions.SearchProducts,
                WorkflowActions.CalculateSurface, WorkflowActions.Ask, WorkflowActions.Reset,
                WorkflowActions.AddToCart // ajout depuis accueil si recherche déjà affichée
            },
            [SalesWorkflowState.ProjectIdentified] = new(StringComparer.OrdinalIgnoreCase)
            {
                WorkflowActions.IdentifyProject, WorkflowActions.SearchProducts,
                WorkflowActions.CalculateSurface, WorkflowActions.AddToCart,
                WorkflowActions.Ask, WorkflowActions.Reset
            },
            [SalesWorkflowState.Calculated] = new(StringComparer.OrdinalIgnoreCase)
            {
                WorkflowActions.SearchProducts, WorkflowActions.CalculateSurface,
                WorkflowActions.AddToCart, WorkflowActions.Ask, WorkflowActions.Reset
            },
            [SalesWorkflowState.Searching] = new(StringComparer.OrdinalIgnoreCase)
            {
                WorkflowActions.SearchProducts, WorkflowActions.AddToCart,
                WorkflowActions.RemoveFromCart, WorkflowActions.CreateQuote,
                WorkflowActions.CreateOrder, WorkflowActions.Ask, WorkflowActions.Reset,
                WorkflowActions.CalculateSurface, WorkflowActions.IdentifyProject
            },
            [SalesWorkflowState.CartBuilding] = new(StringComparer.OrdinalIgnoreCase)
            {
                WorkflowActions.SearchProducts, WorkflowActions.AddToCart,
                WorkflowActions.RemoveFromCart, WorkflowActions.CreateQuote,
                WorkflowActions.CreateOrder, WorkflowActions.Ask, WorkflowActions.Reset,
                WorkflowActions.CalculateSurface, WorkflowActions.IdentifyProject
            },
            [SalesWorkflowState.Quoting] = new(StringComparer.OrdinalIgnoreCase)
            {
                WorkflowActions.SearchProducts, WorkflowActions.AddToCart,
                WorkflowActions.RemoveFromCart, WorkflowActions.CreateQuote,
                WorkflowActions.CreateOrder, WorkflowActions.Ask, WorkflowActions.Reset
            },
            [SalesWorkflowState.AwaitingPayment] = new(StringComparer.OrdinalIgnoreCase)
            {
                WorkflowActions.ConfirmPayment, WorkflowActions.Ask, WorkflowActions.Reset,
                WorkflowActions.CreateOrder // renvoi lien paiement
            },
            [SalesWorkflowState.Ordered] = new(StringComparer.OrdinalIgnoreCase)
            {
                WorkflowActions.Ask, WorkflowActions.Reset, WorkflowActions.SearchProducts
            }
        };

        public bool CanExecute(string action, SalesWorkflowState state)
        {
            if (string.IsNullOrWhiteSpace(action))
                return false;
            if (!Allowed.TryGetValue(state, out var set))
                return false;
            return set.Contains(action);
        }

        public string DenyMessage(string action, SalesWorkflowState state)
        {
            return action switch
            {
                WorkflowActions.CreateQuote =>
                    "Pour un devis, ajoutez d'abord des produits au panier.",
                WorkflowActions.CreateOrder =>
                    state == SalesWorkflowState.Idle
                        ? "Ajoutez des produits au panier avant de commander."
                        : "Finalisez d'abord le panier (ou le devis) avant de commander.",
                WorkflowActions.AddToCart =>
                    "Ajoutez un produit depuis une liste affichée, ou précisez votre recherche.",
                WorkflowActions.ConfirmPayment =>
                    "Aucune commande en attente de paiement.",
                _ => $"Action « {action} » non disponible à l'étape « {state} ». Continuez le parcours (recherche → panier → devis / commande)."
            };
        }

        public void ApplyTransition(StoreChatSession session, string action)
        {
            session.WorkflowState = action switch
            {
                WorkflowActions.Reset => SalesWorkflowState.Idle,
                WorkflowActions.IdentifyProject => Max(session.WorkflowState, SalesWorkflowState.ProjectIdentified),
                WorkflowActions.CalculateSurface => Max(session.WorkflowState, SalesWorkflowState.Calculated),
                WorkflowActions.SearchProducts => Max(session.WorkflowState, SalesWorkflowState.Searching),
                WorkflowActions.AddToCart => Max(session.WorkflowState, SalesWorkflowState.CartBuilding),
                WorkflowActions.RemoveFromCart => session.Cart.Count > 0
                    ? SalesWorkflowState.CartBuilding
                    : (session.ActiveProjectDomainId != null
                        ? SalesWorkflowState.Searching
                        : SalesWorkflowState.Idle),
                WorkflowActions.CreateQuote => SalesWorkflowState.Quoting,
                WorkflowActions.CreateOrder => SalesWorkflowState.AwaitingPayment,
                WorkflowActions.ConfirmPayment => SalesWorkflowState.Ordered,
                _ => session.WorkflowState
            };
        }

        /// <summary>Remonte l'état si les faits session sont en avance (ex. panier rempli).</summary>
        public void EnsureConsistent(StoreChatSession session)
        {
            if (session.WorkflowState is SalesWorkflowState.Ordered or SalesWorkflowState.AwaitingPayment)
                return;

            if (session.Cart.Count > 0)
            {
                session.WorkflowState = Max(session.WorkflowState, SalesWorkflowState.CartBuilding);
                return;
            }

            if (session.WallAreaM2 is > 0)
                session.WorkflowState = Max(session.WorkflowState, SalesWorkflowState.Calculated);
            else if (session.LastSuggestedProducts.Count > 0)
                session.WorkflowState = Max(session.WorkflowState, SalesWorkflowState.Searching);
            else if (!string.IsNullOrWhiteSpace(session.ActiveProjectDomainId))
                session.WorkflowState = Max(session.WorkflowState, SalesWorkflowState.ProjectIdentified);
        }

        private static SalesWorkflowState Max(SalesWorkflowState a, SalesWorkflowState b) =>
            (SalesWorkflowState)Math.Max((int)a, (int)b);
    }
}
