using System;
using System.IO;

namespace Backup.Web.Api.Server.Services.ErpSync;

/// <summary>
/// Builds same-origin proxy URLs for ERP product images.
/// Direct http://eurobrico.ddns.net:15022 links break in browsers that upgrade the host to HTTPS (HSTS).
/// </summary>
public static class ErpProductImageUrls
{
    public const string ProxyPath = "/api/erp-products/image";

    public static string? ToProxyUrl(string? picName)
    {
        var file = NormalizeFileName(picName);
        if (file == null)
            return null;

        return $"{ProxyPath}?f={Uri.EscapeDataString(file)}";
    }

    public static string? ToUpstreamUrl(string? imageBaseUrl, string? picName)
    {
        var file = NormalizeFileName(picName);
        if (file == null)
            return null;

        var baseUrl = (imageBaseUrl ?? string.Empty).Trim().TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl))
            return null;

        // Image server has no TLS — never call it with https.
        if (baseUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            baseUrl = "http://" + baseUrl["https://".Length..];

        return $"{baseUrl}/{file}";
    }

    public static string? NormalizeFileName(string? picName)
    {
        if (string.IsNullOrWhiteSpace(picName))
            return null;

        var raw = picName.Trim().Replace('\\', '/');

        if (raw.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || raw.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri))
                return null;
            raw = uri.AbsolutePath;
        }

        while (raw.StartsWith('/'))
            raw = raw[1..];

        // Keep only the file name (no directories / traversal).
        var file = Path.GetFileName(raw);
        if (string.IsNullOrWhiteSpace(file) || file.Contains("..", StringComparison.Ordinal))
            return null;

        return file;
    }
}
