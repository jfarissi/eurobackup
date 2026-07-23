using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Services.ErpSync;
using Backup.Web.Api.Server.Services.StoreChat;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Backup.Web.Api.Server.Services.SalesAssistant
{
    public interface ISalesComplementTool
    {
        Task<List<StoreChatProductSuggestionDto>> SearchAsync(
            StoreChatSession session,
            IReadOnlyList<string> hints,
            CancellationToken ct = default);
    }

    public class SalesComplementTool : ISalesComplementTool
    {
        private readonly IStorageBroker _storage;
        private readonly ISalesSemanticSearch _semanticSearch;
        private readonly ErpSyncOptions _erpOptions;

        public SalesComplementTool(
            IStorageBroker storage,
            ISalesSemanticSearch semanticSearch,
            IOptions<ErpSyncOptions> erpOptions)
        {
            _storage = storage;
            _semanticSearch = semanticSearch;
            _erpOptions = erpOptions.Value ?? new ErpSyncOptions();
        }

        public async Task<List<StoreChatProductSuggestionDto>> SearchAsync(
            StoreChatSession session,
            IReadOnlyList<string> hints,
            CancellationToken ct = default)
        {
            var complementProducts = new List<StoreChatProductSuggestionDto>();
            foreach (var hint in hints.Take(4))
            {
                var terms = ExpandComplementSearchTerms(hint);
                var hits = await SearchProductsByTermsAsync(terms, ct);

                // Appoint sémantique uniquement si SQL vide — puis filtre strict.
                if (hits.Count == 0)
                {
                    foreach (var term in terms.Take(2))
                    {
                        var sem = await _semanticSearch.SearchAsync(term, 6, ct);
                        hits.AddRange(sem);
                    }
                }

                var takeForHint = ProductsPerComplementHint(hint);
                var ranked = hits
                    .Where(h => complementProducts.All(p => p.ProductId != h.ProductId))
                    .Where(h => session.Cart.All(c => c.ErpProductId.ToString() != h.ProductId))
                    .Select(h =>
                    {
                        var hay = $"{h.Name} {h.Category} {h.Brand}".ToLowerInvariant();
                        return new { Product = h, Hay = hay, Score = ScoreComplementHit(hay, hint) };
                    })
                    .Where(x => x.Score > 0)
                    .Where(x => !ContainsCartStructureNoise(x.Hay, session))
                    .OrderByDescending(x => x.Score)
                    .ThenBy(x => x.Product.Name)
                    .Select(x => x.Product)
                    .Take(takeForHint)
                    .ToList();

                foreach (var best in ranked)
                {
                    best.SuggestedQuantity ??= 1;
                    complementProducts.Add(best);
                    if (complementProducts.Count >= 10)
                        return complementProducts;
                }
            }

            return complementProducts;
        }

        private static int ProductsPerComplementHint(string hint) =>
            hint.Trim().ToLowerInvariant() switch
            {
                "treillis" => 4,
                "ciment" => 3,
                "truelle" => 2,
                _ => 1
            };

        /// <summary>
        /// Évite de reproposer structure/liant déjà couverts par le panier lors des compléments.
        /// </summary>
        private static bool ContainsCartStructureNoise(string hay, StoreChatSession session)
        {
            var cart = string.Join(' ', session.Cart.Select(c => c.Name)).ToLowerInvariant();
            var cartHasStructure = cart.Contains("brique") || cart.Contains("baksteen") || cart.Contains("blok")
                                   || cart.Contains("steen") || cart.Contains("porotherm") || cart.Contains("silka");
            var cartHasBinder = cart.Contains("ciment") || cart.Contains("cement") || cart.Contains("mortier")
                                || cart.Contains("mortel");

            if (cartHasStructure && (hay.Contains("brique") || hay.Contains("baksteen") || hay.Contains("lijmblok")
                                     || hay.Contains("kalkzand") || hay.Contains("porotherm") || hay.Contains("snelbouw")))
                return true;

            return cartHasBinder && (hay.Contains("ciment") || hay.Contains("cement") || hay.Contains("mortier")
                                     || hay.Contains("mortel"));
        }

        /// <summary>Termes SQL stricts (pas de synonymes trop larges type « niveau »).</summary>
        private static IReadOnlyList<string> ExpandComplementSearchTerms(string hint)
        {
            return hint.Trim().ToLowerInvariant() switch
            {
                "truelle" => new[] { "truelle", "troffel", "truweel", "metseltroffel", "waterpas" },
                "treillis" => new[]
                {
                    "treillis", "wapeningsnet", "wapeningsgaas", "bewapeningsnet", "wapening", "mesh",
                    "zind", "grid", "betonijzer", "gaas"
                },
                "auge" => new[] { "auge", "mortelkuip", "speciekuip", "mengkuip", "emmer", "seau", "kuip" },
                "gants" => new[] { "handschoen", "handschoenen", "werkhandschoen", "gants", "gloves" },
                "ciment" => new[] { "ciment", "cement", "mortier", "mortel", "metselspecie" },
                "seau" => new[] { "seau", "auge", "emmer", "mortelkuip", "kuip" },
                "rouleau" => new[] { "rouleau", "roller", "verfroller" },
                "ruban" => new[] { "ruban", "masking", "schilderstape" },
                "sous-couche" => new[] { "sous-couche", "primer", "grondverf" },
                "colle carrelage" => new[] { "tegellijm", "colle carrelage", "colle" },
                "joint" => new[] { "voegsel", "voeg", "joint" },
                "primaire" => new[] { "primaire", "primer", "grondverf" },
                "bordure" => new[] { "bordure", "opsluitband", "border", "kantopsluiting" },
                "geotextile" => new[] { "geotextile", "géotextile", "anti-wortel", "worteldoek" },
                "sable" => new[] { "sable", "zand", "gravier", "grind", "stabilisé", "stabilise" },
                "cloture" => new[] { "cloture", "clôture", "schutting", "brise-vue", "gaas" },
                "souffleur" => new[] { "souffleur", "bladblazer", "blower", "balai" },
                _ => new[] { hint }
            };
        }

        private async Task<List<StoreChatProductSuggestionDto>> SearchProductsByTermsAsync(
            IReadOnlyList<string> terms,
            CancellationToken ct)
        {
            var results = new List<StoreChatProductSuggestionDto>();
            var seen = new HashSet<int>();

            foreach (var term in terms.Where(t => t.Length >= 3).Take(8))
            {
                var needle = term.ToLowerInvariant();
                var rows = await _storage.SelectAllErpProducts()
                    .AsNoTracking()
                    .Where(p =>
                        (p.Name != null && p.Name.ToLower().Contains(needle))
                        || (p.Name2 != null && p.Name2.ToLower().Contains(needle))
                        || (p.Reference != null && p.Reference.ToLower().Contains(needle))
                        || (p.Brand != null && p.Brand.ToLower().Contains(needle))
                        || (p.TypeName != null && p.TypeName.ToLower().Contains(needle))
                        || (p.SubTypeName != null && p.SubTypeName.ToLower().Contains(needle))
                        || (p.MainTypeName != null && p.MainTypeName.ToLower().Contains(needle)))
                    .OrderBy(p => p.Name)
                    .Take(20)
                    .Select(p => new
                    {
                        p.Id,
                        p.Name,
                        p.Name2,
                        p.Brand,
                        p.UnitPrice,
                        p.PriceHT,
                        p.MainTypeName,
                        p.TypeName,
                        p.SubTypeName,
                        p.PicName,
                        p.Reference
                    })
                    .ToListAsync(ct);

                foreach (var p in rows)
                {
                    if (!seen.Add(p.Id))
                        continue;

                    results.Add(new StoreChatProductSuggestionDto
                    {
                        ProductId = p.Id.ToString(CultureInfo.InvariantCulture),
                        Name = FormatProductDisplayName(p.Name, p.Name2, p.Reference, p.Id),
                        Brand = p.Brand,
                        Price = p.UnitPrice ?? p.PriceHT,
                        Category = string.Join(" / ", new[] { p.MainTypeName, p.TypeName, p.SubTypeName }
                            .Where(x => !string.IsNullOrWhiteSpace(x))),
                        SuggestedQuantity = 1,
                        ImageUrl = BuildProductImageUrl(p.PicName)
                    });
                }

                if (results.Count >= 40)
                    break;
            }

            return results;
        }

        /// <summary>Score &gt; 0 = accepté. Pénalise les faux positifs (Gauge⊃auge, Fuel Gauge…).</summary>
        private static int ScoreComplementHit(string hay, string hint)
        {
            if (string.IsNullOrWhiteSpace(hay))
                return 0;

            if (ContainsAnyLocal(hay, "fuel gauge", "flotteur", "remover", "bagues du", "blow gun", "gonflage"))
                return 0;

            if (hay.Contains("gauge", StringComparison.OrdinalIgnoreCase)
                && hint.Trim().Equals("auge", StringComparison.OrdinalIgnoreCase))
                return 0;

            return hint.Trim().ToLowerInvariant() switch
            {
                "truelle" => ScoreAny(hay,
                    ("truelle", 100), ("troffel", 100), ("truweel", 100), ("metseltroffel", 100),
                    ("waterpas", 70), ("spatel", 40)),
                "treillis" => ScoreAny(hay,
                    ("bewapeningsnet", 100), ("wapeningsnet", 100), ("wapeningsgaas", 100),
                    ("treillis", 90), ("wapening", 80), ("betonijzer", 75),
                    ("zind", 70), ("grid", 70), ("gaas", 55), ("mesh", 50)),
                "auge" or "seau" => ScoreAuge(hay),
                "gants" => ScoreGloves(hay),
                "ciment" => ScoreAny(hay, ("ciment", 100), ("cement", 100), ("mortier", 80), ("mortel", 80)),
                "bordure" => ScoreAny(hay, ("bordure", 100), ("opsluitband", 100), ("kantopsluiting", 90), ("border", 70)),
                "geotextile" => ScoreAny(hay, ("geotextile", 100), ("géotextile", 100), ("worteldoek", 90), ("anti-wortel", 80)),
                "sable" => ScoreAny(hay, ("sable", 100), ("zand", 100), ("gravier", 80), ("grind", 80)),
                "cloture" => ScoreAny(hay, ("clôture", 100), ("cloture", 100), ("schutting", 100), ("brise", 70)),
                "souffleur" => ScoreAny(hay, ("souffleur", 100), ("bladblazer", 100), ("blower", 80), ("balai", 60)),
                _ => hay.Contains(hint, StringComparison.OrdinalIgnoreCase) ? 50 : 0
            };
        }

        private static int ScoreAuge(string hay)
        {
            var score = ScoreAny(hay,
                ("mortelkuip", 100), ("speciekuip", 100), ("mengkuip", 100),
                ("emmer", 80), ("seau", 80));

            if (HasWord(hay, "auge"))
                score = Math.Max(score, 90);
            if (HasWord(hay, "kuip") || hay.Contains("kuip", StringComparison.OrdinalIgnoreCase))
                score = Math.Max(score, 70);

            return score;
        }

        private static bool HasWord(string hay, string word) =>
            Regex.IsMatch(hay, $@"\b{Regex.Escape(word)}\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static int ScoreGloves(string hay)
        {
            if (!ContainsAnyLocal(hay, "handschoen", "gant", "glove"))
                return 0;

            var score = 0;
            if (hay.Contains("handschoen", StringComparison.OrdinalIgnoreCase))
                score += 100;
            if (hay.Contains("werkhandschoen", StringComparison.OrdinalIgnoreCase))
                score += 30;
            if (hay.Contains("gant", StringComparison.OrdinalIgnoreCase))
                score += 40;
            if (hay.Contains("glove", StringComparison.OrdinalIgnoreCase))
                score += 30;

            if (ContainsAnyLocal(hay, "isolé", "isole", "insulated", "éléctrique", "electrique"))
                score -= 50;

            return score > 0 ? score : 0;
        }

        private static int ScoreAny(string hay, params (string Needle, int Score)[] scored)
        {
            var best = 0;
            foreach (var (needle, score) in scored)
            {
                if (hay.Contains(needle, StringComparison.OrdinalIgnoreCase) && score > best)
                    best = score;
            }

            return best;
        }

        private static bool ContainsAnyLocal(string hay, params string[] needles) =>
            needles.Any(n => hay.Contains(n, StringComparison.OrdinalIgnoreCase));

        private static string FormatProductDisplayName(string? name, string? name2, string? reference, int id)
        {
            var n1 = name?.Trim();
            var n2 = name2?.Trim();
            var name2IsLabel = !string.IsNullOrWhiteSpace(n2)
                               && n2!.Length <= 80
                               && !n2.Contains("wordt gebruikt", StringComparison.OrdinalIgnoreCase)
                               && !n2.Contains("geschikt voor", StringComparison.OrdinalIgnoreCase)
                               && !n2.Contains("toepasbaar", StringComparison.OrdinalIgnoreCase);

            if (!string.IsNullOrWhiteSpace(n1) && name2IsLabel
                && !string.Equals(n1, n2, StringComparison.OrdinalIgnoreCase))
                return $"{n1} — {n2}";
            if (!string.IsNullOrWhiteSpace(n1))
                return n1!;
            if (!string.IsNullOrWhiteSpace(n2))
                return n2!.Length <= 120 ? n2 : n2[..117] + "…";
            return reference ?? $"Produit {id}";
        }

        private string? BuildProductImageUrl(string? picName)
        {
            if (string.IsNullOrWhiteSpace(picName))
                return null;

            var file = picName.Trim().Replace('\\', '/');
            if (file.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || file.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return file;

            var baseUrl = (_erpOptions.ImageBaseUrl ?? string.Empty).TrimEnd('/');
            if (string.IsNullOrWhiteSpace(baseUrl))
                return null;

            while (file.StartsWith('/'))
                file = file[1..];

            return $"{baseUrl}/{file}";
        }
    }
}
