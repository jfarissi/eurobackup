using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Backup.Web.Api.Server.Services.StoreChat;

namespace Backup.Web.Api.Server.Services.SalesAssistant.Guides
{
    /// <summary>Guide générique : avancement d'après marqueurs panier / intention.</summary>
    public abstract class MarkerProjectGuide : IProjectGuide
    {
        public abstract string DomainId { get; }
        public abstract string Title { get; }
        public abstract IReadOnlyList<ProjectGuideStep> Families { get; }
        public virtual int BaseFamilyCount => Math.Min(2, Families.Count);

        public virtual ProjectGuideStep ResolveNext(
            StoreChatSession session,
            string? userText,
            ProductSearchFilter? meta = null)
        {
            var text = (userText ?? string.Empty).ToLowerInvariant();
            var hints = string.Join(' ', meta?.TypeHints ?? session.SearchTypeHints).ToLowerInvariant();
            var hay = $"{text} {hints}";
            var cart = SalesProjectGuide.CartOnlyHay(session);

            foreach (var step in Families)
            {
                if (!CartHas(cart, step) && LooksLike(hay, step))
                    return step;
            }

            foreach (var step in Families)
            {
                if (!CartHas(cart, step))
                    return step;
            }

            return Families[^1];
        }

        public virtual bool IsComplete(StoreChatSession session)
        {
            var cart = SalesProjectGuide.CartOnlyHay(session);
            return Families.All(f => CartHas(cart, f));
        }

        public virtual bool IsBaseComplete(StoreChatSession session)
        {
            var cart = SalesProjectGuide.CartOnlyHay(session);
            return Families.Take(BaseFamilyCount).All(f => CartHas(cart, f));
        }

        public bool CartHasStep(StoreChatSession session, ProjectGuideStep step) =>
            CartHas(SalesProjectGuide.CartOnlyHay(session), step);

        public virtual string BuildChecklist(StoreChatSession session, ProjectGuideStep focus)
        {
            var cart = SalesProjectGuide.CartOnlyHay(session);
            var complete = IsComplete(session);
            var sb = new StringBuilder();
            var titleKey = $"guide_{DomainId}_title";
            var title = SalesLocale.T(session, titleKey);
            if (string.Equals(title, titleKey, StringComparison.Ordinal))
                title = $"{Title} ({SalesLocale.T(session, "guide_family_suffix")}) :";
            sb.AppendLine(title);

            for (var i = 0; i < Families.Count; i++)
            {
                var step = Families[i];
                var done = CartHas(cart, step);
                var here = !complete && step.Id == focus.Id
                    ? SalesLocale.T(session, "guide_step_now")
                    : "";
                var state = done ? "✓" : "○";
                var aisleHint = LocalizedAisle(session, step);
                var aisle = string.IsNullOrWhiteSpace(aisleHint)
                    ? ""
                    : SalesLocale.T(session, "guide_aisle", aisleHint);
                var line = $"{state} {i + 1}. {LocalizedLabel(session, step)}{aisle}{here}";
                if (i < Families.Count - 1)
                    sb.AppendLine(line);
                else
                    sb.Append(line);
            }

            if (complete)
                sb.Append("\n" + SalesLocale.T(session, "guide_complete"));
            else
                sb.Append("\n" + SalesLocale.T(session, "guide_next_hint"));

            return sb.ToString().Trim();
        }

        public string FocusLabel(ProjectGuideStep step) => step.Label;

        public string FocusLabel(StoreChatSession session, ProjectGuideStep step) =>
            LocalizedLabel(session, step);

        protected string LocalizedLabel(StoreChatSession session, ProjectGuideStep step)
        {
            var key = $"guide_{DomainId}_{step.Id}";
            var t = SalesLocale.T(session, key);
            return string.Equals(t, key, StringComparison.Ordinal) ? step.Label : t;
        }

        protected string LocalizedAisle(StoreChatSession session, ProjectGuideStep step)
        {
            var key = $"guide_{DomainId}_aisle_{step.Id}";
            var t = SalesLocale.T(session, key);
            if (!string.Equals(t, key, StringComparison.Ordinal))
                return t;
            return step.AisleHint ?? "";
        }

        protected static bool CartHas(string cartHay, ProjectGuideStep step) =>
            step.CartMarkers.Any(m => cartHay.Contains(m, StringComparison.OrdinalIgnoreCase));

        protected static bool LooksLike(string hay, ProjectGuideStep step)
        {
            var markers = step.LookMarkers.Length > 0 ? step.LookMarkers : step.CartMarkers;
            return markers.Any(m => hay.Contains(m, StringComparison.OrdinalIgnoreCase));
        }
    }
}
