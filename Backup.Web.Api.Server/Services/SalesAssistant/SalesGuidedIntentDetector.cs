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
        CartComplements
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
            else if (IsWallSchema(lower, session))
                slots.Intent = GuidedSalesIntent.WallSchema;
            else if (IsPackRequest(lower))
                slots.Intent = GuidedSalesIntent.PackRequest;
            else if (IsCartComplements(lower))
                slots.Intent = GuidedSalesIntent.CartComplements;
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

        private static bool IsCartComplements(string lower) =>
            ContainsAny(lower,
                "autres produits", "autre produit", "besoin d'autre", "besoin d autre",
                "aurai besoin", "aurais besoin", "ai besoin d'autre", "ai-je besoin",
                "il me manque", "me manque", "manquera", "complément", "complement",
                "pour mon panier", "pour le panier", "pour ces deux", "pour ce que j'ai",
                "que j'ai ajout", "que j ai ajout",
                "quoi d'autre", "quoi d autre", "encore besoin", "produits en plus");

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
