using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Backup.Web.Api.Server.Services.StoreChat;

namespace Backup.Web.Api.Server.Services.SalesAssistant
{
    public enum GuidedSalesIntent
    {
        None,
        PackRequest,
        Compare,
        WhyProduct,
        Hesitation,
        Tips,
        Savings,
        Promos,
        Logistics,
        Planning,
        ResumeProject,
        Style,
        SemanticSearch,
        WallSchema,
        CartComplements,
        ConfirmComplements,
        DirectComplement,
        MoreProducts
    }

    public sealed class GuidedSalesSlots
    {
        public GuidedSalesIntent Intent { get; set; } = GuidedSalesIntent.None;
        public string? SkillLevel { get; set; }
        public decimal? BudgetMax { get; set; }
        public List<string> CompareBrands { get; set; } = new();
        public bool BudgetMentioned { get; set; }
        public bool SkillMentioned { get; set; }
        public string? Style { get; set; }
        public string? DirectComplementHint { get; set; }
    }

    public interface ISalesGuidedIntentDetector
    {
        GuidedSalesSlots Detect(string text, StoreChatSession session);
    }

    public class SalesGuidedIntentDetector : ISalesGuidedIntentDetector
    {
        public GuidedSalesSlots Detect(string text, StoreChatSession session)
        {
            var lower = (text ?? string.Empty).ToLowerInvariant();
            var slots = new GuidedSalesSlots();

            DetectSkill(lower, slots, session);
            DetectBudget(lower, slots, session);
            DetectCustomer(lower, session);

            if (IsResume(lower))
                slots.Intent = GuidedSalesIntent.ResumeProject;
            else if (IsConfirmComplements(lower, session))
                slots.Intent = GuidedSalesIntent.ConfirmComplements;
            else if (IsDirectComplementKeyword(lower, out var complementHint))
            {
                slots.Intent = GuidedSalesIntent.DirectComplement;
                slots.DirectComplementHint = complementHint;
            }
            else if (SalesComplementRules.TryOverrideIntent(text, session) is GuidedSalesIntent contextual)
            {
                // Contexte métier (base panier complète + suite) → CartComplements, avant MoreProducts.
                slots.Intent = contextual;
            }
            else if (IsWallSchema(lower, session))
                slots.Intent = GuidedSalesIntent.WallSchema;
            else if (IsPackRequest(lower))
                slots.Intent = GuidedSalesIntent.PackRequest;
            else if (IsCartComplements(lower, session))
                slots.Intent = GuidedSalesIntent.CartComplements;
            else if (IsMoreProducts(lower))
                slots.Intent = GuidedSalesIntent.MoreProducts;
            else if (IsCompare(lower, slots))
                slots.Intent = GuidedSalesIntent.Compare;
            else if (IsSavings(lower))
                slots.Intent = GuidedSalesIntent.Savings;
            else if (IsPromos(lower))
                slots.Intent = GuidedSalesIntent.Promos;
            else if (IsTips(lower))
                slots.Intent = GuidedSalesIntent.Tips;
            else if (IsLogistics(lower))
                slots.Intent = GuidedSalesIntent.Logistics;
            else if (IsPlanning(lower))
                slots.Intent = GuidedSalesIntent.Planning;
            else if (IsWhyProduct(lower))
                slots.Intent = GuidedSalesIntent.WhyProduct;
            else if (IsSemantic(lower))
                slots.Intent = GuidedSalesIntent.SemanticSearch;
            else if (IsHesitation(lower))
                slots.Intent = GuidedSalesIntent.Hesitation;
            else if (IsStyle(lower, slots, session))
                slots.Intent = GuidedSalesIntent.Style;

            // Filet : MoreProducts alors que la base chantier est complète → compléments.
            if (SalesComplementRules.ShouldRedirectMoreProductsToComplements(slots.Intent, text, session))
                slots.Intent = GuidedSalesIntent.CartComplements;

            return slots;
        }

