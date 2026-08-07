using System.Globalization;
using System.Text.RegularExpressions;

namespace Backup.Web.Api.Server.Services.Email
{
    public static class EmailTemplateRenderer
    {
        private static readonly Regex Token = new(@"\{([a-z0-9_.]+)\}", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static string Render(string template, IReadOnlyDictionary<string, string> variables)
        {
            if (string.IsNullOrEmpty(template)) return string.Empty;
            return Token.Replace(template, m =>
            {
                var key = m.Groups[1].Value;
                return variables.TryGetValue(key, out var v) ? v : m.Value;
            });
        }

        public static string FormatMoney(decimal amount, string currency = "EUR") =>
            amount.ToString("C", CultureInfo.GetCultureInfo("fr-BE"));

        public static string FormatDate(DateTime? date) =>
            date.HasValue ? date.Value.ToString("dd/MM/yyyy", CultureInfo.GetCultureInfo("fr-BE")) : "—";
    }
}
