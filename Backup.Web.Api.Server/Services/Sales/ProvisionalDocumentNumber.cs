using System;

namespace Backup.Web.Api.Server.Services.Sales
{
    /// <summary>RG-FC6 / N2 : les brouillons n'utilisent pas la séquence légale.</summary>
    public static class ProvisionalDocumentNumber
    {
        public const string DraftPrefix = "DRAFT-";

        public static string Create() => DraftPrefix + Guid.NewGuid().ToString("N");

        public static bool IsProvisional(string? number) =>
            string.IsNullOrWhiteSpace(number)
            || number.StartsWith(DraftPrefix, StringComparison.OrdinalIgnoreCase)
            || number.StartsWith("BROUILLON-", StringComparison.OrdinalIgnoreCase);
    }
}
