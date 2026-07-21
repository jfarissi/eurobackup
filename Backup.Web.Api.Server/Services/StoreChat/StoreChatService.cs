using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
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
            CollectMaterialHints(session, text);
            var products = await SearchProductsAsync(text, session, ct);
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

        private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
        {
            "je", "tu", "il", "on", "nous", "vous", "les", "des", "une", "un", "de", "du", "la", "le",
            "et", "ou", "pour", "avec", "sans", "dans", "sur", "par", "pas", "plus", "très", "tres",
            "veux", "voudrais", "besoin", "cherche", "trouver", "acheter", "faire", "aide", "aider",
            "projet", "svp", "s'il", "sil", "vous", "plait", "ça", "ca", "est", "que", "qui", "quoi",
            "comme", "aussi", "donc", "alors", "mais", "mon", "ma", "mes", "ton", "votre", "notre",
            "mètre", "metre", "metres", "mètres", "cm", "mm", "haut", "haute", "hauteur", "longeur",
            "longueur", "large", "largeur", "de", "d", "l", "à", "a", "au", "aux"
        };

        private static readonly Dictionary<string, string[]> MaterialSynonyms = new(StringComparer.OrdinalIgnoreCase)
        {
            ["brique"] = new[] { "brique", "briques", "briquetage" },
            ["mortier"] = new[] { "mortier", "ciment", "chaux" },
            ["parpaing"] = new[] { "parpaing", "parpaings", "agglo", "aggloméré", "agglomere", "bloc" },
            ["pierre"] = new[] { "pierre", "pierres", "moellon", "moellons" },
            ["ferraillage"] = new[] { "ferraille", "ferraillage", "armature", "treillis" },
            ["sable"] = new[] { "sable", "gravier" },
            ["carrelage"] = new[] { "carrelage", "carreau", "carreaux", "faïence", "faience" },
            ["peinture"] = new[] { "peinture", "peintures", "lasurer", "enduit" },
        };

        private static readonly Dictionary<string, string[]> DomainSearchTerms = new(StringComparer.OrdinalIgnoreCase)
        {
            ["wall_construction"] = new[] { "parpaing", "brique", "mortier", "ciment", "bloc", "agglo" },
            ["painting"] = new[] { "peinture", "rouleau", "enduit", "sous-couche" },
            ["tiling"] = new[] { "carrelage", "colle", "joint", "carreau" },
            ["plumbing"] = new[] { "robinet", "tuyau", "siphon", "pvc" },
            ["electrical"] = new[] { "prise", "interrupteur", "câble", "cable", "led" },
            ["garden_maintenance"] = new[] { "jardin", "tondeuse", "gazon", "haie" },
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
            foreach (var term in terms.Take(10))
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
                    .Take(40)
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

            return scores.Values
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
                AddTermWithSynonyms(terms, token);
            }

            return terms.Take(12).ToList();
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

            if (lower.Contains("construire") && lower.Contains("mur")
                && !session.MaterialHints.Contains("parpaing", StringComparer.OrdinalIgnoreCase))
            {
                session.MaterialHints.Add("parpaing");
                session.MaterialHints.Add("mortier");
            }
        }

        private static string BuildCatalogContext(IReadOnlyList<StoreChatProductSuggestionDto> products)
        {
            if (products.Count == 0)
                return "(aucun produit trouvé dans le catalogue local — ne propose aucun produit inventé; demande un autre mot-clé marque/type)";

            return "Produits catalogue à recommander UNIQUEMENT:\n"
                   + string.Join("\n", products.Take(12).Select(p =>
                       $"- [{p.ProductId}] {p.Name} | {p.Brand} | {p.Category} | {p.Price:N2} €"));
        }

        private static string BuildUserFacingReply(
            string? aiReply,
            IReadOnlyList<StoreChatProductSuggestionDto> products,
            StoreChatSession session)
        {
            if (products.Count > 0)
            {
                var intro = !string.IsNullOrWhiteSpace(aiReply) && aiReply!.Length < 600
                    ? aiReply.Trim()
                    : $"Voici {products.Count} produit(s) du catalogue pour votre projet"
                      + (string.IsNullOrWhiteSpace(session.ActiveProjectDomainLabel)
                          ? "."
                          : $" ({session.ActiveProjectDomainLabel}).");

                if (!intro.Contains("ci-dessous", StringComparison.OrdinalIgnoreCase)
                    && !intro.Contains("catalogue", StringComparison.OrdinalIgnoreCase)
                    && !intro.Contains("panier", StringComparison.OrdinalIgnoreCase))
                {
                    intro += "\n\nChoisissez les quantités dans la liste ci-dessous, puis ajoutez au panier / devis / commande.";
                }

                return intro;
            }

            if (!string.IsNullOrWhiteSpace(aiReply)
                && !LooksLikeInventedProductList(aiReply))
                return aiReply!.Trim();

            return "Je n'ai pas trouvé de produit correspondant dans le catalogue. "
                   + "Indiquez un matériau ou une marque précise (ex. parpaing, brique, mortier, ciment).";
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
                    "moellon", "agglo", "maçonner", "maconner", "briquetage"
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
            if (lower.Contains("mur") && (lower.Contains("construire") || lower.Contains("m ") || lower.Contains("metre") || lower.Contains("mètre")))
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