        private static void DetectCustomer(string lower, StoreChatSession session)
        {
            var m = Regex.Match(lower, @"\b(?:client|customer)\s*[:=]?\s*([a-z0-9\-]{2,64})\b");
            if (m.Success)
                session.CustomerId = m.Groups[1].Value;
        }

        private static void DetectSkill(string lower, GuidedSalesSlots slots, StoreChatSession session)
        {
            if (ContainsAny(lower, "débutant", "debutant", "novice", "jamais fait", "première fois", "premiere fois", "diy débutant"))
            {
                slots.SkillLevel = "Beginner";
                slots.SkillMentioned = true;
                session.SkillLevel = "Beginner";
                return;
            }

            if (ContainsAny(lower, "bricoleur", "diy", "amateur"))
            {
                slots.SkillLevel = "Diy";
                slots.SkillMentioned = true;
                session.SkillLevel = "Diy";
                return;
            }

            if (ContainsAny(lower, "professionnel", "pro ", " je suis pro", "entreprise", "artisan", "maçonn", "maconn"))
            {
                slots.SkillLevel = "Pro";
                slots.SkillMentioned = true;
                session.SkillLevel = "Pro";
                return;
            }

            slots.SkillLevel = session.SkillLevel;
        }

        private static void DetectBudget(string lower, GuidedSalesSlots slots, StoreChatSession session)
        {
            var m = Regex.Match(lower, @"(?:budget|maximum|max|jusqu['’]?à|jusqua|moins de|pas plus de)\s*(\d+(?:[.,]\d+)?)\s*(?:€|euros?)?");
            if (!m.Success)
                m = Regex.Match(lower, @"(\d+(?:[.,]\d+)?)\s*(?:€|euros?)\s*(?:max|maximum|budget)?");

            if (m.Success
                && decimal.TryParse(m.Groups[1].Value.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var budget)
                && budget > 0)
            {
                slots.BudgetMax = budget;
                slots.BudgetMentioned = true;
                session.BudgetMax = budget;
                return;
            }

            slots.BudgetMax = session.BudgetMax;
        }

        private static bool IsPackRequest(string lower) =>
            ContainsAny(lower,
                "tout pour ce mur", "tout pour le mur", "pack mur", "kit mur",
                "donnez-moi tout", "donne moi tout", "pack complet", "kit complet",
                "tout le nécessaire", "tout le necessaire", "pack salle de bain", "pack sdb",
                "pack peinture", "kit peinture");

        private static bool IsConfirmComplements(string lower, StoreChatSession session)
        {
            // Après conseil panier / échec recherche, « ok » doit chercher les compléments — pas le mur.
            var canConfirm = session.AwaitingComplementConfirm
                             || session.PendingComplementHints.Count > 0
                             || string.Equals(session.LastActionType, "CART_ADVICE", StringComparison.OrdinalIgnoreCase);
            if (!canConfirm)
                return false;

            var trimmed = Regex.Replace(lower.Trim(), @"[!?.…]+$", "").Trim();
            trimmed = Regex.Replace(trimmed, @"\s+", " ");

            // Confirmations exactes / quasi exactes
            if (trimmed is "ok" or "okay" or "oké" or "oke" or "oui" or "ouais" or "ouai" or "yes" or "yep"
                or "go" or "ok go" or "okgo" or "ok go!" or "let's go" or "lets go"
                or "vas-y" or "vas y" or "vasy" or "allez" or "allez-y" or "allez y"
                or "d'accord" or "daccord" or "d accord" or "dac" or "d'ac" or "deal"
                or "parfait" or "nickel" or "super" or "top" or "bien" or "très bien" or "tres bien"
                or "volontiers" or "avec plaisir" or "pourquoi pas" or "carrément" or "carrement"
                or "bien sûr" or "bien sur" or "ok merci" or "oui merci" or "merci" or "thanks"
                or "cherche" or "cherche-les" or "cherche les" or "cherche ça" or "cherche ca"
                or "montre" or "montrez" or "montre-les" or "montre les" or "montre moi" or "montrez-moi"
                or "affiche" or "affiche-les" or "affiche les" or "propose" or "propose-les"
                or "je veux bien" or "je veux ça" or "je veux ca" or "ça me va" or "ca me va"
                or "c'est bon" or "cest bon" or "go ahead" or "go on" or "do it"
                or "ja" or "oké go" or "oke go")
                return true;

            // Phrase courte de confirmation (max ~8 mots)
            var words = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length <= 8
                && ContainsAny(trimmed,
                    "d'accord", "daccord", "ok go", "vas-y", "vas y", "allez-y",
                    "cherche les", "cherche-les", "montre les", "montre-les", "affiche les",
                    "oui cherche", "oui vas", "oui ok", "ok oui", "oui go",
                    "les compléments", "les complements", "ces compléments", "ces complements",
                    "ceux-là", "ceux la", "go pour", "ok pour", "d'accord pour",
                    "volontiers", "avec plaisir", "pourquoi pas", "ça me va", "ca me va",
                    "c'est bon", "cest bon", "je veux bien", "parfait go", "nickel go",
                    "ok cherche", "ok montre", "d'accord cherche", "d'accord montre"))
                return true;

