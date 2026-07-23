using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Services.StoreChat;
using Microsoft.EntityFrameworkCore;

namespace Backup.Web.Api.Server.Services.SalesAssistant
{
    public interface ISalesContextDetector
    {
        Task DetectBrandAsync(StoreChatSession session, string text, CancellationToken ct = default);
        void DetectDomain(StoreChatSession session, string text);
        void CollectMaterialHints(StoreChatSession session, string text);
        void ParseWallDimensions(StoreChatSession session, string text);
        void UpdateStickySearchFilters(StoreChatSession session, string text);
        ProductSearchFilter BuildSearchMeta(StoreChatSession session, string text);
        Task EnrichSessionAsync(StoreChatSession session, string text, CancellationToken ct = default);
    }

    public class SalesContextDetector : ISalesContextDetector
    {
        private readonly IStorageBroker _storage;

        private static readonly Dictionary<string, string> BrandTypoAliases = new(StringComparer.OrdinalIgnoreCase)
        {
            ["knau"] = "Knauf",
            ["knauff"] = "Knauf",
            ["silka"] = "Silka",
            ["poroterm"] = "Porotherm",
        };

        private static List<string>? _cachedBrandNames;
        private static DateTime _cachedBrandNamesAtUtc = DateTime.MinValue;
        private static readonly TimeSpan BrandCacheTtl = TimeSpan.FromMinutes(10);

        public SalesContextDetector(IStorageBroker storage) => _storage = storage;

        public async Task EnrichSessionAsync(StoreChatSession session, string text, CancellationToken ct = default)
        {
            await DetectBrandAsync(session, text, ct);
            DetectDomain(session, text);
            ParseWallDimensions(session, text);
            CollectMaterialHints(session, text);
            UpdateStickySearchFilters(session, text);
        }

