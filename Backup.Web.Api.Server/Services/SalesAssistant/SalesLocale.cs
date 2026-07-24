using System;
using System.Collections.Generic;
using Backup.Web.Api.Server.Services.StoreChat;

namespace Backup.Web.Api.Server.Services.SalesAssistant
{
    /// <summary>
    /// Libellés assistant magasin FR / NL / EN (réponses C# déterministes + consignes LLM).
    /// </summary>
    public static class SalesLocale
    {
        public const string Fr = "fr";
        public const string Nl = "nl";
        public const string En = "en";

        public static string Normalize(string? language)
        {
            var lang = (language ?? Fr).Trim().ToLowerInvariant();
            if (lang.StartsWith("nl") || lang.StartsWith("nl-") || lang is "dutch" or "nederlands")
                return Nl;
            if (lang.StartsWith("en") || lang is "english" or "eng")
                return En;
            return Fr;
        }

        public static string Of(StoreChatSession? session) =>
            Normalize(session?.PreferredLanguage);

        public static string LanguageName(string? language) => Normalize(language) switch
        {
            Nl => "néerlandais",
            En => "anglais",
            _ => "français"
        };

        public static string T(StoreChatSession? session, string key, params object[] args)
        {
            var lang = Of(session);
            if (!Catalog.TryGetValue(key, out var byLang)
                || !byLang.TryGetValue(lang, out var template)
                || string.IsNullOrWhiteSpace(template))
            {
                template = Catalog.TryGetValue(key, out var frFall)
                           && frFall.TryGetValue(Fr, out var fr)
                    ? fr
                    : key;
            }

            return args.Length == 0 ? template : string.Format(template, args);
        }