            return false;
        }

        private static bool IsMoreProducts(string lower)
        {
            var trimmed = Regex.Replace(lower.Trim(), @"[!?.…]+$", "").Trim();
            trimmed = Regex.Replace(trimmed, @"\s+", " ");

            // Laisser CartComplements gérer « autres … à ajouter / panier ».
            if (ContainsAny(trimmed, "ajouter", "ajout", "panier", "complément", "complement",
                    "accessoire", "outillage", "manque", "supplément", "supplement"))
                return false;

            return trimmed is "autres produits" or "autre produit" or "autres" or "autre chose"
                or "d'autres" or "d autres" or "montre autre" or "montre autres"
                or "encore des produits" or "autres suggestions" or "autre suggestion"
                || ContainsAny(trimmed,
                    "autres produits", "autre produit", "d'autres produits", "d autres produits",
                    "montre autre", "montre d'autres", "encore des idées", "autre chose pour");
        }

        /// <summary>
        /// Lexique court de secours — le contexte (SalesComplementRules) prime.
        /// </summary>
        private static bool IsCartComplements(string lower, StoreChatSession session)
        {
            // Si le contexte métier offre déjà les compléments, les formulations « suite » suffisent.
            if (SalesComplementRules.ShouldOfferComplements(session)
                && (ContainsAny(lower, "autre", "autres", "encore", "manque", "ajouter", "produit",
                        "complément", "complement", "accessoire", "quoi d'autre", "il me faut")
                    || ContainsAny(lower, "panier")))
                return true;

            return ContainsAny(lower,
                "complément", "complement", "compléments", "complements",
                "accessoire", "accessoires", "outillage",
                "quoi d'autre", "quoi d autre", "quoi encore",
                "il manque", "il me faut", "me manque",
                "pour mon panier", "pour le panier",
                "qu'est-ce qui manque", "ce qui manque",
                "autres pour", "autre pour");
        }
        /// <summary>Demande directe d'un complément (ex. coller le libellé « Gants — … »).</summary>
        private static bool IsDirectComplementKeyword(string lower, out string? hint)
        {
            hint = null;
            var trimmed = Regex.Replace(lower.Trim(), @"[!?.…]+$", "").Trim();
            // Libellé reco collé : « Gants — Protection lors du malaxage. »
            var head = trimmed.Split(new[] { '—', '-', '–', ':' }, 2)[0].Trim();

            if (ContainsAny(head, "handschoen", "gants", "gant ", "gloves", "werkhandschoen")
                || head is "gant" or "gants")
            {
                hint = "gants";
                return true;
            }

            if (ContainsAny(head, "treillis", "wapening", "bewapeningsnet", "mesh")
                || head is "treillis")
            {
                hint = "treillis";
                return true;
            }

            if (ContainsAny(head, "truelle", "troffel", "truweel", "waterpas")
                || head is "truelle" or "troffel")
            {
                hint = "truelle";
                return true;
            }

            if (ContainsAny(head, "auge", "mortelkuip", "seau", "emmer", "mengkuip")
                || head is "auge" or "seau" or "emmer")
            {
                // « gauge » contient « auge » en sous-chaîne.
                if (head.Contains("gauge", StringComparison.OrdinalIgnoreCase))
                    return false;
                hint = "auge";
                return true;
            }

            return false;
        }

