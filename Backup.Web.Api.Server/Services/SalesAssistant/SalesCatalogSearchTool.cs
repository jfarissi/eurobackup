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
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Backup.Web.Api.Server.Services.SalesAssistant
{
    public interface ISalesCatalogSearchTool
    {
        Task<List<StoreChatProductSuggestionDto>> SearchAsync(
            string text,
            StoreChatSession session,
            ProductSearchFilter meta,
            CancellationToken ct = default,
            HashSet<string>? excludeProductIds = null);
    }

    public class SalesCatalogSearchTool : ISalesCatalogSearchTool
    {
        private readonly IStorageBroker _storage;
        private readonly StoreChatOptions _options;
        private readonly ErpSyncOptions _erpOptions;
        private readonly ILogger<SalesCatalogSearchTool> _logger;
        private const decimal BricksPerM2 = 55m;
        private const decimal ParpaingsPerM2 = 12.5m;
        private const decimal MortarKgPerM2 = 30m;
        private const decimal DefaultBagKg = 25m;

        public SalesCatalogSearchTool(
            IStorageBroker storage,
            IOptions<StoreChatOptions> options,
            IOptions<ErpSyncOptions> erpOptions,
            ILogger<SalesCatalogSearchTool> logger)
        {
            _storage = storage;
            _options = options.Value ?? new StoreChatOptions();
            _erpOptions = erpOptions.Value ?? new ErpSyncOptions();
            _logger = logger;
        }
        private static readonly Dictionary<string, string[]> DomainSearchTerms = new(StringComparer.OrdinalIgnoreCase)
        {
            ["wall_construction"] = new[]
            {
                "parpaing", "brique", "mortier", "ciment", "agglo", "moellon",
                "baksteen", "snelbouwsteen", "betonblok", "mortel", "cement", "metselwerk",
                "brick", "mortar", "concrete block"
            },
            ["painting"] = new[]
            {
                "peinture", "rouleau", "enduit", "sous-couche",
                "verf", "muurverf", "kwast", "roller",
                "paint", "roller", "primer"
            },
            ["tiling"] = new[]
            {
                "carrelage", "colle", "joint", "carreau",
                "tegel", "tegellijm", "voegsel",
                "tile", "adhesive", "grout"
            },
            ["plumbing"] = new[]
            {
                "robinet", "tuyau", "siphon", "pvc",
                "kraan", "leiding", "buis",
                "tap", "pipe", "plumbing"
            },
            ["electrical"] = new[]
            {
                // Pas de « wire » : matche trop de Wire Stripper USAG.
                "prise", "interrupteur", "câble", "cable", "led", "ampoule", "lampe",
                "stopcontact", "schakelaar", "draad", "lampje", "gloeilamp",
                "socket", "switch", "bulb"
            },
            ["garden_cleaning"] = new[]
            {
                "souffleur", "bladblazer", "rateau", "râteau", "balai",
                "hogedruk", "nettoyeur", "haute pression", "tuinafval",
                "blad", "bladeren", "afvalzak", "sac jardin", "blower"
            },
            ["garden_landscaping"] = new[]
            {
                "terrasse", "dalle", "bordure", "cloture", "clôture",
                "schutting", "tegel", "tegels", "grind", "gravier",
                "pot", "plante", "tuin", "jardin", "gazon artificiel"
            },
            ["garden_maintenance"] = new[]
            {
                "tondeuse", "grasmaaier", "haie", "haag", "gazon",
                "heggenschaar", "taille-haie", "lawnmower", "hedge"
            },
        };

        private static readonly string[] MasonryPositive = new[]
        {
            // FR
            "parpaing", "brique", "briques", "mortier", "ciment", "agglo", "agglom", "moellon",
            "hourdis", "maçon", "macon", "béton", "beton", "chaux", "sable", "gravier",
            // NL — matériaux (pas outils)
            "baksteen", "snelbouwsteen", "metselsteen", "betonblok", "snelbouwblok", "cellenblok",
            "cellenbeton", "kalkzandsteen", "lijmblok", "mortel", "metselmortel", "voegmortel", "cement",
            "metselwerk", "bouwmaterialen", "bouwmaterial", "zand", "grind", "wapening", "stenen",
            // EN
            "brick", "bricks", "mortar", "cement", "concrete block", "cinder block", "masonry",
            "sand", "gravel", "rebar"
        };

        private static readonly string[] MasonryNoise = new[]
        {
            "flexi", "schuur", "sandpaper", "duracell", "battery", "batterij", "trolley",
            "module", "affuter", "frees", "filter", "kool", "charbon", "bague", "blocage",
            "trowel floreffe", "humiblock", "hellico", "helico", "bijl", "monoblock", "monobloc",
            "schuurblok", "sanding block", "power block",
            // Outils qui citent baksteen/beton comme usage
            "boor voor", "boren voor", "zaagblad", "reciprozaag", "lijnspanner", "carbide tip",
            "set boren", "drill bit", "saw blade", "stuc primer", "stuc-primer", "primer"
        };

        private static readonly string[] ToolMarkers = new[]
        {
            "boor", "boren", "zaagblad", "zaagbladen", "recipro", "lijnspanner", "carbide",
            "drill", "spanner", "frees", "beitel", "hamer", "truelle", "trowel", "kwast",
            "roller", "primer", "schuur", "sandpaper", "set boren", "gereedschap"
        };

        private static readonly string[] MaterialUnitMarkers = new[]
        {
            "lijmblok", "kalkzandsteen", "betonblok", "snelbouwblok", "cellenblok", "cellenbeton",
            "baksteen", "snelbouwsteen", "metselsteen", "parpaing", "hourdis", "ytong",
            "mortel", "metselmortel", "voegmortel", "mortier", "ciment", "cement", "mortar",
            "brique", "brick", "concrete block", "moellon", "agglo"
        };
        public async Task<List<StoreChatProductSuggestionDto>> SearchAsync(
            string text,
            StoreChatSession session,
            ProductSearchFilter meta,
            CancellationToken ct = default,
            HashSet<string>? excludeProductIds = null)
        {
            var brandMode = !string.IsNullOrWhiteSpace(meta.Brand);
            // Marque demandée → ne jamais basculer en mode mur (évite Silka/Coeck pour « Knauf ciment »).
            var wallMode = !brandMode
                && string.Equals(session.ActiveProjectDomainId, "wall_construction", StringComparison.OrdinalIgnoreCase);

            var terms = BuildSearchTerms(text, session, brandMode);
            var scores = new Dictionary<int, ScoredProduct>();

            if (brandMode)
            {
                await AccumulateBrandSearchAsync(scores, meta.Brand!, ct);
                foreach (var typeTerm in meta.TypeHints.SelectMany(SalesMaterialLexicon.ExpandTypeHintTerms).Distinct(StringComparer.OrdinalIgnoreCase).Take(12))
                    await AccumulateSearchTermAsync(scores, typeTerm, ct);
            }
            else
            {
                if (terms.Count == 0)
                    return new List<StoreChatProductSuggestionDto>();

                foreach (var term in terms.Take(24))
                    await AccumulateSearchTermAsync(scores, term, ct);

                if (wallMode)
                    await EnrichWallCatalogCandidatesAsync(scores, ct);
            }

            if (scores.Count == 0)
                return new List<StoreChatProductSuggestionDto>();

            ApplyGardenIntentFilter(scores, session.ActiveProjectDomainId);
            ApplyLightingIntentFilter(scores, text);
            DemoteClearanceNoise(scores, session.ActiveProjectDomainId);

            IEnumerable<ScoredProduct> ranked = scores.Values;

            if (brandMode)
            {
                foreach (var p in scores.Values)
                    p.Score += BrandMatchBoost(p, meta.Brand!);

                var brandMatches = scores.Values
                    .Where(p => MatchesBrand(p, meta.Brand!))
                    .ToList();

                if (brandMatches.Count == 0)
                {
                    meta.Outcome = ProductSearchOutcome.BrandNotFound;
                    return new List<StoreChatProductSuggestionDto>();
                }

                if (meta.TypeHints.Count > 0)
                {
                    var typed = brandMatches
                        .Where(p => MatchesTypeHints(p, meta.TypeHints))
                        .ToList();

                    // Ciment : préférer les vrais ciments (pas colles/joints seuls).
                    if (meta.TypeHints.Any(t => t.Equals("ciment", StringComparison.OrdinalIgnoreCase)))
                    {
                        var strictCement = typed.Where(IsCementProduct).ToList();
                        if (strictCement.Count > 0)
                            typed = strictCement;
                    }

                    typed = typed
                        .OrderByDescending(p => p.Score + TypeExactBoost(p, meta.TypeHints))
                        .ThenBy(p => p.Name)
                        .ToList();

                    if (typed.Count > 0)
                    {
                        meta.Outcome = ProductSearchOutcome.BrandAndType;
                        ranked = typed;
                    }
                    else
                    {
                        meta.Outcome = ProductSearchOutcome.BrandWithoutType;
                        ranked = brandMatches
                            .OrderByDescending(p => p.Score)
                            .ThenBy(p => p.Name);
                    }
                }
                else
                {
                    meta.Outcome = ProductSearchOutcome.BrandOnly;
                    ranked = brandMatches
                        .OrderByDescending(p => p.Score)
                        .ThenBy(p => p.Name);
                }
            }
            else if (wallMode)
            {
                var focus = SalesProjectGuide.ResolveWallFamily(session, text, meta);
                meta.WallGuideFamily = focus;

                if (focus == WallGuideFamily.Reinforcement)
                    await EnrichWallReinforcementCandidatesAsync(scores, ct);
                else if (focus == WallGuideFamily.Tools)
                    await EnrichWallToolCandidatesAsync(scores, ct);
                else if (focus == WallGuideFamily.Binder)
                    await EnrichWallCatalogCandidatesAsync(scores, ct);

                var classified = scores.Values
                    .Select(p =>
                    {
                        var kind = ClassifyWallProduct(p);
                        p.Score += MasonryBoost(p);
                        return (Product: p, Kind: kind);
                    })
                    .ToList();

                ranked = focus switch
                {
                    WallGuideFamily.Binder => DeduplicateWallVariants(
                            classified
                                .Where(x => x.Kind == WallProductKind.Mortar)
                                .OrderByDescending(x => MortarPriority(x.Product))
                                .ThenByDescending(x => x.Product.Score)
                                .ThenBy(x => x.Product.Name)
                                .Select(x => x.Product))
                        .Take(Math.Max(12, _options.MaxProductResults))
                        .ToList(),

                    WallGuideFamily.Reinforcement => DeduplicateWallVariants(
                            classified
                                .Where(x => x.Kind == WallProductKind.Mesh)
                                .OrderByDescending(x => x.Product.Score)
                                .ThenBy(x => x.Product.Name)
                                .Select(x => x.Product))
                        .Take(Math.Max(10, _options.MaxProductResults))
                        .ToList(),

                    WallGuideFamily.Tools => DeduplicateWallVariants(
                            classified
                                .Where(x => x.Kind == WallProductKind.Tool)
                                .OrderByDescending(x => x.Product.Score)
                                .ThenBy(x => x.Product.Name)
                                .Select(x => x.Product))
                        .Take(Math.Max(8, _options.MaxProductResults))
                        .ToList(),

                    _ => BuildWallStructureSelection(classified, Math.Max(12, _options.MaxProductResults))
                };

                meta.Outcome = ProductSearchOutcome.Domain;
            }
            else
            {
                ranked = scores.Values
                    .OrderByDescending(p => p.Score)
                    .ThenBy(p => p.Name);
                meta.Outcome = ProductSearchOutcome.Generic;
            }

            var filtered = ranked.ToList();
            if (meta.WeightKg is > 0)
            {
                var byWeight = filtered.Where(p => MatchesWeightKg(p, meta.WeightKg.Value)).ToList();
                if (byWeight.Count > 0)
                {
                    filtered = byWeight;
                    meta.WeightApplied = true;
                }
                else
                {
                    meta.Outcome = ProductSearchOutcome.WeightNotFound;
                    meta.WeightApplied = true;
                    // Garder le contexte marque/type pour le message, sans polluer avec d'autres poids.
                    filtered = new List<ScoredProduct>();
                }
            }

            meta.TotalMatches = filtered.Count;
            // Marque : max 3. Mur : une famille à la fois, plus de choix dans le rayon.
            var take = brandMode
                ? Math.Max(1, Math.Min(3, _options.InitialProductResults > 0 ? _options.InitialProductResults : 3))
                : wallMode
                    ? meta.WallGuideFamily switch
                    {
                        WallGuideFamily.Binder => Math.Max(8, Math.Min(12, Math.Max(_options.MaxProductResults, 8))),
                        WallGuideFamily.Reinforcement => Math.Max(6, Math.Min(10, Math.Max(_options.MaxProductResults, 6))),
                        WallGuideFamily.Tools => Math.Max(5, Math.Min(8, Math.Max(_options.MaxProductResults, 5))),
                        _ => Math.Max(6, Math.Min(10, Math.Max(_options.MaxProductResults, 6)))
                    }
                    : Math.Max(1, Math.Min(_options.MaxProductResults, Math.Max(3, _options.InitialProductResults)));

            if (excludeProductIds is { Count: > 0 })
            {
                filtered = filtered
                    .Where(p => !excludeProductIds.Contains(p.Id.ToString(CultureInfo.InvariantCulture)))
                    .ToList();
                // Prendre un peu plus pour compenser les exclus.
                take = Math.Max(take, Math.Min(6, take + 3));
            }

            return filtered
                .Take(take)
                .Select(p =>
                {
                    var kind = ClassifyWallProduct(p);
                    return new StoreChatProductSuggestionDto
                    {
                        ProductId = p.Id.ToString(CultureInfo.InvariantCulture),
                        Name = FormatProductDisplayName(p.Name, p.Name2, p.Reference, p.Id),
                        Price = p.UnitPrice ?? p.PriceHT,
                        Brand = p.Brand,
                        Category = string.Join(" / ", new[] { p.MainTypeName, p.TypeName, p.SubTypeName }
                            .Where(x => !string.IsNullOrWhiteSpace(x))),
                        SuggestedQuantity = wallMode
                            ? EstimateQuantityForKind(kind, p.Name, p.Name2, session.WallAreaM2)
                            : 1,
                        ImageUrl = BuildProductImageUrl(p.PicName)
                    };
                })
                .ToList();
        }

        private async Task AccumulateSearchTermAsync(
            Dictionary<int, ScoredProduct> scores,
            string term,
            CancellationToken ct)
        {
            var t = term;
            var rows = await _storage.SelectAllErpProducts()
                .AsNoTracking()
                .Where(p =>
                    (p.Name != null && p.Name.ToLower().Contains(t))
                    || (p.Name2 != null && p.Name2.ToLower().Contains(t))
                    || (p.Reference != null && p.Reference.ToLower().Contains(t))
                    || (p.Brand != null && p.Brand.ToLower().Contains(t))
                    || (p.TypeName != null && p.TypeName.ToLower().Contains(t))
                    || (p.SubTypeName != null && p.SubTypeName.ToLower().Contains(t))
                    || (p.MainTypeName != null && p.MainTypeName.ToLower().Contains(t)))
                .Take(80)
                .Select(p => new ScoredProduct
                {
                    Id = p.Id,
                    Name = p.Name,
                    Name2 = p.Name2,
                    Reference = p.Reference,
                    Brand = p.Brand,
                    UnitPrice = p.UnitPrice,
                    PriceHT = p.PriceHT,
                    MainTypeName = p.MainTypeName,
                    TypeName = p.TypeName,
                    SubTypeName = p.SubTypeName,
                    PicName = p.PicName,
                    Score = 1
                })
                .ToListAsync(ct);

            foreach (var p in rows)
                AddOrBumpScore(scores, p, t);
        }

        private async Task AccumulateBrandSearchAsync(
            Dictionary<int, ScoredProduct> scores,
            string brand,
            CancellationToken ct)
        {
            var b = brand.Trim().ToLowerInvariant();
            if (b.Length < 2)
                return;

            var rows = await _storage.SelectAllErpProducts()
                .AsNoTracking()
                .Where(p =>
                    (p.Brand != null && p.Brand.ToLower().Contains(b))
                    || (p.Manufacturer != null && p.Manufacturer.ToLower().Contains(b))
                    || (p.TypeName != null && p.TypeName.ToLower().Contains(b))
                    || (p.SubTypeName != null && p.SubTypeName.ToLower().Contains(b))
                    || (p.MainTypeName != null && p.MainTypeName.ToLower().Contains(b))
                    || (p.Name != null && p.Name.ToLower().Contains(b))
                    || (p.Name2 != null && p.Name2.ToLower().Contains(b)))
                .Take(200)
                .Select(p => new ScoredProduct
                {
                    Id = p.Id,
                    Name = p.Name,
                    Name2 = p.Name2,
                    Reference = p.Reference,
                    Brand = p.Brand,
                    UnitPrice = p.UnitPrice,
                    PriceHT = p.PriceHT,
                    MainTypeName = p.MainTypeName,
                    TypeName = p.TypeName,
                    SubTypeName = p.SubTypeName,
                    PicName = p.PicName,
                    Score = 5
                })
                .ToListAsync(ct);

            foreach (var p in rows)
                AddOrBumpScore(scores, p, b, bonus: 8);
        }

        /// <summary>
        /// Requêtes ciblées mur. Ne jamais filtrer SubTypeName avec "%bouw%" seul
        /// (inbouw/opbouw saturent le Take et masquent Snelbouwstenen).
        /// </summary>
        private async Task EnrichWallCatalogCandidatesAsync(
            Dictionary<int, ScoredProduct> scores,
            CancellationToken ct)
        {
            var brickRows = await _storage.SelectAllErpProducts()
                .AsNoTracking()
                .Where(p =>
                    (p.SubTypeName != null && (
                        p.SubTypeName.ToLower().Contains("snelbouw")
                        || p.SubTypeName.ToLower().Contains("baksteen")
                        || p.SubTypeName.ToLower().Contains("metselsteen")))
                    || (p.Name != null && (
                        p.Name.ToLower().Contains("snelbouw")
                        || p.Name.ToLower().Contains("porotherm")
                        || p.Name.ToLower().Contains("thermobrick")
                        || p.Name.ToLower().Contains("baksteen")
                        || p.Name.ToLower().Contains("boerkes")
                        || p.Name.ToLower().Contains("kalkzandsteen")
                        || p.Name.ToLower().Contains("lijmblok"))))
                .Take(100)
                .Select(p => new ScoredProduct
                {
                    Id = p.Id,
                    Name = p.Name,
                    Name2 = p.Name2,
                    Reference = p.Reference,
                    Brand = p.Brand,
                    UnitPrice = p.UnitPrice,
                    PriceHT = p.PriceHT,
                    MainTypeName = p.MainTypeName,
                    TypeName = p.TypeName,
                    SubTypeName = p.SubTypeName,
                    PicName = p.PicName,
                    Score = 3
                })
                .ToListAsync(ct);

            foreach (var p in brickRows)
            {
                if (IsExcludedWallNoise(p))
                    continue;
                var kind = ClassifyWallProduct(p);
                if (kind == WallProductKind.Brick)
                    AddOrBumpScore(scores, p, "baksteen", bonus: 10);
                else if (kind == WallProductKind.Block)
                    AddOrBumpScore(scores, p, "blok", bonus: 5);
            }

            var cementRows = await _storage.SelectAllErpProducts()
                .AsNoTracking()
                .Where(p =>
                    (p.SubTypeName != null
                        && (p.SubTypeName.ToLower().Contains("cement")
                            || p.SubTypeName.ToLower().Contains("mortel")
                            || p.SubTypeName.ToLower().Contains("snelcement"))
                        && !p.SubTypeName.ToLower().Contains("gips")
                        && !p.SubTypeName.ToLower().Contains("flexcement"))
                    || (p.TypeName != null && p.TypeName.ToLower().Contains("cement en mortel"))
                    || (p.Name != null && (
                        p.Name.ToLower().Contains("cement cem")
                        || p.Name.ToLower().Contains("snelcement")
                        || p.Name.ToLower().StartsWith("cement ")
                        || p.Name.ToLower().Contains("betonmortel")
                        || p.Name.ToLower().Contains("metselmortel")
                        || p.Name.ToLower().Contains("mixcement")
                        || p.Name.ToLower().Contains("gietmortel"))))
                .Take(80)
                .Select(p => new ScoredProduct
                {
                    Id = p.Id,
                    Name = p.Name,
                    Name2 = p.Name2,
                    Reference = p.Reference,
                    Brand = p.Brand,
                    UnitPrice = p.UnitPrice,
                    PriceHT = p.PriceHT,
                    MainTypeName = p.MainTypeName,
                    TypeName = p.TypeName,
                    SubTypeName = p.SubTypeName,
                    PicName = p.PicName,
                    Score = 3
                })
                .ToListAsync(ct);

            foreach (var p in cementRows)
            {
                if (IsExcludedWallNoise(p))
                    continue;
                if (ClassifyWallProduct(p) == WallProductKind.Mortar)
                    AddOrBumpScore(scores, p, "cement", bonus: 8);
            }
        }

        private static bool IsExcludedWallNoise(ScoredProduct p)
        {
            var hay = $"{p.Name} {p.Name2} {p.TypeName} {p.SubTypeName}".ToLowerInvariant();
            return ContainsAny(hay,
                "wastafel", "gootsteen", "inbouwdoos", "inbouwdozen", "opbouwdoos", "inbouwnis",
                "opbouwnis", "sifon", "lavabo", "tegelprofiel", "tegel ", "tegels", "cementino",
                "flexcement", "vezelcement", "cirkelzaag", "steendrager", "steenbeitel",
                "waarborgpallet", "leddle", "hydro inbouw", "afvoer", "natuursteenstrip",
                "aanrechtblad", "kantband", "underc anti",
                "snelgips", "gips ", "plamuur", "290ml", "drystone", " 1 kg", "1kg");
        }

        private static void AddOrBumpScore(
            Dictionary<int, ScoredProduct> scores,
            ScoredProduct p,
            string term,
            int bonus = 0)
        {
            var inProductName =
                (!string.IsNullOrEmpty(p.Name) && p.Name.Contains(term, StringComparison.OrdinalIgnoreCase))
                || (!string.IsNullOrEmpty(p.Name2) && p.Name2.Contains(term, StringComparison.OrdinalIgnoreCase));
            var hitScore = (inProductName ? 2 : 1) + bonus;

            if (scores.TryGetValue(p.Id, out var existing))
            {
                existing.Score += hitScore;
                if (string.IsNullOrWhiteSpace(existing.PicName) && !string.IsNullOrWhiteSpace(p.PicName))
                    existing.PicName = p.PicName;
            }
            else
            {
                p.Score = hitScore;
                scores[p.Id] = p;
            }
        }

        private static int MortarPriority(ScoredProduct p)
        {
            var name = (p.Name ?? string.Empty).ToLowerInvariant();
            var type = (p.TypeName ?? string.Empty).ToLowerInvariant();
            var sub = (p.SubTypeName ?? string.Empty).ToLowerInvariant();

            // Préférer sacs 20–25 kg (pas les mini 5 kg → qté 84).
            var bagBonus = 0;
            if (ContainsAny(name, "25kg", "25 kg", "20kg", "20 kg"))
                bagBonus = 10;
            else if (ContainsAny(name, "05kg", "5kg", "5 kg", "1kg", "1 kg", "10kg", "10 kg"))
                bagBonus = -20;

            if (type.Contains("cement en mortel") || sub.Contains("cement papieren") || sub.Contains("cement coeck")
                || sub.StartsWith("cement ") || name.Contains("cement cem") || name.Contains("snelcement")
                || name.StartsWith("cement "))
                return 30 + bagBonus;
            if (ContainsAny(name, "metselmortel", "mortel") || ContainsAny($"{p.Name2}", "metselmortel"))
                return 20 + bagBonus;
            if (ContainsAny(name, "voegmortel", "filler"))
                return 5 + bagBonus;
            return 10 + bagBonus;
        }

        private static List<ScoredProduct> InterleaveWallKinds(
            IReadOnlyList<ScoredProduct> blocks,
            IReadOnlyList<ScoredProduct> bricks,
            IReadOnlyList<ScoredProduct> mortars)
        {
            var result = new List<ScoredProduct>();
            var max = Math.Max(blocks.Count, Math.Max(bricks.Count, mortars.Count));
            for (var i = 0; i < max; i++)
            {
                if (i < blocks.Count)
                    result.Add(blocks[i]);
                if (i < bricks.Count)
                    result.Add(bricks[i]);
                if (i < mortars.Count)
                    result.Add(mortars[i]);
            }

            return result;
        }

        /// <summary>Étape structure : briques + blocs seulement (pas de ciment mélangé).</summary>
        private static List<ScoredProduct> BuildWallStructureSelection(
            IReadOnlyList<(ScoredProduct Product, WallProductKind Kind)> classified,
            int max)
        {
            var brickSlots = Math.Min(6, Math.Max(4, max / 2));
            var blockSlots = Math.Max(4, max - brickSlots);

            var blocks = DeduplicateWallVariants(
                    classified
                        .Where(x => x.Kind == WallProductKind.Block)
                        .OrderByDescending(x => x.Product.Score)
                        .ThenBy(x => x.Product.Name)
                        .Select(x => x.Product))
                .Take(blockSlots)
                .ToList();

            var bricks = DeduplicateWallVariants(
                    classified
                        .Where(x => x.Kind == WallProductKind.Brick)
                        .OrderByDescending(x => x.Product.Score)
                        .ThenBy(x => x.Product.Name)
                        .Select(x => x.Product))
                .Take(brickSlots)
                .ToList();

            return InterleaveWallKinds(blocks, bricks, Array.Empty<ScoredProduct>());
        }

        private async Task EnrichWallReinforcementCandidatesAsync(
            Dictionary<int, ScoredProduct> scores,
            CancellationToken ct)
        {
            var rows = await _storage.SelectAllErpProducts()
                .AsNoTracking()
                .Where(p =>
                    (p.TypeName != null && (
                        p.TypeName.ToLower().Contains("zind")
                        || p.TypeName.ToLower().Contains("grid")
                        || p.TypeName.ToLower().Contains("wapening")
                        || p.TypeName.ToLower().Contains("ijzer")
                        || p.TypeName.ToLower().Contains("net")))
                    || (p.SubTypeName != null && (
                        p.SubTypeName.ToLower().Contains("zind")
                        || p.SubTypeName.ToLower().Contains("grid")
                        || p.SubTypeName.ToLower().Contains("wapening")
                        || p.SubTypeName.ToLower().Contains("bewapen")
                        || p.SubTypeName.ToLower().Contains("gaas")
                        || p.SubTypeName.ToLower().Contains("net")))
                    || (p.Name != null && (
                        p.Name.ToLower().Contains("wapeningsnet")
                        || p.Name.ToLower().Contains("bewapeningsnet")
                        || p.Name.ToLower().Contains("wapeningsgaas")
                        || p.Name.ToLower().Contains("treillis")
                        || p.Name.ToLower().Contains("betonijzer"))))
                .Take(120)
                .Select(p => new ScoredProduct
                {
                    Id = p.Id,
                    Name = p.Name,
                    Name2 = p.Name2,
                    Reference = p.Reference,
                    Brand = p.Brand,
                    UnitPrice = p.UnitPrice,
                    PriceHT = p.PriceHT,
                    MainTypeName = p.MainTypeName,
                    TypeName = p.TypeName,
                    SubTypeName = p.SubTypeName,
                    PicName = p.PicName,
                    Score = 4
                })
                .ToListAsync(ct);

            foreach (var p in rows)
            {
                if (ClassifyWallProduct(p) == WallProductKind.Mesh)
                    AddOrBumpScore(scores, p, "wapening", bonus: 12);
            }
        }

        private async Task EnrichWallToolCandidatesAsync(
            Dictionary<int, ScoredProduct> scores,
            CancellationToken ct)
        {
            var rows = await _storage.SelectAllErpProducts()
                .AsNoTracking()
                .Where(p =>
                    (p.Name != null && (
                        p.Name.ToLower().Contains("troffel")
                        || p.Name.ToLower().Contains("truelle")
                        || p.Name.ToLower().Contains("waterpas")
                        || p.Name.ToLower().Contains("mortelkuip")
                        || p.Name.ToLower().Contains("speciekuip")
                        || p.Name.ToLower().Contains("mengkuip")
                        || p.Name.ToLower().Contains("handschoen")
                        || p.Name.ToLower().Contains("metseltroffel")))
                    || (p.SubTypeName != null && (
                        p.SubTypeName.ToLower().Contains("troffel")
                        || p.SubTypeName.ToLower().Contains("handschoen"))))
                .Take(80)
                .Select(p => new ScoredProduct
                {
                    Id = p.Id,
                    Name = p.Name,
                    Name2 = p.Name2,
                    Reference = p.Reference,
                    Brand = p.Brand,
                    UnitPrice = p.UnitPrice,
                    PriceHT = p.PriceHT,
                    MainTypeName = p.MainTypeName,
                    TypeName = p.TypeName,
                    SubTypeName = p.SubTypeName,
                    PicName = p.PicName,
                    Score = 3
                })
                .ToListAsync(ct);

            foreach (var p in rows)
            {
                if (ClassifyWallProduct(p) == WallProductKind.Tool)
                    AddOrBumpScore(scores, p, "troffel", bonus: 10);
            }
        }

        /// <summary>
        /// Une seule variante par famille (ex. 3 Silka 100/150/198 mm → garde le meilleur score).
        /// </summary>
        private static IEnumerable<ScoredProduct> DeduplicateWallVariants(IEnumerable<ScoredProduct> products)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in products)
            {
                var key = WallVariantFamilyKey(p);
                if (!seen.Add(key))
                    continue;
                yield return p;
            }
        }

        private static string WallVariantFamilyKey(ScoredProduct p)
        {
            var raw = $"{p.Brand} {p.Name}".ToLowerInvariant();
            // Retire dimensions / formats fréquents pour regrouper les variants.
            raw = Regex.Replace(raw, @"\d+\s*[x×*]\s*\d+(?:\s*[x×*]\s*\d+)?\s*(?:mm|cm|m)?", " ");
            raw = Regex.Replace(raw, @"\b\d+\s*(?:mm|cm)\b", " ");
            raw = Regex.Replace(raw, @"\s+", " ").Trim();
            if (raw.Length < 8)
                raw = $"{p.Brand}|{p.Id}";
            return raw;
        }

        internal enum WallProductKind
        {
            Exclude,
            Block,
            Brick,
            Mortar,
            Mesh,
            Tool,
            Other
        }

        /// <summary>
        /// Blocs/briques : type lu surtout dans Name.
        /// Mortier/ciment : Name OU Name2 (souvent le type métier est dans Name2).
        /// </summary>
        private static WallProductKind ClassifyWallProduct(ScoredProduct p)
        {
            var name = (p.Name ?? string.Empty).ToLowerInvariant();
            var name2 = (p.Name2 ?? string.Empty).ToLowerInvariant();
            var category = $"{p.MainTypeName} {p.TypeName} {p.SubTypeName}".ToLowerInvariant();

            // Outillage maçonnerie (truelle…) avant les lames/perceuses exclues du parcours.
            if (IsMasonryHandTool(name, category))
                return WallProductKind.Tool;

            if (IsCuttingOrPowerToolNoise(p))
                return WallProductKind.Exclude;

            // Treillis / ferraillage (Zind & Grid, Net IJzer…).
            if (IsMeshProduct(name, name2, category))
                return WallProductKind.Mesh;

            // Finitions / colles / étanchéité : hors structure mur.
            if (ContainsAny(name, "epoxy", "plamuur", "polyfilla", "varioflex", "tegellijm", "primer",
                    "coating", "kelder", "afwerkplamuur", "kit ", " silicon"))
                return WallProductKind.Exclude;
            if (ContainsAny(name, "verf", "paint", "latex") && !ContainsAny(name, "baksteen", "lijmblok"))
                return WallProductKind.Exclude;

            // Mortier / ciment d'abord (Name2 / catégorie "Cement en Mortels").
            if (IsMortarOrCementProduct(name, name2, category))
                return WallProductKind.Mortar;

            // Blocs structurels — intitulés produit, pas "… baksteen / cellenbeton" sur une lame.
            if (ContainsAny(name, "lijmblok", "kalkzandsteen", "snelbouwblok", "parpaing",
                    "hourdis", "ytong", "concrete block", "cinder block"))
                return WallProductKind.Block;

            if (ContainsAny(name, "betonblok", "cellenblok", "cellenbeton", "cementblok")
                && !ContainsAny(name, "zaag", "blad", "plug", "boor", "boren", "snijden", "recipro", "spanner"))
                return WallProductKind.Block;

            if (category.Contains("stenen")
                && ContainsAny(name, "blok")
                && !ContainsAny(name, "boor", "zaag", "blad", "plug", "voor ", "snijden")
                && !category.Contains("snelbouwsteen"))
                return WallProductKind.Block;

            // Briques — Name, Name2 ("holle baksteen") ou sous-type Snelbouwstenen.
            if (IsBrickProduct(name, name2, category))
                return WallProductKind.Brick;

            return WallProductKind.Other;
        }

        private static bool IsMeshProduct(string name, string name2, string category)
        {
            if (ContainsAny(name, "gipsplaat", "gipsplaten") || ContainsAny(category, "gipsplaat", "tape & banden"))
                return false;

            if (ContainsAny(name, "murfor", "wapeningsnet", "bewapeningsnet", "wapeningsgaas", "treillis",
                    "betonijzer", "betonnet", "wapeningsdraad", "draadgaas", "lintvoeg"))
                return true;
            if (ContainsAny(name2, "wapening", "bewapen", "treillis", "murfor"))
                return true;
            if (ContainsAny(category, "zind", "grid", "murfor", "betonnet", "betonijzer")
                && !ContainsAny(name, "verf", "kit ", "silicon"))
                return true;
            if (ContainsAny(category, "ijzer", "net, ijzer", "net ijzer")
                && ContainsAny(name, "net", "gaas", "draad", "wapen", "ijzer", "mesh", "murfor"))
                return true;
            return false;
        }

        private static bool IsMasonryHandTool(string name, string category)
        {
            if (ContainsAny(name,
                    "troffel", "truelle", "metseltroffel", "waterpas", "niveau",
                    "mortelkuip", "speciekuip", "mengkuip", "auge",
                    "handschoen", "werkhandschoen"))
                return true;
            if (ContainsAny(category, "troffel", "handschoen") && !ContainsAny(name, "zaag", "boor"))
                return true;
            return false;
        }

        private static bool IsCuttingOrPowerToolNoise(ScoredProduct p) => IsToolProduct(p);

        private static bool IsBrickProduct(string name, string name2, string category)
        {
            if (ContainsAny(name, "boor", "zaag", "blad", "plug", "fixations", "epoxy", "verf", "waarborgpallet",
                    "wastafel", "gootsteen", "steendrager", "steenbeitel"))
                return false;
            if (ContainsAny(category, "wastafel", "gootsteen", "inbouwdoos", "opbouwdoos", "inbouwnis", "sifon"))
                return false;

            if (ContainsAny(name, "snelbouwsteen", "snelbouw ", "metselsteen", "brique", "briquetage", "baksteen",
                    "thermobrick", "porotherm", "boerkes")
                || (name.Contains("brick") && !ContainsAny(name, "drill", "saw")))
                return true;

            // Sous-types prod (requête GROUP BY SubTypeName … %steen%/%bouw%).
            if (category.Contains("snelbouw") || category.Contains("baksteen") || category.Contains("metselsteen"))
                return true;
            if (category.Contains("steen") && !ContainsAny(category, "opbouw", "inbouw", "goot", "wastafel", "strip")
                && !ContainsAny(name, "profiel", "tegel", "werkblad"))
                return true;

            if (ContainsAny(name2, "holle baksteen", "volle baksteen", "metselbaksteen", "klassieke holle baksteen"))
                return true;

            if (name2.Contains("baksteen")
                && ContainsAny(name2, "waarmee je", "binnen als buiten", "holle ", "volle ", "klassieke")
                && !ContainsAny(name2, "ideaal voor gebruik in", "geschikt voor gebruik", "coating", "epoxy", "fixations"))
                return true;

            return false;
        }

        private static bool IsMortarOrCementProduct(string name, string name2, string category)
        {
            var hay = $"{name} {name2} {category}";

            if (ContainsAny(name, "epoxy", "plamuur", "polyfilla", "varioflex", "tegellijm", "primer",
                    "coating", "kelder", "cementino", "flexcement", "vezelcement", "cirkelzaag", "tegel"))
                return false;
            if (ContainsAny(hay, "cementblok", "cementering", "cementdekvloer", "cementgrijs - 290ml")
                && !ContainsAny(hay, "mortel", "mortier", "voegmortel", "metselmortel", "cement cem", "snelcement",
                    "betonmortel", "mixcement", "gietmortel"))
                return false;

            // Sous-types prod : Cement & Gips, Betonmortel, Rapolith Snelcement, Mixcement…
            if (ContainsAny(name, "snelgips", "gips"))
                return false;
            if (ContainsAny(name, "ml") && !ContainsAny(name, "kg"))
                return false;

            if (ContainsAny(category, "cement en mortel", "cement papieren", "cement coeck",
                    "cement wit", "rapolith snelcement", "betonmortel", "gietmortel", "mixcement", "snelcement"))
                return true;
            // "Cement & Gips" : garder ciment, exclure gips (déjà filtré sur Name).
            if (category.Contains("cement & gips") || (category.Contains("cement") && !category.Contains("cementino")))
                return !ContainsAny(name, "gips");
            if (category.Contains("mortel"))
                return true;

            if (ContainsAny(hay, "metselmortel", "voegmortel", "lijmmortel", "metserijmortel",
                    "betonmortel", "gietmortel", "mixcement", "mortier", "mortel", "mortar", "snelcement",
                    "portlandcement", "cement cem"))
                return true;

            if ((name.StartsWith("cement ") || ContainsAny(name, "ciment "))
                && ContainsAny(hay, "kg", "zak", "sac", "bag", "pallet")
                && !ContainsAny(name, "cementino", "sand "))
                return true;

            if (ContainsAny(name2, "portlandcement", "portlandklinker")
                && ContainsAny(hay, "kg", "cement")
                && !ContainsAny(name, "tegel", "epoxy"))
                return true;

            if (ContainsAny(name, "filler") && ContainsAny(name2, "voegmortel", "mortel", "metselmortel"))
                return true;

            return false;
        }

        private static string FormatProductDisplayName(string? name, string? name2, string? reference, int id)
        {
            var n1 = name?.Trim();
            var n2 = name2?.Trim();
            // Name2 long = fiche descriptive : on l’utilise pour la recherche, pas pour polluer l’UI.
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

        private static bool IsToolProduct(ScoredProduct p)
        {
            var name = (p.Name ?? string.Empty).ToLowerInvariant();
            var category = $"{p.MainTypeName} {p.TypeName} {p.SubTypeName}".ToLowerInvariant();

            // Contient (pas \b) : "reciprozaagbladen" est un seul mot.
            if (ContainsAny(name,
                    "zaagblad", "zaagbladen", "reciprozaag", "recipro", "cellenbetonzaag", "betonzaag",
                    "lijnspanner", "carbide", "drill bit", "cellenbetonplug", "betonplug",
                    "snijden", "boor voor", "set boren", "gereedschap"))
                return true;

            if (ContainsAny(name, "zaag", "boor", "boren", "plug", "blad")
                && ContainsAny(name, "baksteen", "cellenbeton", "cementblok", "betonblok", "beton"))
                return true;

            if (category.Contains("gereedschap") || category.Contains("lames") || category.Contains("tools"))
                return true;

            if (ToolMarkers.Any(m => name.Contains(m))
                && !ContainsAny(name, "lijmblok", "kalkzandsteen", "snelbouwsteen", "mortel", "cement "))
                return true;

            return false;
        }

        private static string ProductHaystack(ScoredProduct p) =>
            $"{p.Name} {p.Name2} {p.Brand} {p.MainTypeName} {p.TypeName} {p.SubTypeName}".ToLowerInvariant();

        private static int MasonryBoost(ScoredProduct p)
        {
            var kind = ClassifyWallProduct(p);
            var name = (p.Name ?? string.Empty).ToLowerInvariant();
            var boost = kind switch
            {
                WallProductKind.Block => 10,
                WallProductKind.Brick => 10,
                WallProductKind.Mortar => 6,
                WallProductKind.Mesh => 8,
                WallProductKind.Tool => 5,
                _ => 0
            };
            if (name.Contains("lijmblok") || name.Contains("kalkzandsteen") || name.Contains("snelbouwsteen"))
                boost += 4;
            if (ContainsAny($"{p.Name2}", "metselmortel", "voegmortel", "lijmmortel", "portlandcement"))
                boost += 5;
            if ((p.MainTypeName ?? "").Contains("Stenen", StringComparison.OrdinalIgnoreCase)
                || (p.TypeName ?? "").Contains("Stenen", StringComparison.OrdinalIgnoreCase))
                boost += 3;
            return boost;
        }

        internal sealed class ScoredProduct
        {
            public int Id { get; set; }
            public string? Name { get; set; }
            public string? Name2 { get; set; }
            public string? Reference { get; set; }
            public string? Brand { get; set; }
            public decimal? UnitPrice { get; set; }
            public decimal? PriceHT { get; set; }
            public string? MainTypeName { get; set; }
            public string? TypeName { get; set; }
            public string? SubTypeName { get; set; }
            public string? PicName { get; set; }
            public int Score { get; set; }
        }

        private string? BuildProductImageUrl(string? picName)
        {
            if (string.IsNullOrWhiteSpace(picName))
                return null;

            var file = picName.Trim().Replace('\\', '/');
            if (file.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || file.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return file;
            }

            var baseUrl = (_erpOptions.ImageBaseUrl ?? string.Empty).TrimEnd('/');
            if (string.IsNullOrWhiteSpace(baseUrl))
                return null;

            while (file.StartsWith('/'))
                file = file[1..];

            return $"{baseUrl}/{file}";
        }

        private static List<string> BuildSearchTerms(string text, StoreChatSession session, bool brandMode)
        {
            var terms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var lighting = IsLightingQuery(text);

            if (!brandMode)
            {
                foreach (var hint in session.MaterialHints)
                {
                    // Requête éclairage : ignorer les hints maçonnerie sticky (brique/ciment…).
                    if (lighting && IsMasonrySearchHint(hint))
                        continue;
                    AddTermWithSynonyms(terms, hint);
                }

                if (!string.IsNullOrWhiteSpace(session.ActiveProjectDomainId)
                    && DomainSearchTerms.TryGetValue(session.ActiveProjectDomainId, out var domainTerms))
                {
                    foreach (var t in domainTerms)
                    {
                        // Requête ampoules : ne pas injecter câble/draad (→ dénudeurs).
                        if (lighting && t is "câble" or "cable" or "draad" or "wire")
                            continue;
                        terms.Add(t);
                    }
                }

                if (lighting)
                {
                    foreach (var t in new[]
                             {
                                 "ampoule", "ampoules", "lampe", "lampes", "lamp", "bulb",
                                 "gloeilamp", "spaarlamp", "lampje", "led"
                             })
                        terms.Add(t);
                }
            }
            else if (!string.IsNullOrWhiteSpace(session.PreferredBrand))
            {
                terms.Add(session.PreferredBrand);
            }

            foreach (var raw in text.Split(new[] { ' ', ',', ';', '.', '!', '?', '/', '\\', '\n', '\t', '\'', '’' },
                         StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var token = raw.Trim().ToLowerInvariant();
                if (token.Length < 3 || SalesMaterialLexicon.StopWords.Contains(token) || token.Any(char.IsDigit))
                    continue;
                if (token is "bloc" or "block" or "blocks")
                    continue;
                AddTermWithSynonyms(terms, token);
            }

            terms.Remove("bloc");
            terms.Remove("block");
            terms.Remove("blocks");

            return terms.Take(24).ToList();
        }

        private static bool IsLightingQuery(string text)
        {
            var lower = (text ?? string.Empty).ToLowerInvariant();
            return ContainsIgnoreCase(lower, "ampoule")
                   || ContainsIgnoreCase(lower, "lampe")
                   || ContainsIgnoreCase(lower, "lampes")
                   || ContainsIgnoreCase(lower, "bulb")
                   || ContainsIgnoreCase(lower, "gloeilamp")
                   || ContainsIgnoreCase(lower, "spaarlamp")
                   || ContainsIgnoreCase(lower, "lampje")
                   || ContainsIgnoreCase(lower, "e27")
                   || ContainsIgnoreCase(lower, "e14")
                   || ContainsIgnoreCase(lower, "gu10")
                   || ContainsIgnoreCase(lower, "halogène")
                   || ContainsIgnoreCase(lower, "halogene");
        }

        private static bool IsMasonrySearchHint(string hint) =>
            ContainsIgnoreCase(hint, "parpaing")
            || ContainsIgnoreCase(hint, "brique")
            || ContainsIgnoreCase(hint, "mortier")
            || ContainsIgnoreCase(hint, "ciment")
            || ContainsIgnoreCase(hint, "cement")
            || ContainsIgnoreCase(hint, "baksteen")
            || ContainsIgnoreCase(hint, "steen")
            || ContainsIgnoreCase(hint, "blok")
            || ContainsIgnoreCase(hint, "silka")
            || ContainsIgnoreCase(hint, "porotherm")
            || ContainsIgnoreCase(hint, "snelbouw");

        private static void AddTermWithSynonyms(HashSet<string> terms, string token)
        {
            terms.Add(token);
            foreach (var kv in SalesMaterialLexicon.MaterialSynonyms)
            {
                if (token.Contains(kv.Key, StringComparison.OrdinalIgnoreCase)
                    || kv.Value.Any(s => token.Contains(s, StringComparison.OrdinalIgnoreCase)))
                {
                    foreach (var s in kv.Value)
                        terms.Add(s);
                }
            }
        }
        private static decimal EstimateQuantityForKind(
            WallProductKind kind,
            string? name,
            string? name2,
            decimal? areaM2)
        {
            if (areaM2 is null or <= 0)
                return 1;

            var area = areaM2.Value;
            return kind switch
            {
                WallProductKind.Block => Math.Max(1, Math.Ceiling(area * ParpaingsPerM2)),
                WallProductKind.Brick => Math.Max(1, Math.Ceiling(area * BricksPerM2)),
                WallProductKind.Mortar => EstimateMortarBags(name, name2, area),
                _ => 1
            };
        }

        private static decimal EstimateMortarBags(string? name, string? name2, decimal area)
        {
            var hay = $"{name} {name2}".ToLowerInvariant();
            var bagKg = TryParseBagKg(hay) ?? DefaultBagKg;
            return Math.Max(1, Math.Ceiling(area * MortarKgPerM2 / bagKg));
        }

        private static decimal? TryParseBagKg(string hay)
        {
            var m = Regex.Match(hay, @"(\d+(?:[.,]\d+)?)\s*kg");
            if (m.Success
                && decimal.TryParse(m.Groups[1].Value.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var kg)
                && kg > 0)
                return kg;
            return null;
        }

        private static bool ContainsAny(string hay, params string[] keys) =>
            keys.Any(k => hay.Contains(k, StringComparison.OrdinalIgnoreCase));
        private static bool MatchesWeightKg(ScoredProduct p, decimal kg)
        {
            var hay = $"{p.Name} {p.Name2}";
            var parsed = TryParseBagKg(hay);
            if (parsed.HasValue)
                return Math.Abs(parsed.Value - kg) < 0.05m;

            // Formats fréquents sans espace : 25kg, 25KG
            var token = kg == Math.Truncate(kg)
                ? ((int)kg).ToString(CultureInfo.InvariantCulture)
                : kg.ToString("0.##", CultureInfo.InvariantCulture);
            return Regex.IsMatch(hay, $@"\b{Regex.Escape(token)}\s*kg\b", RegexOptions.IgnoreCase)
                   || hay.Contains(token + "kg", StringComparison.OrdinalIgnoreCase)
                   || hay.Contains(token + " kg", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsCementProduct(ScoredProduct p)
        {
            var hay = $"{p.Name} {p.Name2} {p.TypeName} {p.SubTypeName}".ToLowerInvariant();
            return ContainsAny(hay, "cement", "ciment", "portlandcement")
                   && !ContainsAny(hay, "tegellijm", "voegmortel", "voegen", "lijm ", "blokkenlijm");
        }

        private static int TypeExactBoost(ScoredProduct p, IReadOnlyList<string> typeHints)
        {
            if (typeHints.Any(t => t.Equals("ciment", StringComparison.OrdinalIgnoreCase)) && IsCementProduct(p))
                return 30;
            return MatchesTypeHints(p, typeHints) ? 5 : 0;
        }

        private static bool MatchesBrand(ScoredProduct p, string brand)
        {
            var b = brand.Trim();
            if (b.Length == 0)
                return false;

            return ContainsIgnoreCase(p.Brand, b)
                   || ContainsIgnoreCase(p.Name, b)
                   || ContainsIgnoreCase(p.Name2, b)
                   || ContainsIgnoreCase(p.TypeName, b)
                   || ContainsIgnoreCase(p.SubTypeName, b)
                   || ContainsIgnoreCase(p.MainTypeName, b);
        }

        private static int BrandMatchBoost(ScoredProduct p, string brand)
        {
            if (ContainsIgnoreCase(p.Brand, brand))
                return 20;
            if (ContainsIgnoreCase(p.TypeName, brand) || ContainsIgnoreCase(p.SubTypeName, brand))
                return 12;
            if (ContainsIgnoreCase(p.Name, brand))
                return 8;
            return 0;
        }

        private static bool MatchesTypeHints(ScoredProduct p, IReadOnlyList<string> typeHints)
        {
            if (typeHints.Count == 0)
                return true;

            var hay = $"{p.Name} {p.Name2} {p.Brand} {p.MainTypeName} {p.TypeName} {p.SubTypeName}"
                .ToLowerInvariant();

            foreach (var hint in typeHints)
            {
                var keys = SalesMaterialLexicon.ExpandTypeHintTerms(hint).Select(x => x.ToLowerInvariant()).ToList();
                if (keys.Any(k => hay.Contains(k, StringComparison.OrdinalIgnoreCase)))
                    return true;
            }

            return false;
        }

        private static bool ContainsIgnoreCase(string? hay, string needle) =>
            !string.IsNullOrWhiteSpace(hay)
            && hay.Contains(needle, StringComparison.OrdinalIgnoreCase);
        private static bool IsGardenDomain(string? domainId) =>
            domainId is "garden_cleaning" or "garden_landscaping" or "garden_maintenance";

        /// <summary>
        /// Évite de proposer des tondeuses pour « nettoyer / aménager » le jardin.
        /// </summary>
        private static void ApplyGardenIntentFilter(Dictionary<int, ScoredProduct> scores, string? domainId)
        {
            if (domainId is not ("garden_cleaning" or "garden_landscaping"))
                return;

            var mowerMarkers = new[] { "tondeuse", "grasmaaier", "lawnmower", "maaier" };
            var cleanBoost = new[] { "souffleur", "bladblazer", "rateau", "râteau", "balai", "nettoyeur", "hogedruk", "blower", "afval" };
            var landscapeBoost = new[] { "terrasse", "dalle", "bordure", "cloture", "clôture", "schutting", "tegel", "grind", "pot", "plante" };

            foreach (var p in scores.Values.ToList())
            {
                var hay = $"{p.Name} {p.Name2} {p.TypeName} {p.SubTypeName}".ToLowerInvariant();
                if (mowerMarkers.Any(m => hay.Contains(m, StringComparison.OrdinalIgnoreCase)))
                {
                    scores.Remove(p.Id);
                    continue;
                }

                if (domainId == "garden_cleaning" && cleanBoost.Any(m => hay.Contains(m, StringComparison.OrdinalIgnoreCase)))
                    p.Score += 12;
                if (domainId == "garden_landscaping" && landscapeBoost.Any(m => hay.Contains(m, StringComparison.OrdinalIgnoreCase)))
                    p.Score += 12;
            }
        }

        /// <summary>« ampoules » ne doit pas remonter des Wire Stripper via le domaine Électricité.</summary>
        private static void ApplyLightingIntentFilter(Dictionary<int, ScoredProduct> scores, string text)
        {
            if (!IsLightingQuery(text) || scores.Count == 0)
                return;

            var noise = new[]
            {
                "wire strip", "stripper", "dénudeur", "denudeur", "dégainer", "degainer",
                "outil à dégainer", "outil a degainer"
            };
            var boost = new[]
            {
                "ampoule", "lampe", "lamp", "bulb", "gloeilamp", "spaarlamp", "lampje",
                "e27", "e14", "gu10", "led"
            };

            foreach (var p in scores.Values.ToList())
            {
                var hay = $"{p.Name} {p.Name2} {p.MainTypeName} {p.TypeName} {p.SubTypeName}".ToLowerInvariant();
                if (noise.Any(n => hay.Contains(n, StringComparison.OrdinalIgnoreCase)))
                {
                    scores.Remove(p.Id);
                    continue;
                }

                if (boost.Any(b => hay.Contains(b, StringComparison.OrdinalIgnoreCase)))
                    p.Score += 20;
            }
        }

        /// <summary>Pénalise les soldes / Winkel (oud) quand un rayon métier existe.</summary>
        private static void DemoteClearanceNoise(Dictionary<int, ScoredProduct> scores, string? domainId)
        {
            if (scores.Count == 0)
                return;
            if (domainId is not ("electrical" or "plumbing" or "painting" or "tiling"
                or "garden_cleaning" or "garden_landscaping" or "garden_maintenance"
                or "wall_construction"))
                return;

            foreach (var p in scores.Values)
            {
                var hay = $"{p.MainTypeName} {p.TypeName} {p.SubTypeName} {p.Name}".ToLowerInvariant();
                if (hay.Contains("winkel (oud)", StringComparison.OrdinalIgnoreCase)
                    || hay.Contains("uitverkoop", StringComparison.OrdinalIgnoreCase))
                    p.Score -= 15;

                if (domainId == "electrical"
                    && (hay.Contains("elektriciteit", StringComparison.OrdinalIgnoreCase)
                        || hay.Contains("eko lampen", StringComparison.OrdinalIgnoreCase)
                        || hay.Contains("lamp", StringComparison.OrdinalIgnoreCase)))
                    p.Score += 8;
            }
        }    }
}
