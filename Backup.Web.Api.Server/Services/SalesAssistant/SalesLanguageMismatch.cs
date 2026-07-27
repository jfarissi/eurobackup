using System;
using System.Linq;
using System.Text.RegularExpressions;
using Backup.Web.Api.Server.Services.StoreChat;

namespace Backup.Web.Api.Server.Services.SalesAssistant
{
    /// <summary>
    /// Détecte un décalage langue UI ↔ langue du message (heuristique légère, sans LLM).
    /// Avertissement seulement — ne change pas PreferredLanguage.
    /// </summary>
    public static class SalesLanguageMismatch
    {
        private static readonly string[] FrMarkers =
        [
            " je ", " j'", " il me ", " peindre ", " peinture ", " pour mon ", " pour ma ",
            " matériaux ", " materiaux ", " toiture ", " voulez ", " avez ", " besoin ",
            " réparer ", " reparer ", " longueur ", " hauteur ", " mur de ", " je veux ",
            " je voudrais ", " il me faut ", " des ", " les ", " une ", " mon ", " ma "
        ];

        private static readonly string[] NlMarkers =
        [
            " ik ", " mijn ", " wil ", " voor ", " schilderen ", " muurverf ", " dak ",
            " meter ", " graag ", " hebt ", " kunt ", " nodig ", " repareren ",
            " lengte ", " hoogte ", " ik wil ", " ik heb ", " voor mijn ", " lang ",
            " hoog ", " verven ", " winkelwagen ", " offerte "
        ];

        private static readonly string[] EnMarkers =
        [
            " i ", " my ", " need ", " want ", " paint ", " wall ", " roof ", " please ",
            " materials ", " for my ", " how much ", " can you "
        ];

        public static string? DetectMessageLanguage(string? text)
        {
            if (string.IsNullOrWhiteSpace(text) || text.Trim().Length < 14)
                return null;

            if (SalesTextGuards.IsBareConfirmation(text) || SalesTextGuards.IsNewProjectText(text))
                return null;

            var padded = " " + text.Trim().ToLowerInvariant() + " ";
            padded = Regex.Replace(padded, @"\s+", " ");

            var fr = FrMarkers.Count(m => padded.Contains(m, StringComparison.Ordinal));
            var nl = NlMarkers.Count(m => padded.Contains(m, StringComparison.Ordinal));
            var en = EnMarkers.Count(m => padded.Contains(m, StringComparison.Ordinal));

            // Accents / mots typiques FR.
            if (Regex.IsMatch(padded, @"[àâçéèêëîïôùûüœ]"))
                fr += 2;
            if (Regex.IsMatch(padded, @"\b(de|het|een|van|voor|mijn|ik)\b"))
                nl += 1;

            var best = Math.Max(fr, Math.Max(nl, en));
            if (best < 2)
                return null;

            // Ambigu FR/NL proche → pas d'avertissement.
            if (fr == nl && fr >= en)
                return null;

            if (fr == best) return SalesLocale.Fr;
            if (nl == best) return SalesLocale.Nl;
            if (en == best) return SalesLocale.En;
            return null;
        }

        public static string MaybePrependWarning(StoreChatSession session, string? userText, string reply)
        {
            if (string.IsNullOrWhiteSpace(reply))
                return reply;

            var detected = DetectMessageLanguage(userText);
            if (detected == null)
                return reply;

            var ui = SalesLocale.Of(session);
            if (string.Equals(detected, ui, StringComparison.OrdinalIgnoreCase))
                return reply;

            var warn = SalesLocale.T(session, "lang_mismatch_warn",
                LocalizedLanguageName(session, ui),
                LocalizedLanguageName(session, detected));

            if (reply.Contains(warn, StringComparison.Ordinal))
                return reply;

            return warn + "\n\n" + reply;
        }

        private static string LocalizedLanguageName(StoreChatSession session, string lang) =>
            SalesLocale.Normalize(lang) switch
            {
                SalesLocale.Nl => SalesLocale.T(session, "lang_name_nl"),
                SalesLocale.En => SalesLocale.T(session, "lang_name_en"),
                _ => SalesLocale.T(session, "lang_name_fr")
            };
    }
}