        public async Task DetectBrandAsync(StoreChatSession session, string text, CancellationToken ct = default)
        {
            var lower = text.ToLowerInvariant();
            var brands = await GetCatalogBrandNamesAsync(ct);
            if (brands.Count == 0)
                return;

            foreach (var brand in brands.OrderByDescending(b => b.Length))
            {
                var needle = brand.ToLowerInvariant();
                if (needle.Length < 2)
                    continue;

                if (Regex.IsMatch(lower, $@"\b{Regex.Escape(needle)}\b", RegexOptions.IgnoreCase)
                    || lower.Contains(needle, StringComparison.OrdinalIgnoreCase))
                {
                    session.PreferredBrand = brand;
                    return;
                }
            }

            foreach (var alias in BrandTypoAliases.OrderByDescending(kv => kv.Key.Length))
            {
                if (!Regex.IsMatch(lower, $@"\b{Regex.Escape(alias.Key)}\b", RegexOptions.IgnoreCase)
                    && !lower.Contains(alias.Key, StringComparison.OrdinalIgnoreCase))
                    continue;

                var resolved = brands.FirstOrDefault(b =>
                    b.Equals(alias.Value, StringComparison.OrdinalIgnoreCase)
                    || b.StartsWith(alias.Value, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrWhiteSpace(resolved))
                {
                    session.PreferredBrand = resolved;
                    return;
                }
            }

            var tokens = Regex.Matches(lower, @"[a-z0-9][\w-]{2,}")
                .Select(m => m.Value)
                .Where(t => !SalesMaterialLexicon.StopWords.Contains(t)
                            && !SalesMaterialLexicon.MaterialSynonyms.ContainsKey(t))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var token in tokens.OrderByDescending(t => t.Length))
            {
                var startsWith = brands
                    .Where(b => b.StartsWith(token, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                if (startsWith.Count == 1)
                {
                    session.PreferredBrand = startsWith[0];
                    return;
                }
            }

            var marqueMatch = Regex.Match(lower, @"\b(?:marque|brand)\s+([a-z0-9][\w-]{2,})\b");
            if (!marqueMatch.Success)
                return;

            var brandToken = marqueMatch.Groups[1].Value;
            var hit = brands.FirstOrDefault(b =>
                b.Equals(brandToken, StringComparison.OrdinalIgnoreCase)
                || b.StartsWith(brandToken, StringComparison.OrdinalIgnoreCase)
                || b.Contains(brandToken, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(hit))
                session.PreferredBrand = hit;
        }

        public void DetectDomain(StoreChatSession session, string text)
        {
            var lower = text.ToLowerInvariant();
            var brandQuery = !string.IsNullOrWhiteSpace(session.PreferredBrand);
            (string id, string label, string[] keys)[] domains =
            {
                ("wall_construction", "Construction de mur", new[]
                {
                    "construire un mur", "construction de mur", "mur de séparation", "mur de separation",
                    "mur de soutènement", "mur de souteinement", "briquetage", "maçonner un mur", "maconner un mur",
                    "muur bouwen", "metselwerk", "build a wall", "brick wall", "masonry wall",
                    "je construis un mur", "faire un mur", "monter un mur"
                }),
                ("painting", "Peinture", new[] { "peinture", "peindre", "rouleau à peindre", "sous-couche", "lasurer" }),
                ("tiling", "Carrelage", new[] { "carrelage", "carreau", "faïence", "faience" }),
                ("plumbing", "Plomberie", new[] { "plomberie", "robinet", "tuyau", "wc", "siphon" }),
                ("electrical", "Électricité", new[]
                {
                    "électri", "electri", "prise", "interrupteur", "câble", "cable", "led",
                    "ampoule", "ampoules", "lampe", "lampes", "e27", "e14"
                }),
                ("garden_cleaning", "Nettoyage jardin", new[]
                {
                    "nettoyer mon jardin", "nettoyer le jardin", "nettoyer jardin", "nettoyage jardin",
                    "nettoyer mon tuin", "ramasser les feuilles", "feuilles mortes", "souffler les feuilles",
                    "nettoyer", "nettoyage", "bladblazer", "souffleur"
                }),
                ("garden_maintenance", "Entretien jardin", new[]
                {
                    "tondeuse", "tondre", "grasmaaier", "haie", "haag", "taille-haie", "heggenschaar", "gazon"
                }),
                ("garden_landscaping", "Aménagement jardin", new[]
                {
                    "aménager mon jardin", "amenager mon jardin", "aménager le jardin", "amenager le jardin",
                    "aménagement jardin", "amenagement jardin", "aménager", "amenager", "aménagement", "amenagement",
                    "jardin", "tuin", "garden"
                }),
            };

            var previousDomain = session.ActiveProjectDomainId;
            foreach (var domain in domains)
            {
                if (!domain.keys.Any(key => lower.Contains(key)))
                    continue;

                if (brandQuery && domain.id == "wall_construction")
                    continue;

                session.ActiveProjectDomainId = domain.id;
                session.ActiveProjectDomainLabel = domain.label;
                if (!string.Equals(previousDomain, domain.id, StringComparison.OrdinalIgnoreCase)
                    && IsGardenDomain(domain.id))
                {
                    session.MaterialHints.RemoveAll(h =>
                        h.Contains("tondeuse", StringComparison.OrdinalIgnoreCase)
                        || h.Contains("grasmaaier", StringComparison.OrdinalIgnoreCase)
                        || h.Contains("gazon", StringComparison.OrdinalIgnoreCase));
                }

                return;
            }

            if (!brandQuery
                && lower.Contains("mur")
                && (lower.Contains("construire") || Regex.IsMatch(lower, @"\d+\s*m")))
            {
                session.ActiveProjectDomainId = "wall_construction";
                session.ActiveProjectDomainLabel = "Construction de mur";
            }
        }

        public void CollectMaterialHints(StoreChatSession session, string text)
        {
            var lower = text.ToLowerInvariant();
            var brandMode = !string.IsNullOrWhiteSpace(session.PreferredBrand);

            foreach (var hint in SalesMaterialLexicon.ExtractTypeHints(text))
            {
                if (!session.MaterialHints.Contains(hint, StringComparer.OrdinalIgnoreCase))
                    session.MaterialHints.Add(hint);
            }

            if (brandMode)
                return;

            if ((lower.Contains("construire") && lower.Contains("mur"))
                || string.Equals(session.ActiveProjectDomainId, "wall_construction", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var hint in new[] { "parpaing", "brique", "mortier", "ciment" })
                {
                    if (!session.MaterialHints.Contains(hint, StringComparer.OrdinalIgnoreCase))
                        session.MaterialHints.Add(hint);
                }
            }
        }

        public void ParseWallDimensions(StoreChatSession session, string text)
        {
            var lower = text.ToLowerInvariant().Replace(',', '.');
            decimal? length = null;
            decimal? height = null;

            var lengthMatch = Regex.Match(lower, @"(?:longueur|longeur|long|lengte|length|l)\s*(?:de|:)?\s*(\d+(?:\.\d+)?)\s*(?:m|metres?|mètres?|mettres?)");
            var heightMatch = Regex.Match(lower, @"(?:hauteur|haut|hoogte|height|h)\s*(?:de|:)?\s*(\d+(?:\.\d+)?)\s*(?:m|metres?|mètres?|mettres?)");
            if (lengthMatch.Success && decimal.TryParse(lengthMatch.Groups[1].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsedLength))
                length = parsedLength;
            if (heightMatch.Success && decimal.TryParse(heightMatch.Groups[1].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsedHeight))
                height = parsedHeight;

            if (length is null || height is null)
            {
                var pair = Regex.Match(lower, @"(\d+(?:\.\d+)?)\s*(?:m|metres?|mètres?|mettres?)\D{0,32}(\d+(?:\.\d+)?)\s*(?:m|metres?|mètres?|mettres?)");
                if (pair.Success
                    && decimal.TryParse(pair.Groups[1].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var first)
                    && decimal.TryParse(pair.Groups[2].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var second))
                {
                    length ??= first;
                    height ??= second;
                }
            }

            if (length is > 0)
                session.WallLengthM = length;
            if (height is > 0)
                session.WallHeightM = height;
        }

        public void UpdateStickySearchFilters(StoreChatSession session, string text)
        {
            var weight = ParseWeightKgFromText(text);
            if (weight is > 0)
                session.PreferredWeightKg = weight;

            foreach (var hint in SalesMaterialLexicon.ExtractTypeHints(text))
            {
                if (!session.SearchTypeHints.Contains(hint, StringComparer.OrdinalIgnoreCase))
                    session.SearchTypeHints.Add(hint);
            }
        }

        public ProductSearchFilter BuildSearchMeta(StoreChatSession session, string text)
        {
            var fromText = SalesMaterialLexicon.ExtractTypeHints(text);
            var types = fromText.Count > 0 ? fromText : session.SearchTypeHints.ToList();

            return new ProductSearchFilter
            {
                Brand = session.PreferredBrand,
                Categories = types,
                WeightKg = ParseWeightKgFromText(text) ?? session.PreferredWeightKg,
                IsYesNoBrandQuestion = IsYesNoBrandQuestion(text)
            };
        }

        private async Task<List<string>> GetCatalogBrandNamesAsync(CancellationToken ct)
        {
            if (_cachedBrandNames != null && DateTime.UtcNow - _cachedBrandNamesAtUtc < BrandCacheTtl)
                return _cachedBrandNames;

            var fromTable = await _storage.SelectAllErpBrands()
                .AsNoTracking()
                .Where(b => b.IsActive && b.Name != null && b.Name != "")
                .Select(b => b.Name)
                .ToListAsync(ct);

            var names = fromTable.Count > 0
                ? fromTable
                : await _storage.SelectAllErpProducts()
                    .AsNoTracking()
                    .Where(p => p.Brand != null && p.Brand != "")
                    .Select(p => p.Brand!)
                    .Distinct()
                    .ToListAsync(ct);

            _cachedBrandNames = names
                .Select(name => name.Trim())
                .Where(name => name.Length >= 2)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(name => name.Length)
                .ToList();
            _cachedBrandNamesAtUtc = DateTime.UtcNow;
            return _cachedBrandNames;
        }

        private static bool IsGardenDomain(string? domainId) =>
            domainId is "garden_cleaning" or "garden_landscaping" or "garden_maintenance";

        private static bool IsYesNoBrandQuestion(string text)
        {
            var lower = text.ToLowerInvariant();
            return lower.Contains("est-ce que")
                   || lower.Contains("est ce que")
                   || lower.Contains("avez-vous")
                   || lower.Contains("a-t-il")
                   || lower.Contains("a t il")
                   || Regex.IsMatch(lower, @"\b(ont|a)\s+du\b")
                   || lower.Contains("propose") && (lower.Contains("?") || lower.Contains("ciment") || lower.Contains("produit"));
        }

        private static decimal? ParseWeightKgFromText(string text)
        {
            var lower = text.ToLowerInvariant().Replace(',', '.');
            var match = Regex.Match(lower, @"(\d+(?:\.\d+)?)\s*kg");
            if (match.Success
                && decimal.TryParse(match.Groups[1].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var kg)
                && kg > 0)
                return kg;

            match = Regex.Match(lower, @"\b(?:sacs?|zakken?|bags?)\s*(?:de|van|of)?\s*(\d+(?:\.\d+)?)\b");
            if (match.Success
                && decimal.TryParse(match.Groups[1].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var bag)
                && bag >= 1 && bag <= 100)
                return bag;

            return null;
        }
    }
}
