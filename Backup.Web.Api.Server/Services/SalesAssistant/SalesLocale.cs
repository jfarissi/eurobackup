using System;
using System.Collections.Generic;
using System.Linq;
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

        public static string T(StoreChatSession? session, string key, params object?[] args)
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

            if (args.Length == 0)
                return template;

            var safe = args.Select(a => a ?? string.Empty).Cast<object>().ToArray();
            return string.Format(template, safe);
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
            ["lang_mismatch_warn"] = new()
            {
                [Fr] = "Note : la langue de l'interface est {0}, mais votre message semble en {1}. Vous pouvez changer de langue en haut si besoin.",
                [Nl] = "Let op: de interfacetaal is {0}, maar uw bericht lijkt in het {1}. U kunt bovenaan van taal wisselen indien nodig.",
                [En] = "Note: the UI language is {0}, but your message looks like {1}. You can switch language at the top if needed."
            },
            ["lang_name_fr"] = new()
            {
                [Fr] = "français",
                [Nl] = "Frans",
                [En] = "French"
            },
            ["lang_name_nl"] = new()
            {
                [Fr] = "néerlandais",
                [Nl] = "Nederlands",
                [En] = "Dutch"
            },
            ["lang_name_en"] = new()
            {
                [Fr] = "anglais",
                [Nl] = "Engels",
                [En] = "English"
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
            ["complements_inline"] = new()
            {
                [Fr] = "Compléments utiles :",
                [Nl] = "Nuttige aanvullingen:",
                [En] = "Useful complements:"
            },
            ["guide_painting_title"] = new()
            {
                [Fr] = "Parcours peinture (une famille à la fois) :",
                [Nl] = "Verftraject (één productgroep tegelijk):",
                [En] = "Paint journey (one family at a time):"
            },
            ["guide_family_suffix"] = new()
            {
                [Fr] = "une famille à la fois",
                [Nl] = "één productgroep tegelijk",
                [En] = "one family at a time"
            },
            ["guide_step_now"] = new()
            {
                [Fr] = " ← à choisir maintenant",
                [Nl] = " ← nu kiezen",
                [En] = " ← choose now"
            },
            ["guide_aisle"] = new()
            {
                [Fr] = " — rayon : {0}",
                [Nl] = " — afdeling: {0}",
                [En] = " — aisle: {0}"
            },
            ["guide_complete"] = new()
            {
                [Fr] = "Parcours complet — vous pouvez demander un devis ou passer commande.",
                [Nl] = "Traject compleet — u kunt een offerte vragen of bestellen.",
                [En] = "Journey complete — you can request a quote or place an order."
            },
            ["guide_next_hint"] = new()
            {
                [Fr] = "Ajoutez une référence au panier ou précisez un type pour passer à l’étape suivante.",
                [Nl] = "Voeg een referentie toe aan de winkelwagen of preciseer een type om verder te gaan.",
                [En] = "Add an item to the cart or specify a type to move to the next step."
            },
            ["guide_next_step"] = new()
            {
                [Fr] = "Étape suivante — {0} :",
                [Nl] = "Volgende stap — {0}:",
                [En] = "Next step — {0}:"
            },
            ["guide_painting_paint"] = new()
            {
                [Fr] = "Peinture",
                [Nl] = "Muurverf",
                [En] = "Paint"
            },
            ["guide_painting_primer"] = new()
            {
                [Fr] = "Sous-couche / primaire",
                [Nl] = "Voorstrijk / primer",
                [En] = "Primer / undercoat"
            },
            ["guide_painting_roller"] = new()
            {
                [Fr] = "Rouleau / pinceau",
                [Nl] = "Verfroller / kwast",
                [En] = "Roller / brush"
            },
            ["guide_painting_tape"] = new()
            {
                [Fr] = "Adhésif de masquage",
                [Nl] = "Schilderstape",
                [En] = "Masking tape"
            },
            ["guide_painting_aisle_paint"] = new()
            {
                [Fr] = "Peintures intérieur / extérieur",
                [Nl] = "Binnen- / buitenverf",
                [En] = "Interior / exterior paints"
            },
            ["guide_painting_aisle_primer"] = new()
            {
                [Fr] = "Primaires & sous-couches",
                [Nl] = "Primers & voorstrijk",
                [En] = "Primers & undercoats"
            },
            ["guide_painting_aisle_roller"] = new()
            {
                [Fr] = "Outillage peinture",
                [Nl] = "Verfgereedschap",
                [En] = "Paint tools"
            },
            ["guide_painting_aisle_tape"] = new()
            {
                [Fr] = "Rubans & protection",
                [Nl] = "Tape & bescherming",
                [En] = "Tapes & protection"
            },
            ["guide_roofing_title"] = new()
            {
                [Fr] = "Parcours toiture (une famille à la fois) :",
                [Nl] = "Daktraject (één productgroep tegelijk):",
                [En] = "Roofing journey (one family at a time):"
            },
            ["guide_roofing_cover"] = new()
            {
                [Fr] = "Couverture / tuiles",
                [Nl] = "Dakbedekking / dakpannen",
                [En] = "Covering / tiles"
            },
            ["guide_roofing_fixings"] = new()
            {
                [Fr] = "Crochets / fixations",
                [Nl] = "Haken / bevestigingen",
                [En] = "Hooks / fixings"
            },
            ["guide_roofing_gutter"] = new()
            {
                [Fr] = "Gouttière / évacuation",
                [Nl] = "Dakgoot / afvoer",
                [En] = "Gutter / drainage"
            },
            ["guide_roofing_aisle_cover"] = new()
            {
                [Fr] = "Tuiles, ardoises, plaques",
                [Nl] = "Dakpannen, leien, platen",
                [En] = "Tiles, slates, sheets"
            },
            ["guide_roofing_aisle_fixings"] = new()
            {
                [Fr] = "Fixations toiture",
                [Nl] = "Dakbevestigingen",
                [En] = "Roof fixings"
            },
            ["guide_roofing_aisle_gutter"] = new()
            {
                [Fr] = "Gouttières & descentes",
                [Nl] = "Dakgoten & afvoeren",
                [En] = "Gutters & downpipes"
            },
            ["paint_hint_wall"] = new()
            {
                [Fr] = "mur ≈ {0:0.#} m²",
                [Nl] = "muur ≈ {0:0.#} m²",
                [En] = "wall ≈ {0:0.#} m²"
            },
            ["paint_hint_room"] = new()
            {
                [Fr] = "pièce ≈ {0:0.#} m²",
                [Nl] = "ruimte ≈ {0:0.#} m²",
                [En] = "room ≈ {0:0.#} m²"
            },
            ["cart_item_added"] = new()
            {
                [Fr] = "Produit ajouté au panier.",
                [Nl] = "Product toegevoegd aan de winkelwagen.",
                [En] = "Product added to cart."
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
            },
            ["here_examples"] = new()
            {
                [Fr] = "Voici quelques exemples en attendant :",
                [Nl] = "Hier zijn enkele voorbeelden in afwachting:",
                [En] = "Here are a few examples in the meantime:"
            },
            ["here_n_catalog"] = new()
            {
                [Fr] = "Voici {0} produit(s) du catalogue.",
                [Nl] = "Hier zijn {0} product(en) uit de catalogus.",
                [En] = "Here are {0} product(s) from the catalog."
            },
            ["here_n_domain"] = new()
            {
                [Fr] = "Voici {0} produit(s) du catalogue pour {1}.",
                [Nl] = "Hier zijn {0} product(en) uit de catalogus voor {1}.",
                [En] = "Here are {0} product(s) from the catalog for {1}."
            },
            ["here_n_brand"] = new()
            {
                [Fr] = "Voici {0} produit(s) de la marque {1}.",
                [Nl] = "Hier zijn {0} product(en) van het merk {1}.",
                [En] = "Here are {0} product(s) from the brand {1}."
            },
            ["here_n_brand_weight"] = new()
            {
                [Fr] = "Voici {0} produit(s) {1} en {2}.",
                [Nl] = "Hier zijn {0} product(en) {1} in {2}.",
                [En] = "Here are {0} {1} product(s) in {2}."
            },
            ["here_n_brand_type"] = new()
            {
                [Fr] = "Voici {0} référence(s) {1} liées à « {2} ».",
                [Nl] = "Hier zijn {0} referentie(s) {1} voor « {2} ».",
                [En] = "Here are {0} {1} reference(s) related to « {2} »."
            },
            ["here_n_brand_type_weight"] = new()
            {
                [Fr] = "Voici {0} référence(s) {1} — {2} — {3}.",
                [Nl] = "Hier zijn {0} referentie(s) {1} — {2} — {3}.",
                [En] = "Here are {0} {1} reference(s) — {2} — {3}."
            },
            ["brand_type_missing"] = new()
            {
                [Fr] = "Je n'ai pas trouvé de {0} de la marque {1} dans le catalogue. Voici d'autres produits {1} :",
                [Nl] = "Ik vond geen {0} van het merk {1} in de catalogus. Hier zijn andere {1}-producten:",
                [En] = "I couldn't find {0} from brand {1} in the catalog. Here are other {1} products:"
            },
            ["yes_brand"] = new()
            {
                [Fr] = "Oui. {0} propose {1}dans notre catalogue{2}",
                [Nl] = "Ja. {0} biedt {1}aan in onze catalogus{2}",
                [En] = "Yes. {0} offers {1}in our catalog{2}"
            },
            ["ask_weight"] = new()
            {
                [Fr] = "Cherchez-vous un petit format (ex. 5 kg) ou un sac chantier (25 kg) ?",
                [Nl] = "Zoekt u een klein formaat (bv. 5 kg) of een werfzak (25 kg)?",
                [En] = "Are you looking for a small format (e.g. 5 kg) or a site bag (25 kg)?"
            },
            ["display_best"] = new()
            {
                [Fr] = "(Affichage des {0} meilleures sur {1} — précisez pour affiner.)",
                [Nl] = "(Weergave van de {0} beste op {1} — preciseer om te verfijnen.)",
                [En] = "(Showing the top {0} of {1} — please refine for better matches.)"
            },
            ["catalog_refine_lighting"] = new()
            {
                [Fr] = "J’ai environ {0} références d’éclairage. Pour afficher les bonnes ampoules, précisez :\n"
                       + "• Température : blanc chaud (2700K), neutre (4000K) ou froid (6500K)\n"
                       + "• Ou l’usage : pièce de vie / bureau / extérieur\n"
                       + "• Puissance (ex. 8W) si vous la connaissez\n\n"
                       + "Sinon dites « montrez quand même » pour voir quelques références.",
                [Nl] = "Ik heb ongeveer {0} verlichtingsreferenties. Om de juiste lampen te tonen, preciseer:\n"
                       + "• Kleurtemperatuur: warm wit (2700K), neutraal (4000K) of koel wit (6500K)\n"
                       + "• Of het gebruik: woonkamer / kantoor / buiten\n"
                       + "• Vermogen (bv. 8W) indien bekend\n\n"
                       + "Of zeg « toon toch » om enkele referenties te zien.",
                [En] = "I have about {0} lighting references. To show the right bulbs, please specify:\n"
                       + "• Colour temperature: warm white (2700K), neutral (4000K) or cool white (6500K)\n"
                       + "• Or usage: living room / office / outdoor\n"
                       + "• Wattage (e.g. 8W) if you know it\n\n"
                       + "Or say « show anyway » to see a few references."
            },
            ["catalog_refine_painting"] = new()
            {
                [Fr] = "J’ai environ {0} références peinture. Pour afficher les bons produits, précisez :\n"
                       + "• Intérieur ou extérieur ?\n"
                       + "• Peinture murale (latex), sous-couche, ou outils (rouleau / pinceaux) ?\n"
                       + "• Couleur / marque si vous en avez une\n"
                       + "• Surface en m² si connue\n\n"
                       + "Sinon dites « montrez quand même ».",
                [Nl] = "Ik heb ongeveer {0} verfreferenties. Preciseer:\n"
                       + "• Binnen of buiten?\n"
                       + "• Muurverf (latex), voorstrijk, of gereedschap (roller / penselen)?\n"
                       + "• Kleur / merk indien bekend\n"
                       + "• Oppervlakte in m² indien bekend\n\n"
                       + "Of zeg « toon toch ».",
                [En] = "I have about {0} paint references. Please specify:\n"
                       + "• Indoor or outdoor?\n"
                       + "• Wall paint (latex), primer, or tools (roller / brushes)?\n"
                       + "• Colour / brand if you have one\n"
                       + "• Area in m² if known\n\n"
                       + "Or say « show anyway »."
            },
            ["catalog_refine_wall"] = new()
            {
                [Fr] = "J’ai environ {0} références maçonnerie. Pour afficher les bons matériaux, précisez :\n"
                       + "• Briques, blocs / parpaings, ou mortier / ciment ?\n"
                       + "• Marque (ex. Silka, Knauf) si connue\n"
                       + "• Dimensions du mur (L × H) si connues\n\n"
                       + "Sinon dites « montrez quand même ».",
                [Nl] = "Ik heb ongeveer {0} metselreferenties. Preciseer:\n"
                       + "• Stenen, blokken, of mortel / cement?\n"
                       + "• Merk (bv. Silka, Knauf) indien bekend\n"
                       + "• Muurafmetingen (L × H) indien bekend\n\n"
                       + "Of zeg « toon toch ».",
                [En] = "I have about {0} masonry references. Please specify:\n"
                       + "• Bricks, blocks, or mortar / cement?\n"
                       + "• Brand (e.g. Silka, Knauf) if known\n"
                       + "• Wall size (L × H) if known\n\n"
                       + "Or say « show anyway »."
            },
            ["catalog_refine_tiling"] = new()
            {
                [Fr] = "J’ai environ {0} références carrelage. Précisez :\n"
                       + "• Sol ou mur ?\n"
                       + "• Carreaux, colle, ou joints ?\n"
                       + "• Format / couleur / marque si connue\n\n"
                       + "Sinon dites « montrez quand même ».",
                [Nl] = "Ik heb ongeveer {0} tegelreferenties. Preciseer:\n"
                       + "• Vloer of muur?\n"
                       + "• Tegels, lijm of voegen?\n"
                       + "• Formaat / kleur / merk indien bekend\n\n"
                       + "Of zeg « toon toch ».",
                [En] = "I have about {0} tiling references. Please specify:\n"
                       + "• Floor or wall?\n"
                       + "• Tiles, adhesive, or grout?\n"
                       + "• Size / colour / brand if known\n\n"
                       + "Or say « show anyway »."
            },
            ["catalog_refine_plumbing"] = new()
            {
                [Fr] = "J’ai environ {0} références plomberie. Précisez :\n"
                       + "• Robinetterie, PVC / tuyaux, évacuation, ou accessoires ?\n"
                       + "• Marque / diamètre si connus\n\n"
                       + "Sinon dites « montrez quand même ».",
                [Nl] = "Ik heb ongeveer {0} sanitairreferenties. Preciseer:\n"
                       + "• Kranen, PVC / buizen, afvoer of accessoires?\n"
                       + "• Merk / diameter indien bekend\n\n"
                       + "Of zeg « toon toch ».",
                [En] = "I have about {0} plumbing references. Please specify:\n"
                       + "• Taps, PVC / pipes, drainage, or accessories?\n"
                       + "• Brand / diameter if known\n\n"
                       + "Or say « show anyway »."
            },
            ["catalog_refine_roofing"] = new()
            {
                [Fr] = "J’ai environ {0} références toiture. Précisez :\n"
                       + "• Tuiles / panneaux, crochets / fixations, ou gouttière ?\n"
                       + "• Marque / type si connus\n\n"
                       + "Sinon dites « montrez quand même ».",
                [Nl] = "Ik heb ongeveer {0} dakreferenties. Preciseer:\n"
                       + "• Dakpannen / panelen, haken / bevestiging, of goot?\n"
                       + "• Merk / type indien bekend\n\n"
                       + "Of zeg « toon toch ».",
                [En] = "I have about {0} roofing references. Please specify:\n"
                       + "• Tiles / panels, hooks / fixings, or gutter?\n"
                       + "• Brand / type if known\n\n"
                       + "Or say « show anyway »."
            },
            ["catalog_refine_electrical"] = new()
            {
                [Fr] = "J’ai environ {0} références électricité. Précisez :\n"
                       + "• Ampoules / LED, prises & interrupteurs, câbles, ou tableaux / disjoncteurs ?\n"
                       + "• Marque / modèle si connus\n\n"
                       + "Sinon dites « montrez quand même ».",
                [Nl] = "Ik heb ongeveer {0} elektriciteitsreferenties. Preciseer:\n"
                       + "• Lampen / LED, stopcontacten & schakelaars, kabels, of borden / automaten?\n"
                       + "• Merk / model indien bekend\n\n"
                       + "Of zeg « toon toch ».",
                [En] = "I have about {0} electrical references. Please specify:\n"
                       + "• Bulbs / LED, sockets & switches, cables, or panels / breakers?\n"
                       + "• Brand / model if known\n\n"
                       + "Or say « show anyway »."
            },
            ["catalog_refine_garden"] = new()
            {
                [Fr] = "J’ai environ {0} références jardin. Précisez :\n"
                       + "• Aménagement (dalles, clôture), entretien (tondeuse, haie), ou nettoyage ?\n"
                       + "• Marque / type si connus\n\n"
                       + "Sinon dites « montrez quand même ».",
                [Nl] = "Ik heb ongeveer {0} tuinreferenties. Preciseer:\n"
                       + "• Aanleg (tegels, omheining), onderhoud (maaier, haag), of reiniging?\n"
                       + "• Merk / type indien bekend\n\n"
                       + "Of zeg « toon toch ».",
                [En] = "I have about {0} garden references. Please specify:\n"
                       + "• Landscaping (slabs, fence), maintenance (mower, hedge), or cleaning?\n"
                       + "• Brand / type if known\n\n"
                       + "Or say « show anyway »."
            },
            ["catalog_refine_generic"] = new()
            {
                [Fr] = "J’ai environ {0} références ({1}). Pour afficher les bons produits, précisez type, marque ou usage.\n\n"
                       + "Sinon dites « montrez quand même ».",
                [Nl] = "Ik heb ongeveer {0} referenties ({1}). Preciseer type, merk of gebruik.\n\n"
                       + "Of zeg « toon toch ».",
                [En] = "I have about {0} references ({1}). Please specify type, brand or usage.\n\n"
                       + "Or say « show anyway »."
            },
            ["catalog_sku_found"] = new()
            {
                [Fr] = "Voici {0} ampoule(s) {1} adaptées à votre demande.",
                [Nl] = "Hier zijn {0} {1}-lamp(en) die bij uw vraag passen.",
                [En] = "Here are {0} {1} bulb(s) matching your request."
            },
            ["catalog_sku_soft_refine"] = new()
            {
                [Fr] = "Préférez-vous une lumière chaleureuse (salon, 2700K), neutre (bureau, 4000K) ou très blanche (garage, 6500K) ?",
                [Nl] = "Kiest u liever warm wit (woonkamer, 2700K), neutraal (kantoor, 4000K) of koel wit (garage, 6500K)?",
                [En] = "Prefer warm white (living room, 2700K), neutral (office, 4000K) or cool white (garage, 6500K)?"
            },
            ["qty_prefilled"] = new()
            {
                [Fr] = "Les quantités proposées sont préremplies dans le tableau.",
                [Nl] = "De voorgestelde hoeveelheden zijn vooraf ingevuld in de tabel.",
                [En] = "Suggested quantities are pre-filled in the table."
            },
            ["more_products"] = new()
            {
                [Fr] = "Voici d'autres produits{0} :",
                [Nl] = "Hier zijn andere producten{0}:",
                [En] = "Here are more products{0}:"
            },
            ["more_products_for"] = new()
            {
                [Fr] = " pour {0}",
                [Nl] = " voor {0}",
                [En] = " for {0}"
            },
            ["domain_painting"] = new()
            {
                [Fr] = "Peinture",
                [Nl] = "Schilderen",
                [En] = "Painting"
            },
            ["domain_wall"] = new()
            {
                [Fr] = "Construction de mur",
                [Nl] = "Muurbouw",
                [En] = "Wall construction"
            },
            ["domain_tiling"] = new()
            {
                [Fr] = "Carrelage",
                [Nl] = "Tegels",
                [En] = "Tiling"
            },
            ["domain_electrical"] = new()
            {
                [Fr] = "Électricité",
                [Nl] = "Elektriciteit",
                [En] = "Electrical"
            },
            ["domain_roofing"] = new()
            {
                [Fr] = "Toiture",
                [Nl] = "Dak",
                [En] = "Roofing"
            },
            ["domain_plumbing"] = new()
            {
                [Fr] = "Plomberie",
                [Nl] = "Sanitair",
                [En] = "Plumbing"
            },
            ["tip_primer"] = new()
            {
                [Fr] = "Sous-couche",
                [Nl] = "Voorstrijk / primer",
                [En] = "Primer"
            },
            ["tip_primer_reason"] = new()
            {
                [Fr] = "Meilleure accroche et rendu uniforme.",
                [Nl] = "Betere hechting en egale afwerking.",
                [En] = "Better adhesion and even finish."
            },
            ["tip_roller"] = new()
            {
                [Fr] = "Rouleau",
                [Nl] = "Verfroller",
                [En] = "Paint roller"
            },
            ["tip_roller_reason"] = new()
            {
                [Fr] = "Application rapide sur grandes surfaces.",
                [Nl] = "Snelle aanbreng op grote oppervlakken.",
                [En] = "Fast application on large areas."
            },
            ["tip_tape"] = new()
            {
                [Fr] = "Ruban de masquage",
                [Nl] = "Schilderstape",
                [En] = "Masking tape"
            },
            ["tip_tape_reason"] = new()
            {
                [Fr] = "Finitions propres aux angles.",
                [Nl] = "Nette afwerking in de hoeken.",
                [En] = "Clean finishes at edges."
            },
            ["lang_switched"] = new()
            {
                [Fr] = "Langue définie : français. Je répondrai désormais en français.",
                [Nl] = "Taal ingesteld: Nederlands. Ik antwoord vanaf nu in het Nederlands.",
                [En] = "Language set to English. I will reply in English from now on."
            },
            ["complements_confirm_prompt"] = new()
            {
                [Fr] = "Répondez « ok », « d'accord », « oui » ou « vas-y » pour que je cherche ces articles dans le catalogue.",
                [Nl] = "Antwoord « ok », « ja » of « ga je gang » zodat ik deze artikelen in de catalogus zoek.",
                [En] = "Reply « ok », « yes » or « go ahead » so I can search for these items in the catalog."
            },
            ["complements_not_found"] = new()
            {
                [Fr] = "Je n'ai pas encore trouvé ces compléments. Réessayez « ok », ou un mot précis : {0}",
                [Nl] = "Ik heb deze aanvullingen nog niet gevonden. Probeer opnieuw « ok », of een precies woord: {0}",
                [En] = "I haven't found these complements yet. Try « ok » again, or a precise word: {0}"
            },
            ["complements_hints_paint"] = new()
            {
                [Fr] = "sous-couche, rouleau, ruban.",
                [Nl] = "voorstrijk, roller, schilderstape.",
                [En] = "primer, roller, masking tape."
            },
            ["complements_hints_wall"] = new()
            {
                [Fr] = "treillis, truelle, auge, gants.",
                [Nl] = "wapeningsnet, troffel, kuip, handschoenen.",
                [En] = "mesh, trowel, mixing tub, gloves."
            },
            ["complements_cart_found"] = new()
            {
                [Fr] = "Voici les compléments catalogue pour votre panier :\nAjoutez ce dont vous avez besoin, puis devis / commande.",
                [Nl] = "Hier zijn catalogusaanvullingen voor uw winkelwagen:\nVoeg toe wat u nodig heeft, daarna offerte / bestelling.",
                [En] = "Here are catalog complements for your cart:\nAdd what you need, then quote / order."
            },
            ["complements_searching"] = new()
            {
                [Fr] = "Je cherche les compléments… Réessayez « ok », ou un mot précis : treillis, truelle, auge, gants.",
                [Nl] = "Ik zoek de aanvullingen… Probeer opnieuw « ok », of een precies woord: wapeningsnet, troffel, kuip, handschoenen.",
                [En] = "Searching for complements… Try « ok » again, or a precise word: mesh, trowel, mixing tub, gloves."
            },
            ["direct_complement_missing"] = new()
            {
                [Fr] = "Je n'ai pas trouvé de produit pour « {0} ». Essayez un autre mot (ex. handschoen, truelle, auge).",
                [Nl] = "Ik vond geen product voor « {0} ». Probeer een ander woord (bv. handschoen, troffel, kuip).",
                [En] = "I couldn't find a product for « {0} ». Try another word (e.g. glove, trowel, tub)."
            },
            ["direct_complement_found"] = new()
            {
                [Fr] = "Voici des références pour « {0} » :",
                [Nl] = "Hier zijn referenties voor « {0} »:",
                [En] = "Here are references for « {0} »:"
            },
            ["more_products_empty"] = new()
            {
                [Fr] = "Je n'ai pas d'autres références pertinentes pour l'instant. Précisez un type ou une marque.",
                [Nl] = "Ik heb voorlopig geen andere relevante referenties. Preciseer een type of merk.",
                [En] = "I don't have other relevant references right now. Please refine a type or brand."
            },
            ["more_products_empty_roofing"] = new()
            {
                [Fr] = "Je n'ai pas d'autres références toiture pour l'instant. Précisez (tuiles, crochets, gouttière…).",
                [Nl] = "Ik heb voorlopig geen andere dakreferenties. Preciseer (dakpannen, panhaken, dakgoot…).",
                [En] = "No more roofing references for now. Please refine (tiles, hooks, gutter…)."
            },
            ["more_products_empty_painting"] = new()
            {
                [Fr] = "Je n'ai pas d'autres références peinture pour l'instant. Précisez (peinture, primaire, rouleau…).",
                [Nl] = "Ik heb voorlopig geen andere verfreferenties. Preciseer (muurverf, primer, roller…).",
                [En] = "No more paint references for now. Please refine (wall paint, primer, roller…)."
            },
            ["more_products_empty_garden"] = new()
            {
                [Fr] = "Je n'ai pas d'autres références jardin pour l'instant. Précisez (bordure, clôture, gravier…).",
                [Nl] = "Ik heb voorlopig geen andere tuinreferenties. Preciseer (boordsteen, omheining, grind…).",
                [En] = "No more garden references for now. Please refine (edging, fence, gravel…)."
            },
            ["weight_not_found"] = new()
            {
                [Fr] = "Je n'ai pas trouvé de{0}{1}{2} dans le catalogue.{3}",
                [Nl] = "Ik vond geen{0}{1}{2} in de catalogus.{3}",
                [En] = "I couldn't find{0}{1}{2} in the catalog.{3}"
            },
            ["weight_not_found_ask"] = new()
            {
                [Fr] = " Souhaitez-vous voir d'autres formats {0} ({1}) ?",
                [Nl] = " Wilt u andere formaten {0} ({1}) zien?",
                [En] = " Would you like to see other {0} ({1}) formats?"
            },
            ["weight_not_found_refine"] = new()
            {
                [Fr] = " Affinez marque, type ou poids.",
                [Nl] = " Preciseer merk, type of gewicht.",
                [En] = " Please refine brand, type or weight."
            },
            ["brand_not_found"] = new()
            {
                [Fr] = "Je n'ai trouvé aucun produit de la marque {0} dans le catalogue. Vérifiez l'orthographe ou essayez une autre marque / un type de produit.",
                [Nl] = "Ik vond geen producten van het merk {0} in de catalogus. Controleer de spelling of probeer een ander merk / producttype.",
                [En] = "I found no products from brand {0} in the catalog. Check the spelling or try another brand / product type."
            },
            ["brand_present_no_type"] = new()
            {
                [Fr] = "La marque {0} est présente, mais je n'ai pas de {1} {0} dans le catalogue. Précisez un autre type (plâtre, plaque, colle…) ou une autre marque.",
                [Nl] = "Het merk {0} is aanwezig, maar ik heb geen {1} {0} in de catalogus. Preciseer een ander type (gips, plaat, lijm…) of een ander merk.",
                [En] = "Brand {0} is present, but I don't have {1} {0} in the catalog. Specify another type (plaster, board, adhesive…) or another brand."
            },
            ["no_matching_materials"] = new()
            {
                [Fr] = "Je n'ai pas trouvé de parpaings/briques/mortier/ciment correspondants dans le catalogue. Affinez avec un matériau précis.",
                [Nl] = "Ik vond geen overeenkomende blokken/stenen/mortel/cement in de catalogus. Preciseer een materiaal.",
                [En] = "I couldn't find matching blocks/bricks/mortar/cement in the catalog. Please refine with a specific material."
            },
            ["no_matching_product"] = new()
            {
                [Fr] = "Je n'ai pas trouvé de produit correspondant dans le catalogue. Indiquez un matériau ou une marque précise (ex. Knauf, parpaing, brique, mortier, ciment).",
                [Nl] = "Ik vond geen overeenkomend product in de catalogus. Geef een materiaal of precies merk op (bv. Knauf, blok, steen, mortel, cement).",
                [En] = "I couldn't find a matching product in the catalog. Specify a material or brand (e.g. Knauf, block, brick, mortar, cement)."
            },
            ["vague_electrical"] = new()
            {
                [Fr] = "Le rayon électricité est large. Que cherchez-vous exactement : ampoules / LED, prises & interrupteurs, câbles, ou tableaux / disjoncteurs ?",
                [Nl] = "Het elektriciteitsassortiment is breed. Wat zoekt u precies: lampen / LED, stopcontacten & schakelaars, kabels, of borden / automaten?",
                [En] = "The electrical range is broad. What exactly are you looking for: bulbs / LED, sockets & switches, cables, or panels / breakers?"
            },
            ["vague_painting"] = new()
            {
                [Fr] = "Pour peindre : intérieur ou extérieur ? Peinture murale (muurverf / latex), sous-couche, ou outils (rouleau / pinceaux) ?",
                [Nl] = "Schilderen: binnen of buiten? Muurverf (latex), voorstrijk, of gereedschap (roller / penselen)?",
                [En] = "For painting: indoor or outdoor? Wall paint (latex), primer, or tools (roller / brushes)?"
            },
            ["vague_tiling"] = new()
            {
                [Fr] = "Carrelage : sol ou mur ? Format / couleur, ou plutôt colle et joints ?",
                [Nl] = "Tegels: vloer of muur? Formaat / kleur, of eerder lijm en voegen?",
                [En] = "Tiling: floor or wall? Size / colour, or rather adhesive and grout?"
            },
            ["vague_plumbing"] = new()
            {
                [Fr] = "Plomberie : robinetterie, PVC / tuyaux, évacuation, ou accessoires (joints, colliers) ?",
                [Nl] = "Sanitair: kranen, PVC / buizen, afvoer, of accessoires (pakkingen, beugels)?",
                [En] = "Plumbing: taps, PVC / pipes, drainage, or accessories (seals, clamps)?"
            },
            ["vague_garden"] = new()
            {
                [Fr] = "Jardin : aménagement (dalles, clôture), entretien (tondeuse, haie), ou nettoyage (souffleur, sacs) ?",
                [Nl] = "Tuin: aanleg (tegels, omheining), onderhoud (maaier, haag), of reiniging (bladblazer, zakken)?",
                [En] = "Garden: landscaping (slabs, fence), maintenance (mower, hedge), or cleaning (blower, bags)?"
            },
            ["vague_wall"] = new()
            {
                [Fr] = "Pour votre mur : briques, blocs / parpaings, ou mortier / ciment ?",
                [Nl] = "Voor uw muur: stenen, blokken, of mortel / cement?",
                [En] = "For your wall: bricks, blocks, or mortar / cement?"
            },
            ["wall_step_matches"] = new()
            {
                [Fr] = "(Étape « {0} » : {1} refs affichées sur {2} dans ce rayon — précisez marque / type pour affiner.)",
                [Nl] = "(Stap « {0} » : {1} refs getoond van {2} in dit assortiment — preciseer merk / type om te verfijnen.)",
                [En] = "(Step « {0} »: {1} refs shown of {2} in this range — refine brand / type for better matches.)"
            },
            ["product_word"] = new()
            {
                [Fr] = " produit",
                [Nl] = " product",
                [En] = " product"
            },
            ["in_weight"] = new()
            {
                [Fr] = " en {0}",
                [Nl] = " in {0}",
                [En] = " in {0}"
            }
        };

        public static string DomainDisplay(StoreChatSession? session, string? domainId, string? fallback = null)
        {
            var key = (domainId ?? string.Empty).ToLowerInvariant() switch
            {
                "painting" => "domain_painting",
                "wall_construction" => "domain_wall",
                "tiling" => "domain_tiling",
                "electrical" => "domain_electrical",
                "roofing" => "domain_roofing",
                "plumbing" => "domain_plumbing",
                _ => null
            };
            if (key == null)
                return fallback ?? domainId ?? "";
            return T(session, key);
        }

        /// <summary>Message « plus de produits » adapté au domaine (évite le hint jardin hors contexte).</summary>
        public static string MoreProductsEmpty(StoreChatSession? session)
        {
            var domain = (session?.ActiveProjectDomainId ?? string.Empty).ToLowerInvariant();
            var key = domain switch
            {
                "roofing" => "more_products_empty_roofing",
                "painting" => "more_products_empty_painting",
                "garden_cleaning" or "garden_landscaping" or "garden_maintenance" => "more_products_empty_garden",
                _ => "more_products_empty"
            };
            return T(session, key);
        }
    }
}
