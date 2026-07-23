using System.Text.RegularExpressions;

namespace Backup.Web.Api.Server.Services.SalesAssistant
{
    public static class SalesTextGuards
    {
        public static bool IsBareConfirmation(string text)
        {
            var trimmed = Regex.Replace((text ?? string.Empty).Trim().ToLowerInvariant(), @"[!?.…]+$", "").Trim();
            trimmed = Regex.Replace(trimmed, @"\s+", " ");
            return trimmed is "ok" or "okay" or "oké" or "oke" or "oui" or "ouais" or "yes" or "go"
                or "d'accord" or "daccord" or "vas-y" or "vas y" or "vasy" or "merci"
                or "ok go" or "parfait" or "nickel";
        }

        public static bool IsNewProjectText(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;

            var lower = text.Trim().ToLowerInvariant();
            if (lower is "nouveau projet" or "new project" or "nieuw project" or "reset" or "recommencer")
                return true;

            return Regex.IsMatch(lower, @"^(bonjour[,!]?\s+)?(je\s+(veux|voudrais)\s+)?(démarrer|demarrer|commencer|lancer)?\s*(un\s+)?nouveau\s+projet\b")
                   || Regex.IsMatch(lower, @"\b(start|new)\s+project\b");
        }

        public static bool IsExplicitWallIntent(string text)
        {
            var lower = text.ToLowerInvariant();
            return lower.Contains("construire un mur")
                   || lower.Contains("construction de mur")
                   || lower.Contains("mur de séparation")
                   || lower.Contains("mur de separation")
                   || lower.Contains("faire un mur")
                   || lower.Contains("monter un mur")
                   || lower.Contains("muur bouwen")
                   || lower.Contains("build a wall")
                   || lower.Contains("brick wall")
                   || (lower.Contains("mur") && (lower.Contains("construire") || Regex.IsMatch(lower, @"\d+\s*m")));
        }

        /// <summary>Accepte uniquement une origine http(s) absolue (anti open-redirect).</summary>
        public static string? ResolveClientReturnBaseUrl(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return null;
            if (!Uri.TryCreate(raw.Trim(), UriKind.Absolute, out var uri))
                return null;
            if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
                return null;
            if (string.IsNullOrWhiteSpace(uri.Host))
                return null;

            return uri.GetLeftPart(UriPartial.Authority).TrimEnd('/');
        }
    }
}
