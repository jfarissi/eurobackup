using System;

namespace Backup.Web.Api.Server.Services.Documents.Parsing
{
    /// <summary>
    /// Helper class pour gérer les ProductKey de manière cohérente.
    /// Les ProductKey sont stockées SANS préfixe dans la base de données.
    /// On utilise directement les valeurs brutes (ProductCode, EAN, ou nom normalisé).
    /// </summary>
    public static class ProductKeyHelper
    {
        /// <summary>
        /// Génère une ProductKey à partir d'une DocumentLine.
        /// Priorité: ProductCode > EAN > nom normalisé
        /// </summary>
        public static string GetProductKey(Models.DocumentLine line)
        {
            if (!string.IsNullOrWhiteSpace(line.ProductCode))
                return line.ProductCode.Trim();
            if (!string.IsNullOrWhiteSpace(line.Ean))
                return line.Ean.Trim();
            var norm = ProductTextNormalizer.Normalize(line.Product ?? string.Empty);
            return norm;
        }

        /// <summary>
        /// Génère une ProductKey à partir de ProductCode, EAN, ou nom de produit.
        /// Priorité: ProductCode > EAN > nom normalisé
        /// </summary>
        public static string GetProductKey(string? productCode, string? ean, string? productName)
        {
            if (!string.IsNullOrWhiteSpace(productCode))
                return productCode.Trim();
            if (!string.IsNullOrWhiteSpace(ean))
                return ean.Trim();
            var norm = ProductTextNormalizer.Normalize(productName ?? string.Empty);
            return norm;
        }

        /// <summary>
        /// Normalise une ProductKey (trim et supprime les préfixes C:, E:, N: si présents).
        /// </summary>
        public static string Normalize(string productKey)
        {
            if (string.IsNullOrWhiteSpace(productKey)) return productKey;
            productKey = productKey.Trim();
            // Supprimer les préfixes C:, E:, N: si présents (pour compatibilité avec anciennes données)
            if (productKey.StartsWith("C:", StringComparison.OrdinalIgnoreCase) ||
                productKey.StartsWith("E:", StringComparison.OrdinalIgnoreCase) ||
                productKey.StartsWith("N:", StringComparison.OrdinalIgnoreCase))
            {
                return productKey.Substring(2);
            }
            return productKey;
        }
    }
}

