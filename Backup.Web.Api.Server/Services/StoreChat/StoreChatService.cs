using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Backup.Web.Api.Server.Services.StoreChat
{
    public interface IStoreChatService
    {
        Task<StoreChatResponseDto> ProcessMessageAsync(StoreChatMessageRequest request, CancellationToken ct = default);
        Task<StoreChatPaymentResultDto?> GetPaymentResultAsync(Guid orderId, CancellationToken ct = default);
        Task<StoreChatPaymentResultDto?> ConfirmPaymentAsync(Guid orderId, string? stripeSessionId, CancellationToken ct = default);
    }

    public class StoreChatService : IStoreChatService
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private readonly IStorageBroker _storage;
        private readonly IStoreChatSessionStore _sessions;
        private readonly IStoreChatAiClient _ai;
        private readonly IStoreChatPdfService _pdf;
        private readonly IStoreChatStripeService _stripe;
        private readonly StoreChatOptions _options;
        private readonly ILogger<StoreChatService> _logger;

        public StoreChatService(
            IStorageBroker storage,
            IStoreChatSessionStore sessions,
            IStoreChatAiClient ai,
            IStoreChatPdfService pdf,
            IStoreChatStripeService stripe,
            IOptions<StoreChatOptions> options,
            ILogger<StoreChatService> logger)
        {
            _storage = storage;
            _sessions = sessions;
            _ai = ai;
            _pdf = pdf;
            _stripe = stripe;
            _options = options.Value ?? new StoreChatOptions();
            _logger = logger;
        }

        public async Task<StoreChatResponseDto> ProcessMessageAsync(StoreChatMessageRequest request, CancellationToken ct = default)
        {
            var session = _sessions.GetOrCreate(request.SessionId);
            var intent = (request.ClientIntent ?? string.Empty).Trim();

            if (intent.Equals("NewProject", StringComparison.OrdinalIgnoreCase))
            {
                _sessions.Reset(session.SessionId);
                session = _sessions.GetOrCreate(session.SessionId);
                return Ok(session, "Nouveau projet démarré. Comment puis-je vous aider ?", "NONE");
            }

            if (intent.Equals("AddToCartFromList", StringComparison.OrdinalIgnoreCase))
            {
                await AddToCartAsync(session, request.TargetProductId, request.TargetQuantity ?? 1, ct);
                _sessions.Save(session);
                return Ok(session, "Produit ajouté au panier.", "CART_UPDATED");
            }

            if (intent.Equals("RemoveFromCartFromList", StringComparison.OrdinalIgnoreCase))
            {
                RemoveFromCart(session, request.TargetProductId);
                _sessions.Save(session);
                return Ok(session, "Produit retiré du panier.", "CART_UPDATED");
            }

            if (intent.Equals("CreateQuoteFromTableSelection", StringComparison.OrdinalIgnoreCase))
            {
                await ReplaceCartFromTableAsync(session, request.TableCartLines, ct);
                return await CreateQuoteAsync(session, ct);
            }

            if (intent.Equals("CreateOrderFromTableSelection", StringComparison.OrdinalIgnoreCase))
            {
                await ReplaceCartFromTableAsync(session, request.TableCartLines, ct);
                return await CreateOrderAsync(session, ct);
            }

            var text = (request.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(text))
                return Ok(session, "Message vide.", "NONE");

            DetectDomain(session, text);
            ParseWallDimensions(session, text);
            CollectMaterialHints(session, text);
            var products = await SearchProductsAsync(text, session, ct);
            ApplySuggestedQuantities(products, session);
            var catalogContext = BuildCatalogContext(products);
            var aiReply = await _ai.CompleteAsync(session.History, text, catalogContext, ct);

            session.History.Add(new StoreChatHistoryMessage { Role = "user", Content = text });
            var reply = BuildUserFacingReply(aiReply, products, session);

            session.History.Add(new StoreChatHistoryMessage { Role = "assistant", Content = reply });
            TrimHistory(session);
            _sessions.Save(session);

            if (products.Count > 0)
            {
                return new StoreChatResponseDto
                {
                    SessionId = session.SessionId,
                    ReplyText = reply,
                    HasAction = true,
                    ActionType = "PRODUCT_LIST",
                    ActionData = products,
                    Products = products,
                    ActiveProjectDomainId = session.ActiveProjectDomainId,
                    ActiveProjectDomainLabel = session.ActiveProjectDomainLabel
                };
            }

            return Ok(session, reply, "NONE");
        }

        public async Task<StoreChatPaymentResultDto?> GetPaymentResultAsync(Guid orderId, CancellationToken ct = default)
        {
            var order = await _storage.SelectAllStoreChatOrders()
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.Id == orderId, ct);
            if (order == null)
                return null;

            return ToPaymentResult(order);
        }

        public async Task<StoreChatPaymentResultDto?> ConfirmPaymentAsync(
            Guid orderId,
            string? stripeSessionId,
            CancellationToken ct = default)
        {
            var order = await _storage.SelectAllStoreChatOrders()
                .FirstOrDefaultAsync(o => o.Id == orderId, ct);
            if (order == null)
                return null;

            if (string.Equals(order.Status, "paid", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(order.InvoicePdfBase64))
            {
                return ToPaymentResult(order);
            }

            var status = "paid";
            if (_stripe.IsEnabled)
            {
                var sid = stripeSessionId ?? order.StripeSessionId;
                if (!string.IsNullOrWhiteSpace(sid))
                {
                    var stripeStatus = await _stripe.GetCheckoutSessionStatusAsync(sid, ct);
                    if (!string.Equals(stripeStatus, "paid", StringComparison.OrdinalIgnoreCase))
                    {
                        order.Status = "pending";
                        await _storage.UpdateStoreChatOrderAsync(order);
                        return ToPaymentResult(order);
                    }
                }
                else if (!string.IsNullOrWhiteSpace(stripeSessionId))
                {
                    order.StripeSessionId = stripeSessionId;
                }
            }

            var items = DeserializeCart(order.LinesJson);
            var invoiceNumber = order.InvoiceNumber
                ?? $"FAC-{DateTime.UtcNow:yyyyMMdd}-{order.Id.ToString("N")[..6].ToUpperInvariant()}";
            var invoice = _pdf.GenerateInvoice(items, invoiceNumber, "Client magasin");

            order.Status = status;
            order.PaidAt = DateTime.UtcNow;
            order.InvoiceNumber = invoiceNumber;
            order.InvoicePdfBase64 = invoice.PdfBase64;
            order.InvoiceFileName = invoice.FileName;
            if (!string.IsNullOrWhiteSpace(stripeSessionId))
                order.StripeSessionId = stripeSessionId;

            await _storage.UpdateStoreChatOrderAsync(order);
            return ToPaymentResult(order);
        }

        private async Task<StoreChatResponseDto> CreateQuoteAsync(StoreChatSession session, CancellationToken ct)
        {
            if (session.Cart.Count == 0)
                return Ok(session, "Panier vide. Ajoutez des produits avant de demander un devis.", "NONE");

            var pdf = _pdf.GenerateQuote(session.Cart, "Client magasin");
            var quote = new StoreChatQuote
            {
                SessionId = session.SessionId,
                Number = $"DEV-{DateTime.UtcNow:yyyyMMddHHmmss}",
                TotalAmount = pdf.Total ?? 0,
                PdfBase64 = pdf.PdfBase64,
                FileName = pdf.FileName,
                LinesJson = JsonSerializer.Serialize(session.Cart, JsonOptions)
            };
            await _storage.InsertStoreChatQuoteAsync(quote);
            _sessions.Save(session);

            return new StoreChatResponseDto
            {
                SessionId = session.SessionId,
                ReplyText = $"Devis prêt ({pdf.Total:N2} €). Vous pouvez le télécharger.",
                HasAction = true,
                ActionType = "QUOTE_PDF",
                QuotePdf = pdf,
                ActiveProjectDomainId = session.ActiveProjectDomainId,
                ActiveProjectDomainLabel = session.ActiveProjectDomainLabel
            };
        }

        private async Task<StoreChatResponseDto> CreateOrderAsync(StoreChatSession session, CancellationToken ct)
        {
            if (session.Cart.Count == 0)
                return Ok(session, "Panier vide. Ajoutez des produits avant de commander.", "NONE");

            var order = new StoreChatOrder
            {
                SessionId = session.SessionId,
                Status = "pending",
                TotalAmount = session.Cart.Sum(c => c.TotalPrice),
                LinesJson = JsonSerializer.Serialize(session.Cart, JsonOptions)
            };
            await _storage.InsertStoreChatOrderAsync(order);
            session.LastOrderId = order.Id;

            if (_stripe.IsEnabled)
            {
                var link = await _stripe.CreateCheckoutAsync(order.Id, session.Cart, session.SessionId, ct);
                if (link != null)
                {
                    order.StripeSessionId = link.Source; // Stripe Checkout Session Id
                    await _storage.UpdateStoreChatOrderAsync(order);
                    _sessions.Save(session);

                    return new StoreChatResponseDto
                    {
                        SessionId = session.SessionId,
                        ReplyText = $"Commande créée ({order.TotalAmount:N2} €). Payez par carte pour finaliser.",
                        HasAction = true,
                        ActionType = "PAYMENT_LINK",
                        PaymentLink = new StoreChatPaymentLinkDto
                        {
                            Url = link.Url,
                            Amount = link.Amount,
                            Description = link.Description,
                            OrderId = link.OrderId,
                            Source = "stripe",
                            SourceLabel = link.SourceLabel
                        },
                        ActiveProjectDomainId = session.ActiveProjectDomainId,
                        ActiveProjectDomainLabel = session.ActiveProjectDomainLabel
                    };
                }
            }

            // Mode démo sans Stripe : confirme directement et génère facture.
            var confirmed = await ConfirmPaymentAsync(order.Id, null, ct);
            _sessions.Save(session);
            return new StoreChatResponseDto
            {
                SessionId = session.SessionId,
                ReplyText = "Commande enregistrée (mode démo sans Stripe). Facture disponible.",
                HasAction = true,
                ActionType = "INVOICE_PDF",
                QuotePdf = confirmed?.InvoicePdf,
                ActiveProjectDomainId = session.ActiveProjectDomainId,
                ActiveProjectDomainLabel = session.ActiveProjectDomainLabel
            };
        }

        private async Task AddToCartAsync(StoreChatSession session, string? productId, decimal qty, CancellationToken ct)
        {
            if (!int.TryParse(productId, out var id) || qty <= 0)
                return;

            var product = await _storage.SelectAllErpProducts()
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == id, ct);
            if (product == null)
                return;

            var existing = session.Cart.FirstOrDefault(c => c.ErpProductId == id);
            if (existing != null)
            {
                existing.Quantity = qty;
                return;
            }

            session.Cart.Add(new StoreChatCartItem
            {
                ErpProductId = product.Id,
                Name = product.Name ?? product.Reference ?? $"Produit {product.Id}",
                Reference = product.Reference,
                Quantity = qty,
                UnitPrice = product.UnitPrice ?? product.PriceHT ?? 0
            });
        }

        private static void RemoveFromCart(StoreChatSession session, string? productId)
        {
            if (!int.TryParse(productId, out var id))
                return;
            session.Cart.RemoveAll(c => c.ErpProductId == id);
        }

        private async Task ReplaceCartFromTableAsync(
            StoreChatSession session,
            List<StoreChatTableCartLineDto>? lines,
            CancellationToken ct)
        {
            if (lines == null || lines.Count == 0)
                return;

            session.Cart.Clear();
            foreach (var line in lines)
                await AddToCartAsync(session, line.ProductId, line.Quantity <= 0 ? 1 : line.Quantity, ct);
        }

        // Estimations magasin (ordre de grandeur) pour un mur plein.
        private const decimal BricksPerM2 = 55m;
        private const decimal ParpaingsPerM2 = 12.5m;
        private const decimal MortarKgPerM2 = 30m;
        private const decimal DefaultBagKg = 25m;

        private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
        {
            "je", "tu", "il", "on", "nous", "vous", "les", "des", "une", "un", "de", "du", "la", "le",
            "et", "ou", "pour", "avec", "sans", "dans", "sur", "par", "pas", "plus", "très", "tres",
            "veux", "voudrais", "besoin", "cherche", "trouver", "acheter", "faire", "aide", "aider",
            "projet", "svp", "s'il", "sil", "vous", "plait", "ça", "ca", "est", "que", "qui", "quoi",
            "comme", "aussi", "donc", "alors", "mais", "mon", "ma", "mes", "ton", "votre", "notre",
            "mètre", "metre", "metres", "mètres", "cm", "mm", "haut", "haute", "hauteur", "longeur",
            "longueur", "large", "largeur", "de", "d", "l", "à", "a", "au", "aux", "construire", "mur"
        };

        private static readonly Dictionary<string, string[]> MaterialSynonyms = new(StringComparer.OrdinalIgnoreCase)
        {
            // FR / NL / EN — les libellés ERP sont multilingues
            ["brique"] = new[]
            {
                "brique", "briques", "briquetage",
                "baksteen", "bakstenen", "snelbouwsteen", "snelbouwstenen", "metselsteen", "metselstenen",
                "brick", "bricks"
            },
            ["mortier"] = new[]
            {
                "mortier", "mortiers",
                "mortel", "metselmortel", "voegmortel", "lijmmortel",
                "mortar"
            },
            ["ciment"] = new[]
            {
                "ciment", "ciments", "ciment portland",
                "cement", "portlandcement"
            },
            // Éviter le mot seul "block" (trop de faux positifs).
            ["parpaing"] = new[]
            {
                "parpaing", "parpaings", "agglo", "aggloméré", "agglomere", "hourdis",
                "betonblok", "betonblokken", "snelbouwblok", "snelbouwblokken", "cellenblok", "cellenbeton",
                "kalkzandsteen", "ytong",
                "concrete block", "cinder block", "breeze block"
            },
            ["pierre"] = new[]
            {
                "pierre", "pierres", "moellon", "moellons",
                "natuursteen", "steen",
                "stone", "masonry"
            },
            ["ferraillage"] = new[]
            {
                "ferraille", "ferraillage", "armature", "treillis",
                "wapening", "betonijzer", "draadmat",
                "rebar", "reinforcement", "mesh"
            },
            ["sable"] = new[]
            {
                "sable", "gravier",
                "zand", "grind",
                "sand", "gravel"
            },
            ["carrelage"] = new[]
            {
                "carrelage", "carreau", "carreaux", "faïence", "faience",
                "tegel", "tegels", "vloertegel", "wandtegel",
                "tile", "tiles", "tiling"
            },
            ["peinture"] = new[]
            {
                "peinture", "peintures", "lasurer", "enduit",
                "verf", "muurverf", "latex", "beits",
                "paint", "coating", "plaster"
            },
        };

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
                "prise", "interrupteur", "câble", "cable", "led",
                "stopcontact", "schakelaar", "draad",
                "socket", "switch", "wire"
            },
            ["garden_maintenance"] = new[]
            {
                "jardin", "tondeuse", "gazon", "haie",
                "tuin", "grasmaaier", "gazon", "haag",
                "garden", "lawnmower", "hedge"
            },
        };

        private static readonly string[] MasonryPositive = new[]
        {
            // FR
            "parpaing", "brique", "briques", "mortier", "ciment", "agglo", "agglom", "moellon",
            "hourdis", "maçon", "macon", "béton", "beton", "chaux", "sable", "gravier",
            // NL
            "baksteen", "snelbouwsteen", "metselsteen", "betonblok", "snelbouwblok", "cellenblok",
            "cellenbeton", "kalkzandsteen", "mortel", "metselmortel", "voegmortel", "cement",
            "metselwerk", "bouwmaterialen", "bouwmaterial", "zand", "grind", "wapening",
            // EN
            "brick", "bricks", "mortar", "cement", "concrete block", "cinder block", "masonry",
            "sand", "gravel", "rebar"
        };

        private static readonly string[] MasonryNoise = new[]
        {
            "flexi", "schuur", "sandpaper", "duracell", "battery", "batterij", "trolley",
            "module", "affuter", "frees", "filter", "kool", "charbon", "bague", "blocage",
            "trowel floreffe", "humiblock", "hellico", "helico", "bijl", "monoblock", "monobloc",
            "schuurblok", "sanding block", "power block"
        };

        private async Task<List<StoreChatProductSuggestionDto>> SearchProductsAsync(
            string text,
            StoreChatSession session,
            CancellationToken ct)
        {
            var terms = BuildSearchTerms(text, session);
            if (terms.Count == 0)
                return new List<StoreChatProductSuggestionDto>();

            var scores = new Dictionary<int, ScoredProduct>();
            foreach (var term in terms.Take(24))
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
                    .OrderBy(p => p.Name)
                    .Take(50)
                    .Select(p => new ScoredProduct
                    {
                        Id = p.Id,
                        Name = p.Name,
                        Reference = p.Reference,
                        Brand = p.Brand,
                        UnitPrice = p.UnitPrice,
                        PriceHT = p.PriceHT,
                        MainTypeName = p.MainTypeName,
                        TypeName = p.TypeName,
                        SubTypeName = p.SubTypeName,
                        Score = 1
                    })
                    .ToListAsync(ct);

                foreach (var p in rows)
                {
                    if (scores.TryGetValue(p.Id, out var existing))
                        existing.Score++;
                    else
                        scores[p.Id] = p;
                }
            }

            var wallMode = string.Equals(session.ActiveProjectDomainId, "wall_construction", StringComparison.OrdinalIgnoreCase);
            IEnumerable<ScoredProduct> ranked = scores.Values;
            if (wallMode)
            {
                ranked = ranked
                    .Where(IsMasonryRelevant)
                    .Select(p =>
                    {
                        p.Score += MasonryBoost(p);
                        return p;
                    });
            }

            return ranked
                .OrderByDescending(p => p.Score)
                .ThenBy(p => p.Name)
                .Take(_options.MaxProductResults)
                .Select(p => new StoreChatProductSuggestionDto
                {
                    ProductId = p.Id.ToString(CultureInfo.InvariantCulture),
                    Name = p.Name ?? p.Reference ?? $"Produit {p.Id}",
                    Price = p.UnitPrice ?? p.PriceHT,
                    Brand = p.Brand,
                    Category = string.Join(" / ", new[] { p.MainTypeName, p.TypeName, p.SubTypeName }
                        .Where(x => !string.IsNullOrWhiteSpace(x))),
                    SuggestedQuantity = 1
                })
                .ToList();
        }

        private static bool IsMasonryRelevant(ScoredProduct p)
        {
            var hay = $"{p.Name} {p.Brand} {p.MainTypeName} {p.TypeName} {p.SubTypeName}".ToLowerInvariant();
            if (MasonryNoise.Any(n => hay.Contains(n)))
                return false;
            return MasonryPositive.Any(k => hay.Contains(k));
        }

        private static int MasonryBoost(ScoredProduct p)
        {
            var hay = $"{p.Name} {p.MainTypeName} {p.TypeName} {p.SubTypeName}".ToLowerInvariant();
            var boost = 0;
            if (hay.Contains("parpaing") || hay.Contains("brique") || hay.Contains("mortier") || hay.Contains("ciment")
                || hay.Contains("baksteen") || hay.Contains("betonblok") || hay.Contains("mortel") || hay.Contains("cement")
                || hay.Contains("brick") || hay.Contains("mortar"))
                boost += 5;
            if (hay.Contains("bouwmaterial") || hay.Contains("metsel"))
                boost += 2;
            return boost;
        }

        private sealed class ScoredProduct
        {
            public int Id { get; set; }
            public string? Name { get; set; }
            public string? Reference { get; set; }
            public string? Brand { get; set; }
            public decimal? UnitPrice { get; set; }
            public decimal? PriceHT { get; set; }
            public string? MainTypeName { get; set; }
            public string? TypeName { get; set; }
            public string? SubTypeName { get; set; }
            public int Score { get; set; }
        }

        private static List<string> BuildSearchTerms(string text, StoreChatSession session)
        {
            var terms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var hint in session.MaterialHints)
                AddTermWithSynonyms(terms, hint);

            if (!string.IsNullOrWhiteSpace(session.ActiveProjectDomainId)
                && DomainSearchTerms.TryGetValue(session.ActiveProjectDomainId, out var domainTerms))
            {
                foreach (var t in domainTerms)
                    terms.Add(t);
            }

            foreach (var raw in text.Split(new[] { ' ', ',', ';', '.', '!', '?', '/', '\\', '\n', '\t' },
                         StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var token = raw.Trim().ToLowerInvariant();
                if (token.Length < 3 || StopWords.Contains(token) || token.Any(char.IsDigit))
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

        private static void AddTermWithSynonyms(HashSet<string> terms, string token)
        {
            terms.Add(token);
            foreach (var kv in MaterialSynonyms)
            {
                if (token.Contains(kv.Key, StringComparison.OrdinalIgnoreCase)
                    || kv.Value.Any(s => token.Contains(s, StringComparison.OrdinalIgnoreCase)))
                {
                    foreach (var s in kv.Value)
                        terms.Add(s);
                }
            }
        }

        private static void CollectMaterialHints(StoreChatSession session, string text)
        {
            var lower = text.ToLowerInvariant();
            foreach (var kv in MaterialSynonyms)
            {
                if (kv.Value.Any(s => lower.Contains(s, StringComparison.OrdinalIgnoreCase))
                    || lower.Contains(kv.Key, StringComparison.OrdinalIgnoreCase))
                {
                    if (!session.MaterialHints.Contains(kv.Key, StringComparer.OrdinalIgnoreCase))
                        session.MaterialHints.Add(kv.Key);
                }
            }

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

        private static void ParseWallDimensions(StoreChatSession session, string text)
        {
            var lower = text.ToLowerInvariant().Replace(',', '.');

            decimal? length = null;
            decimal? height = null;

            var lengthMatch = Regex.Match(lower, @"(?:longueur|longeur|long|lengte|length|l)\s*(?:de|:)?\s*(\d+(?:\.\d+)?)\s*(?:m|metres?|mètres?|mettres?)");
            var heightMatch = Regex.Match(lower, @"(?:hauteur|haut|hoogte|height|h)\s*(?:de|:)?\s*(\d+(?:\.\d+)?)\s*(?:m|metres?|mètres?|mettres?)");
            if (lengthMatch.Success && decimal.TryParse(lengthMatch.Groups[1].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var l1))
                length = l1;
            if (heightMatch.Success && decimal.TryParse(heightMatch.Groups[1].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var h1))
                height = h1;

            if (length is null || height is null)
            {
                var pair = Regex.Match(lower, @"(\d+(?:\.\d+)?)\s*(?:m|metres?|mètres?|mettres?)\D{0,32}(\d+(?:\.\d+)?)\s*(?:m|metres?|mètres?|mettres?)");
                if (pair.Success
                    && decimal.TryParse(pair.Groups[1].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var a)
                    && decimal.TryParse(pair.Groups[2].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var b))
                {
                    // Convention : première valeur = longueur, seconde = hauteur si non déjà trouvées.
                    length ??= a;
                    height ??= b;
                }
            }

            if (length is > 0)
                session.WallLengthM = length;
            if (height is > 0)
                session.WallHeightM = height;
        }

        private static void ApplySuggestedQuantities(
            List<StoreChatProductSuggestionDto> products,
            StoreChatSession session)
        {
            var area = session.WallAreaM2;
            foreach (var p in products)
            {
                var hay = $"{p.Name} {p.Category}".ToLowerInvariant();
                p.SuggestedQuantity = EstimateQuantity(hay, area);
            }
        }

        private static decimal EstimateQuantity(string hay, decimal? areaM2)
        {
            if (areaM2 is null or <= 0)
                return 1;

            var area = areaM2.Value;

            if (ContainsAny(hay, "brique", "briques", "briquetage", "baksteen", "snelbouwsteen", "metselsteen", "brick", "bricks"))
                return Math.Max(1, Math.Ceiling(area * BricksPerM2));

            if (ContainsAny(hay, "parpaing", "agglo", "agglom", "hourdis", "moellon",
                    "betonblok", "snelbouwblok", "cellenblok", "cellenbeton", "kalkzandsteen",
                    "concrete block", "cinder block", "breeze block"))
                return Math.Max(1, Math.Ceiling(area * ParpaingsPerM2));

            if (ContainsAny(hay, "mortier", "ciment", "chaux", "béton", "beton",
                    "mortel", "metselmortel", "voegmortel", "cement", "mortar"))
            {
                var bagKg = TryParseBagKg(hay) ?? DefaultBagKg;
                var kgNeeded = area * MortarKgPerM2;
                return Math.Max(1, Math.Ceiling(kgNeeded / bagKg));
            }

            if (ContainsAny(hay, "sable", "gravier", "zand", "grind", "sand", "gravel"))
                return Math.Max(1, Math.Ceiling(area * 0.05m)); // ~50 L/m² → sacs approx.

            // Outils / accessoires
            return 1;
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

        private static string BuildCatalogContext(IReadOnlyList<StoreChatProductSuggestionDto> products)
        {
            if (products.Count == 0)
                return "(aucun produit trouvé dans le catalogue local — ne propose aucun produit inventé; demande un autre mot-clé marque/type)";

            return "Produits catalogue à recommander UNIQUEMENT (quantités déjà estimées):\n"
                   + string.Join("\n", products.Take(12).Select(p =>
                       $"- [{p.ProductId}] {p.Name} | qté estimée {p.SuggestedQuantity:0} | {p.Brand} | {p.Category} | {p.Price:N2} €"));
        }

        private static string BuildUserFacingReply(
            string? aiReply,
            IReadOnlyList<StoreChatProductSuggestionDto> products,
            StoreChatSession session)
        {
            var calc = BuildCalculationSummary(session);

            if (products.Count > 0)
            {
                var intro = calc;
                if (string.IsNullOrWhiteSpace(intro))
                {
                    intro = $"Voici {products.Count} produit(s) du catalogue"
                            + (string.IsNullOrWhiteSpace(session.ActiveProjectDomainLabel)
                                ? "."
                                : $" pour {session.ActiveProjectDomainLabel}.");
                }

                intro += "\n\nLes quantités dans la liste sont des estimations : ajustez-les puis ajoutez au panier / devis / commande.";

                if (!string.IsNullOrWhiteSpace(aiReply)
                    && aiReply!.Length < 400
                    && !LooksLikeInventedProductList(aiReply))
                {
                    intro = calc + "\n\n" + aiReply.Trim()
                            + "\n\nLes quantités proposées sont préremplies dans le tableau.";
                }

                return intro.Trim();
            }

            if (!string.IsNullOrWhiteSpace(calc))
                return calc + "\n\nJe n'ai pas trouvé de parpaings/briques/mortier/ciment correspondants dans le catalogue. Affinez avec un matériau précis.";

            if (!string.IsNullOrWhiteSpace(aiReply) && !LooksLikeInventedProductList(aiReply))
                return aiReply!.Trim();

            return "Je n'ai pas trouvé de produit correspondant dans le catalogue. "
                   + "Indiquez un matériau ou une marque précise (ex. parpaing, brique, mortier, ciment).";
        }

        private static string BuildCalculationSummary(StoreChatSession session)
        {
            if (session.WallLengthM is not > 0 || session.WallHeightM is not > 0 || session.WallAreaM2 is not > 0)
                return string.Empty;

            var area = session.WallAreaM2!.Value;
            var bricks = Math.Ceiling(area * BricksPerM2);
            var parpaings = Math.Ceiling(area * ParpaingsPerM2);
            var mortarBags = Math.Ceiling(area * MortarKgPerM2 / DefaultBagKg);

            return $"Mur {session.WallLengthM:0.##} m × {session.WallHeightM:0.##} m → surface ≈ {area:0.##} m².\n"
                   + $"Estimations (ordre de grandeur) : ~{bricks:0} briques, ou ~{parpaings:0} parpaings, "
                   + $"et ~{mortarBags:0} sac(s) de mortier/ciment ({DefaultBagKg:0} kg).";
        }

        private static bool LooksLikeInventedProductList(string reply)
        {
            var lower = reply.ToLowerInvariant();
            return lower.Contains("voici quelques suggestions")
                   || lower.Contains("griffes pour murs")
                   || lower.Contains("griffe pour murs")
                   || (lower.Contains("matériaux suivants") && lower.Contains("*"));
        }

        private static void DetectDomain(StoreChatSession session, string text)
        {
            var lower = text.ToLowerInvariant();
            // Ordre important : construction mur avant peinture (évite que "mur" déclenche peinture).
            (string id, string label, string[] keys)[] domains =
            {
                ("wall_construction", "Construction de mur", new[]
                {
                    "construire un mur", "construction de mur", "mur de séparation", "mur de separation",
                    "mur de soutènement", "mur de souteinement", "parpaing", "ciment", "mortier", "brique",
                    "moellon", "agglo", "maçonner", "maconner", "briquetage",
                    "muur bouwen", "metselwerk", "baksteen", "betonblok", "mortel", "cement",
                    "build a wall", "brick wall", "masonry"
                }),
                ("painting", "Peinture", new[] { "peinture", "peindre", "rouleau à peindre", "sous-couche", "lasurer" }),
                ("tiling", "Carrelage", new[] { "carrelage", "carreau", "faïence", "faience" }),
                ("plumbing", "Plomberie", new[] { "plomberie", "robinet", "tuyau", "wc", "siphon" }),
                ("electrical", "Électricité", new[] { "électri", "electri", "prise", "interrupteur", "câble", "cable", "led" }),
                ("garden_maintenance", "Entretien jardin", new[] { "jardin", "tondeuse", "haie", "gazon" }),
            };

            foreach (var d in domains)
            {
                if (d.keys.Any(k => lower.Contains(k)))
                {
                    session.ActiveProjectDomainId = d.id;
                    session.ActiveProjectDomainLabel = d.label;
                    return;
                }
            }

            // Fallback : "mur" + dimensions / construire → maçonnerie
            if (lower.Contains("mur") && (lower.Contains("construire") || Regex.IsMatch(lower, @"\d+\s*m")))
            {
                session.ActiveProjectDomainId = "wall_construction";
                session.ActiveProjectDomainLabel = "Construction de mur";
            }
        }

        private void TrimHistory(StoreChatSession session)
        {
            var limit = Math.Max(4, _options.ChatHistoryLimit);
            if (session.History.Count > limit)
                session.History = session.History.TakeLast(limit).ToList();
        }

        private static StoreChatResponseDto Ok(StoreChatSession session, string text, string actionType) => new()
        {
            SessionId = session.SessionId,
            ReplyText = text,
            HasAction = !string.Equals(actionType, "NONE", StringComparison.OrdinalIgnoreCase),
            ActionType = actionType,
            ActiveProjectDomainId = session.ActiveProjectDomainId,
            ActiveProjectDomainLabel = session.ActiveProjectDomainLabel
        };

        private static StoreChatPaymentResultDto ToPaymentResult(StoreChatOrder order) => new()
        {
            Status = order.Status,
            OrderId = order.Id.ToString("D"),
            InvoiceNumber = order.InvoiceNumber,
            InvoicePdf = string.IsNullOrWhiteSpace(order.InvoicePdfBase64)
                ? null
                : new StoreChatQuotePdfDto
                {
                    PdfBase64 = order.InvoicePdfBase64!,
                    FileName = order.InvoiceFileName ?? $"facture-{order.InvoiceNumber}.pdf",
                    Total = order.TotalAmount,
                    Source = "invoice",
                    SourceLabel = "Facture"
                },
            SuggestNewProject = true,
            Source = "store-chat",
            SourceLabel = "Assistant magasin"
        };

        private static List<StoreChatCartItem> DeserializeCart(string json)
        {
            try
            {
                return JsonSerializer.Deserialize<List<StoreChatCartItem>>(json, JsonOptions)
                       ?? new List<StoreChatCartItem>();
            }
            catch
            {
                return new List<StoreChatCartItem>();
            }
        }
    }
}
