using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Services.StoreChat;
using Microsoft.EntityFrameworkCore;

namespace Backup.Web.Api.Server.Services.SalesAssistant
{
    public sealed class SalesPhotoClassificationDto
    {
        public string Label { get; set; } = "unknown";
        public string ProjectHint { get; set; } = "Other";
        public string DomainId { get; set; } = string.Empty;
        public string DomainLabel { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
    }

    public interface ISalesPhotoClassifier
    {
        SalesPhotoClassificationDto Classify(string? caption, string? fileName);
    }

    /// <summary>Classification basique par légende / nom de fichier (sans modèle vision).</summary>
    public class SalesPhotoClassifier : ISalesPhotoClassifier
    {
        public SalesPhotoClassificationDto Classify(string? caption, string? fileName)
        {
            var hay = $"{caption} {fileName}".ToLowerInvariant();

            if (ContainsAny(hay, "fissure", "crack", "crevasse"))
            {
                return new SalesPhotoClassificationDto
                {
                    Label = "crack",
                    ProjectHint = "Wall",
                    DomainId = "wall_construction",
                    DomainLabel = "Réparation mur / fissure",
                    Summary = "Photo classée : fissure murale. Je prépare un parcours réparation (enduit, bande, peinture)."
                };
            }

            if (ContainsAny(hay, "carrel", "tegel", "faïence", "faience", "salle de bain", "sdb"))
            {
                return new SalesPhotoClassificationDto
                {
                    Label = "tiling",
                    ProjectHint = "Bathroom",
                    DomainId = "tiling",
                    DomainLabel = "Carrelage",
                    Summary = "Photo classée : carrelage / SDB. Pack carrelage + colle + joint recommandé."
                };
            }

            if (ContainsAny(hay, "mur", "wall", "brique", "parpaing", "maçon", "macon"))
            {
                return new SalesPhotoClassificationDto
                {
                    Label = "wall",
                    ProjectHint = "Wall",
                    DomainId = "wall_construction",
                    DomainLabel = "Construction de mur",
                    Summary = "Photo classée : mur. Donnez les dimensions (ex. 7 m × 2 m) pour estimer les quantités."
                };
            }

            if (ContainsAny(hay, "peinture", "paint", "mur peint", "plafond"))
            {
                return new SalesPhotoClassificationDto
                {
                    Label = "painting",
                    ProjectHint = "Painting",
                    DomainId = "painting",
                    DomainLabel = "Peinture",
                    Summary = "Photo classée : peinture. Indiquez la surface m² pour le besoin en litres."
                };
            }

            return new SalesPhotoClassificationDto
            {
                Label = "unknown",
                Summary = "Photo reçue. Précisez ce que l'on voit (mur, carrelage, fissure, peinture…) pour adapter le projet."
            };
        }

        private static bool ContainsAny(string hay, params string[] needles) =>
            needles.Any(n => hay.Contains(n, StringComparison.OrdinalIgnoreCase));
    }

    public sealed class SalesWallSchemaDto
    {
        public decimal? LengthM { get; set; }
        public decimal? HeightM { get; set; }
        public decimal? AreaM2 { get; set; }
        public decimal OpeningsM2 { get; set; }
        public decimal NetAreaM2 { get; set; }
        public string Summary { get; set; } = string.Empty;
    }

    public interface ISalesWallSchemaParser
    {
        bool TryParse(string text, StoreChatSession session, out SalesWallSchemaDto schema);
    }

    public class SalesWallSchemaParser : ISalesWallSchemaParser
    {
        public bool TryParse(string text, StoreChatSession session, out SalesWallSchemaDto schema)
        {
            schema = new SalesWallSchemaDto();
            var lower = (text ?? string.Empty).ToLowerInvariant().Replace(',', '.');

            var isSchema = ContainsAny(lower, "schéma", "schema", "ouverture", "porte", "fenêtre", "fenetre", "window", "door")
                           && (ContainsAny(lower, "mur", "wall") || session.WallLengthM is > 0 || Regex.IsMatch(lower, @"\d+\s*m"));
            if (!isSchema && !ContainsAny(lower, "avec porte", "avec fenêtre", "avec fenetre", "moins la porte"))
                return false;

            decimal? length = session.WallLengthM;
            decimal? height = session.WallHeightM;

            var pair = Regex.Match(lower, @"(\d+(?:\.\d+)?)\s*m\D{0,24}(\d+(?:\.\d+)?)\s*m");
            if (pair.Success
                && decimal.TryParse(pair.Groups[1].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var a)
                && decimal.TryParse(pair.Groups[2].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var b))
            {
                length ??= a;
                height ??= b;
            }

            if (length is null or <= 0 || height is null or <= 0)
                return false;

            session.WallLengthM = length;
            session.WallHeightM = height;
            session.ActiveProjectDomainId ??= "wall_construction";
            session.ActiveProjectDomainLabel ??= "Construction de mur";

            decimal openings = 0;
            foreach (Match m in Regex.Matches(lower, @"(?:porte|door)\s*(?:de\s*)?(\d+(?:\.\d+)?)\s*(?:m|cm)?"))
            {
                var v = ParseOpening(m.Groups[1].Value, m.Value.Contains("cm"));
                // porte standard hauteur ~2.1 si seule largeur
                openings += v <= 1.5m ? v * 2.1m : v;
            }

            foreach (Match m in Regex.Matches(lower, @"(?:fenêtre|fenetre|window)\s*(?:de\s*)?(\d+(?:\.\d+)?)\s*(?:x|×|\*)\s*(\d+(?:\.\d+)?)"))
            {
                if (decimal.TryParse(m.Groups[1].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var w)
                    && decimal.TryParse(m.Groups[2].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var h))
                {
                    if (m.Value.Contains("cm"))
                    {
                        w /= 100m;
                        h /= 100m;
                    }

                    openings += w * h;
                }
            }

            var gross = Math.Round(length.Value * height.Value, 2);
            var net = Math.Max(0, Math.Round(gross - openings, 2));
            var bricks = Math.Ceiling(net * 55m);
            var mortar = Math.Ceiling(net / 4m);

            schema = new SalesWallSchemaDto
            {
                LengthM = length,
                HeightM = height,
                AreaM2 = gross,
                OpeningsM2 = Math.Round(openings, 2),
                NetAreaM2 = net,
                Summary = $"Schéma mur {length:0.##}×{height:0.##} m → {gross:0.##} m² brut"
                          + (openings > 0 ? $", ouvertures −{openings:0.##} m² → net {net:0.##} m²" : "")
                          + $".\nEstimation : ≈ {bricks:0} briques, ≈ {mortar:0} sacs mortier (+10 % perte conseillé)."
            };
            return true;
        }