        private static bool IsCompare(string lower, GuidedSalesSlots slots)
        {
            if (!ContainsAny(lower, "compar", "différence entre", "difference entre", "versus", " vs "))
                return false;

            var pair = Regex.Match(lower, @"compar\w*\s+([a-z0-9][\w-]{2,})\s+(?:et|vs|versus|\/|avec)\s+([a-z0-9][\w-]{2,})");
            if (pair.Success)
            {
                slots.CompareBrands.Add(pair.Groups[1].Value);
                slots.CompareBrands.Add(pair.Groups[2].Value);
            }

            return true;
        }

        private static bool IsWhyProduct(string lower) =>
            ContainsAny(lower, "pourquoi ce", "pourquoi le", "pourquoi cette", "pourquoi choisir", "pourquoi ce produit", "pourquoi ce ciment");

        private static bool IsHesitation(string lower) =>
            ContainsAny(lower, "je ne sais pas", "je sais pas", "pas sûr", "pas sur", "hésite", "hesite", "aucune idée", "aucune idee");

        private static bool IsTips(string lower) =>
            ContainsAny(lower, "astuce", "conseil chantier", "perte", "+10", "sous-couche", "sous couche", "tips");

        private static bool IsSavings(string lower) =>
            ContainsAny(lower, "économie", "economie", "moins cher", "différence de prix", "difference de prix", "a vs b", "économiser", "economiser");

        private static bool IsPromos(string lower) =>
            ContainsAny(lower, "promo", "promotion", "réduction panier", "reduction panier", "offre panier");

        private static bool IsLogistics(string lower) =>
            ContainsAny(lower, "livraison", "retrait", "trop lourd", "logistique", "transport", "poids panier");

        private static bool IsPlanning(string lower) =>
            ContainsAny(lower, "planning", "étapes", "etapes", "phases", "planning chantier", "dans quel ordre");

        private static bool IsResume(string lower) =>
            ContainsAny(lower, "reprendre projet", "reprise projet", "charger projet", "projet #", "project #", "resume project");

        private static bool IsSemantic(string lower) =>
            ContainsAny(lower, "cherche quelque chose comme", "produit similaire", "ressemble à", "ressemble a", "sémantique", "semantique");

        private static bool IsWallSchema(string lower, StoreChatSession session) =>
            (ContainsAny(lower, "schéma", "schema", "avec porte", "avec fenêtre", "avec fenetre", "ouverture")
             && (ContainsAny(lower, "mur", "wall") || session.WallLengthM is > 0 || Regex.IsMatch(lower, @"\d+\s*m")));

        private static bool IsStyle(string lower, GuidedSalesSlots slots, StoreChatSession session)
        {
            if (!ContainsAny(lower, "scandinave", "moderne", "industriel", "classique", "style ", "déco", "deco", "ambiance"))
                return false;

            if (ContainsAny(lower, "scandinave", "nordique"))
                slots.Style = "scandinave";
            else if (ContainsAny(lower, "moderne", "minimal"))
                slots.Style = "moderne";
            else if (ContainsAny(lower, "industriel"))
                slots.Style = "industriel";
            else if (ContainsAny(lower, "classique"))
                slots.Style = "classique";
            else
                slots.Style = "personnalisé";

            session.PreferredStyle = slots.Style;
            return true;
        }

        private static bool ContainsAny(string hay, params string[] needles) =>
            needles.Any(n => hay.Contains(n, StringComparison.OrdinalIgnoreCase));
    }
}
