using System;
using System.IO;

namespace Backup.Web.Api.Server.Services.ErpSync;

/// <summary>
/// Builds product image URLs for two catalogue modes:
/// - EuroBrico / ERP webservice: PicName = filename → same-origin proxy → ImageBaseUrl
/// - RapidAPI / pièces auto: PicName = absolute https URL → returned as-is (no EuroBrico proxy)
/// </summary>
public static class ErpProductImageUrls
{
    public const string ProxyPath = "/api/erp-products/image";

    public static bool IsAbsoluteHttpUrl(string? picName)
    {
        if (string.IsNullOrWhiteSpace(picName))
            return false;
        var raw = picName.Trim();
        return raw.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || raw.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// URL à exposer au client (assistant, API). Absolute RapidAPI → inchangé ;
    /// fichier EuroBrico → proxy /api/erp-products/image?f=…
    /// </summary>
    public static string? ToProxyUrl(string? picName)
    {
        if (string.IsNullOrWhiteSpace(picName))
            return null;

        var raw = picName.Trim();
        if (IsAbsoluteHttpUrl(raw))
            return raw;

        var file = NormalizeFileName(raw);
        if (file == null)
            return null;

        return $"{ProxyPath}?f={Uri.EscapeDataString(file)}";
    }

    public static string? ToUpstreamUrl(string? imageBaseUrl, string? picName)
    {
        // Ne jamais envoyer une URL S3/TecDoc vers le serveur images EuroBrico.
        if (IsAbsoluteHttpUrl(picName))
            return null;

        var file = NormalizeFileName(picName);
        if (file == null)
            return null;

        var baseUrl = ResolveImageBaseUrl(imageBaseUrl);
        if (string.IsNullOrWhiteSpace(baseUrl))
            return null;

        return $"{baseUrl}/{file}";
    }

    /// <summary>Base URL images EuroBrico (port 15022). Utilise la valeur par défaut si la config est vide.</summary>
    public static string? ResolveImageBaseUrl(string? imageBaseUrl)
    {
        var baseUrl = (imageBaseUrl ?? string.Empty).Trim().TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl))
            baseUrl = "http://eurobrico.ddns.net:15022";

        // Image server has no TLS — never call it with https.
        if (baseUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            baseUrl = "http://" + baseUrl["https://".Length..];

        return baseUrl;
    }

    public static string? NormalizeFileName(string? picName)
    {
        if (string.IsNullOrWhiteSpace(picName))
            return null;

        var raw = picName.Trim().Replace('\\', '/');

        // Absolute URLs are not EuroBrico filenames — caller must use IsAbsoluteHttpUrl / ToProxyUrl.
        if (IsAbsoluteHttpUrl(raw))
            return null;

        while (raw.StartsWith('/'))
            raw = raw[1..];

        // Keep only the file name (no directories / traversal).
        var file = Path.GetFileName(raw);
        if (string.IsNullOrWhiteSpace(file) || file.Contains("..", StringComparison.Ordinal))
            return null;

        return file;
    }
}
