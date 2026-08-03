using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Backup.Web.Api.Server.Services.Purchases;

/// <summary>
/// Dérive un jeton marque depuis le nom fournisseur
/// (ex. "FF GROUP TOOL INDUSTRIES SA" → "FF GROUP") pour filtrer ErpBrands / ErpProducts.
/// </summary>
public static partial class SupplierBrandMatcher
{
    private static readonly Regex LegalSuffixRegex = LegalSuffixPattern();
    private static readonly Regex MultiSpaceRegex = MultiSpacePattern();

    /// <summary>
    /// Token utilisé pour Brand.Name LIKE '%token%'.
    /// Prefère les 2 premiers mots significatifs après retrait des formes juridiques.
    /// </summary>
    public static string? DeriveBrandToken(string? supplierName)
    {
        if (string.IsNullOrWhiteSpace(supplierName))
            return null;

        var cleaned = LegalSuffixRegex.Replace(supplierName.Trim(), " ");
        cleaned = MultiSpaceRegex.Replace(cleaned, " ").Trim();
        if (cleaned.Length == 0)
            return null;

        var words = cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (words.Length == 0)
            return null;

        // "FF GROUP TOOL INDUSTRIES" → "FF GROUP"
        if (words.Length >= 2)
            return $"{words[0]} {words[1]}";

        return words[0];
    }

    public static IReadOnlyList<string> DeriveBrandTokens(string? supplierName)
    {
        var primary = DeriveBrandToken(supplierName);
        if (primary == null)
            return Array.Empty<string>();

        var tokens = new List<string> { primary };
        // Aussi le 1er mot seul si le token a 2+ mots (ex. "FF" en secours — non : trop large)
        // On garde uniquement le token principal pour éviter le bruit.
        return tokens;
    }

    [GeneratedRegex(
        @"\b(SA|S\.A\.|NV|N\.V\.|BV|BVBA|SRL|SPRL|SAS|SARL|LTD|LIMITED|GMBH|INC|CORP|PLC|ASBL|VZW|AG|KG)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LegalSuffixPattern();

    [GeneratedRegex(@"\s+")]
    private static partial Regex MultiSpacePattern();
}
