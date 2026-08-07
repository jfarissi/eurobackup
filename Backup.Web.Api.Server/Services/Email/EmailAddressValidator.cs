using System.Net.Mail;
using System.Text.RegularExpressions;

namespace Backup.Web.Api.Server.Services.Email
{
    public static class EmailAddressValidator
    {
        private static readonly Regex SimpleEmail =
            new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static bool IsValid(string? email)
        {
            if (string.IsNullOrWhiteSpace(email)) return false;
            email = email.Trim();
            if (!SimpleEmail.IsMatch(email)) return false;
            try
            {
                _ = new MailAddress(email);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static string? Normalize(string? email)
        {
            if (string.IsNullOrWhiteSpace(email)) return null;
            email = email.Trim();
            return IsValid(email) ? email : null;
        }

        public static IReadOnlyList<string> ParseList(string? raw, int max = 5)
        {
            if (string.IsNullOrWhiteSpace(raw)) return Array.Empty<string>();
            return raw.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(IsValid)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(max)
                .ToList();
        }
    }
}