        private static decimal ParseOpening(string raw, bool cmHint)
        {
            if (!decimal.TryParse(raw.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var v))
                return 0.9m;
            return cmHint || v > 3 ? v / 100m : v;
        }

        private static bool ContainsAny(string hay, params string[] needles) =>
            needles.Any(n => hay.Contains(n, StringComparison.OrdinalIgnoreCase));
    }

    public interface ISalesSemanticSearch
    {
        Task<List<StoreChatProductSuggestionDto>> SearchAsync(string query, int take = 5, CancellationToken ct = default);
    }

    /// <summary>Appoint « embeddings » léger : similarité cosinus bag-of-words sur libellés catalogue.</summary>
    public class SalesBagOfWordsSearch : ISalesSemanticSearch
    {
        private readonly IStorageBroker _storage;
        private static List<(int Id, string Name, string? Brand, string? Category, decimal? Price, string? Pic, HashSet<string> Tokens)>? _cache;
        private static DateTime _cacheAt = DateTime.MinValue;

        public SalesBagOfWordsSearch(IStorageBroker storage)
        {
            _storage = storage;
        }

        public async Task<List<StoreChatProductSuggestionDto>> SearchAsync(
            string query,
            int take = 5,
            CancellationToken ct = default)
        {
            var qTokens = Tokenize(query);
            if (qTokens.Count == 0)
                return new List<StoreChatProductSuggestionDto>();

            await EnsureCacheAsync(ct);
            var scored = _cache!
                .Select(p => (p, Score: Jaccard(qTokens, p.Tokens)))
                .Where(x => x.Score > 0)
                .OrderByDescending(x => x.Score)
                .Take(take)
                .Select(x => new StoreChatProductSuggestionDto
                {
                    ProductId = x.p.Id.ToString(CultureInfo.InvariantCulture),
                    Name = x.p.Name,
                    Brand = x.p.Brand,
                    Category = x.p.Category,
                    Price = x.p.Price
                })
                .ToList();

            return scored;
        }

        private async Task EnsureCacheAsync(CancellationToken ct)
        {
            if (_cache != null && DateTime.UtcNow - _cacheAt < TimeSpan.FromMinutes(15))
                return;

            var rows = await _storage.SelectAllErpProducts()
                .AsNoTracking()
                .Where(p => p.Name != null && p.Name != "")
                .OrderByDescending(p => p.Id)
                .Take(4000)
                .Select(p => new
                {
                    p.Id,
                    p.Name,
                    p.Name2,
                    p.Brand,
                    p.MainTypeName,
                    p.TypeName,
                    p.SubTypeName,
                    p.UnitPrice,
                    p.PriceHT
                })
                .ToListAsync(ct);

            _cache = rows.Select(p =>
            {
                var name = string.IsNullOrWhiteSpace(p.Name2) ? p.Name! : $"{p.Name} — {p.Name2}";
                var cat = string.Join(" / ", new[] { p.MainTypeName, p.TypeName, p.SubTypeName }.Where(x => !string.IsNullOrWhiteSpace(x)));
                var text = $"{name} {p.Brand} {cat}";
                return (p.Id, name, p.Brand, cat, p.UnitPrice ?? p.PriceHT, (string?)null, Tokenize(text));
            }).ToList();
            _cacheAt = DateTime.UtcNow;
        }

        private static HashSet<string> Tokenize(string text)
        {
            return Regex.Matches((text ?? string.Empty).ToLowerInvariant(), @"[a-zàâäéèêëïîôùûüç0-9]{3,}")
                .Select(m => m.Value)
                .Where(t => t is not ("les" or "des" or "une" or "pour" or "avec" or "dans"))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        private static double Jaccard(HashSet<string> a, HashSet<string> b)
        {
            if (a.Count == 0 || b.Count == 0)
                return 0;
            var inter = a.Count(x => b.Contains(x));
            var union = a.Count + b.Count - inter;
            return union == 0 ? 0 : (double)inter / union;
        }
    }
}
