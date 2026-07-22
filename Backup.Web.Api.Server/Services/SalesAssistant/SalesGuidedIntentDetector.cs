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
        Hesitation
    }

    public sealed class GuidedSalesSlots
    {
        public GuidedSalesIntent Intent { get; set; } = GuidedSalesIntent.None;
        public string? SkillLevel { get; set; }
        public decimal? BudgetMax { get; set; }
        public List<string> CompareBrands { get; set; } = new();
        public bool BudgetMentioned { get; set; }
        public bool SkillMentioned { get; set; }
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

            if (IsPackRequest(lower))
                slots.Intent = GuidedSalesIntent.PackRequest;
            else if (IsCompare(lower, slots))
                slots.Intent = GuidedSalesIntent.Compare;
            else if (IsWhyProduct(lower))
                slots.Intent = GuidedSalesIntent.WhyProduct;
            else if (IsHesitation(lower))
                slots.Intent = GuidedSalesIntent.Hesitation;

            return slots;
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

        private static bool IsCompare(string lower, GuidedSalesSlots slots)
        {
            if (!ContainsAny(lower, "compar", "différence entre", "difference entre", "versus", " vs ", " ou bien "))
                return false;

            var brands = Regex.Matches(lower, @"\b([a-z][a-z0-9-]{2,})\b")
                .Select(x => x.Value)
                .Where(t => t is not ("comparer" or "compare" or "comparatif" or "entre" or "avec" or "versus" or "ou" or "bien" or "les" or "des" or "produits" or "marques" or "produit" or "marque"))
                .Take(4)
                .ToList();

            // « comparer Knauf et Silka »
            var pair = Regex.Match(lower, @"compar\w*\s+([a-z0-9][\w-]{2,})\s+(?:et|vs|versus|\/|avec)\s+([a-z0-9][\w-]{2,})");
            if (pair.Success)
            {
                slots.CompareBrands.Add(pair.Groups[1].Value);
                slots.CompareBrands.Add(pair.Groups[2].Value);
            }
            else if (brands.Count >= 2)
            {
                slots.CompareBrands.AddRange(brands.Take(2));
            }

            return true;
        }

        private static bool IsWhyProduct(string lower) =>
            ContainsAny(lower, "pourquoi ce", "pourquoi le", "pourquoi cette", "pourquoi choisir", "pourquoi ce produit", "pourquoi ce ciment");

        private static bool IsHesitation(string lower) =>
            ContainsAny(lower, "je ne sais pas", "je sais pas", "pas sûr", "pas sur", "hésite", "hesite", "aucune idée", "aucune idee");

        private static bool ContainsAny(string hay, params string[] needles) =>
            needles.Any(n => hay.Contains(n, StringComparison.OrdinalIgnoreCase));
    }
}