        private static readonly Dictionary<string, Dictionary<string, string>> Catalog = new(StringComparer.OrdinalIgnoreCase)
        {
            ["empty_message"] = new()
            {
                [Fr] = "Message vide.",
                [Nl] = "Leeg bericht.",
                [En] = "Empty message."
            },
            ["new_project"] = new()
            {
                [Fr] = "Nouveau projet démarré. Comment puis-je vous aider ?",
                [Nl] = "Nieuw project gestart. Waarmee kan ik u helpen?",
                [En] = "New project started. How can I help you?"
            },
            ["quote_ready"] = new()
            {
                [Fr] = "Devis prêt ({0:N2} €). Vous pouvez le télécharger, ou passer à la commande.",
                [Nl] = "Offerte klaar ({0:N2} €). U kunt ze downloaden of bestellen.",
                [En] = "Quote ready ({0:N2} €). You can download it, or place an order."
            },
            ["cart_empty_review"] = new()
            {
                [Fr] = "Votre panier est vide. Ajoutez d'abord des produits pour que je puisse le commenter.",
                [Nl] = "Uw winkelwagen is leeg. Voeg eerst producten toe zodat ik hem kan beoordelen.",
                [En] = "Your cart is empty. Add products first so I can review it."
            },
            ["cart_empty_complements"] = new()
            {
                [Fr] = "Votre panier est vide. Ajoutez d'abord des produits, puis je vous dirai ce qu'il manque.",
                [Nl] = "Uw winkelwagen is leeg. Voeg eerst producten toe, dan zeg ik wat er nog ontbreekt.",
                [En] = "Your cart is empty. Add products first, then I'll tell you what's missing."
            },
            ["paint_surface"] = new()
            {
                [Fr] = "Surface murs à peindre ≈ {0:0.#} m².\nEstimation (2 couches, ~10 m²/L) : ≈ {1:0} L de peinture murale (+ sous-couche si support neuf).",
                [Nl] = "Muuroppervlakte ≈ {0:0.#} m².\nSchatting (2 lagen, ~10 m²/L) : ≈ {1:0} L muurverf (+ primer bij nieuwe ondergrond).",
                [En] = "Wall area to paint ≈ {0:0.#} m².\nEstimate (2 coats, ~10 m²/L): ≈ {1:0} L wall paint (+ primer if new surface)."
            },
            ["wall_estimate"] = new()
            {
                [Fr] = "Mur {0:0.##} m × {1:0.##} m → surface ≈ {2:0.##} m².\nEstimations (ordre de grandeur) : ~{3:0} briques, ou ~{4:0} parpaings, et ~{5:0} sac(s) de mortier/ciment ({6:0} kg).",
                [Nl] = "Muur {0:0.##} m × {1:0.##} m → oppervlakte ≈ {2:0.##} m².\nSchatting : ~{3:0} stenen, of ~{4:0} blokken, en ~{5:0} zak(ken) mortel/cement ({6:0} kg).",
                [En] = "Wall {0:0.##} m × {1:0.##} m → area ≈ {2:0.##} m².\nRough estimate: ~{3:0} bricks, or ~{4:0} blocks, and ~{5:0} bag(s) of mortar/cement ({6:0} kg)."
            },
            ["complements_header"] = new()
            {
                [Fr] = "D'après votre panier actuel :",
                [Nl] = "Op basis van uw huidige winkelwagen:",
                [En] = "Based on your current cart:"
            },
            ["complements_list"] = new()
            {
                [Fr] = "Compléments utiles (pas encore dans le panier) :",
                [Nl] = "Nuttige aanvullingen (nog niet in de winkelwagen):",
                [En] = "Useful complements (not yet in the cart):"
            },
            ["complements_none"] = new()
            {
                [Fr] = "Rien d'essentiel ne manque pour démarrer. Vous pouvez passer au devis.",
                [Nl] = "Er ontbreekt niets essentieels om te starten. U kunt een offerte vragen.",
                [En] = "Nothing essential is missing to get started. You can request a quote."
            },
            ["complements_search_paint"] = new()
            {
                [Fr] = "Je peux chercher ces compléments dans le catalogue si vous voulez (ex. « {0} »).\nPas besoin de racheter la peinture déjà choisie.",
                [Nl] = "Ik kan deze aanvullingen in de catalogus zoeken als u wilt (bv. « {0} »).\nU hoeft de gekozen verf niet opnieuw te kopen.",
                [En] = "I can search the catalog for these complements if you want (e.g. « {0} »).\nNo need to buy the paint already chosen again."
            },
            ["complements_search_wall"] = new()
            {
                [Fr] = "Je peux chercher ces compléments dans le catalogue si vous voulez (ex. « treillis » ou « truelle »).\nPas besoin de racheter briques/blocs/ciment déjà choisis.",
                [Nl] = "Ik kan deze aanvullingen in de catalogus zoeken als u wilt (bv. « wapening » of « troffel »).\nU hoeft stenen/blokken/cement niet opnieuw te kopen.",
                [En] = "I can search the catalog for these complements if you want (e.g. « mesh » or « trowel »).\nNo need to buy bricks/blocks/cement already chosen again."
            },
            ["complements_search_generic"] = new()
            {
                [Fr] = "Je peux chercher ces compléments dans le catalogue si vous voulez (ex. « {0} »).",
                [Nl] = "Ik kan deze aanvullingen in de catalogus zoeken als u wilt (bv. « {0} »).",
                [En] = "I can search the catalog for these complements if you want (e.g. « {0} »)."
            },
            ["complements_catalog_refs"] = new()
            {
                [Fr] = "Voici des références catalogue pour ces compléments :",
                [Nl] = "Hier zijn catalogusreferenties voor deze aanvullingen:",
                [En] = "Here are catalog references for these complements:"
            },
            ["adjust_qty"] = new()
            {
                [Fr] = "Ajustez les quantités puis ajoutez au panier / devis / commande.",
                [Nl] = "Pas de hoeveelheden aan en voeg toe aan winkelwagen / offerte / bestelling.",
                [En] = "Adjust quantities then add to cart / quote / order."
            },
            ["review_header"] = new()
            {
                [Fr] = "Voici mon avis sur votre panier :",
                [Nl] = "Dit is mijn advies over uw winkelwagen:",
                [En] = "Here is my review of your cart:"
            },
            ["review_next"] = new()
            {
                [Fr] = "Prochaines familles utiles :",
                [Nl] = "Volgende nuttige families:",
                [En] = "Next useful product families:"
            },
            ["review_ok"] = new()
            {
                [Fr] = "Rien d’essentiel ne manque pour passer au devis / commande.",
                [Nl] = "Er ontbreekt niets essentieels om naar offerte / bestelling te gaan.",
                [En] = "Nothing essential is missing to go to quote / order."
            },
            ["multi_paint_warn"] = new()
            {
                [Fr] = "⚠ Plusieurs peintures murales dans le panier : ce sont des alternatives — en général une seule gamme suffit pour le chantier.",
                [Nl] = "⚠ Meerdere muurverven in de winkelwagen: dit zijn alternatieven — meestal volstaat één gamma voor de werf.",
                [En] = "⚠ Several wall paints in the cart: these are alternatives — usually one range is enough for the job."
            },
            ["paint_need_line"] = new()
            {
                [Fr] = "Surface à peindre ~{0:0.#} m² → besoin estimé ≈ {1:0} L (2 couches).",
                [Nl] = "Te schilderen oppervlakte ~{0:0.#} m² → geschatte behoefte ≈ {1:0} L (2 lagen).",
                [En] = "Area to paint ~{0:0.#} m² → estimated need ≈ {1:0} L (2 coats)."
            }
        };
    }
}
