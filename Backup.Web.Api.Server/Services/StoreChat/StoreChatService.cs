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
            var products = await SearchProductsAsync(text, ct);
            var catalogContext = BuildCatalogContext(products);
            var aiReply = await _ai.CompleteAsync(session.History, text, catalogContext, ct);

            session.History.Add(new StoreChatHistoryMessage { Role = "user", Content = text });
            var reply = aiReply
                ?? (products.Count > 0
                    ? $"Voici {products.Count} produit(s) correspondant à votre demande."
                    : "Je n'ai pas trouvé de produit correspondant. Reformulez votre recherche (marque, type, usage).");

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

        private async Task<List<StoreChatProductSuggestionDto>> SearchProductsAsync(string text, CancellationToken ct)
        {
            var terms = text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(t => t.Length >= 2)
                .Select(t => t.ToLowerInvariant())
                .Take(6)
                .ToList();

            if (terms.Count == 0)
                return new List<StoreChatProductSuggestionDto>();

            var query = _storage.SelectAllErpProducts().AsNoTracking();
            foreach (var term in terms)
            {
                query = query.Where(p =>
                    (p.Name != null && p.Name.ToLower().Contains(term))
                    || (p.Name2 != null && p.Name2.ToLower().Contains(term))
                    || (p.Reference != null && p.Reference.ToLower().Contains(term))
                    || (p.Brand != null && p.Brand.ToLower().Contains(term))
                    || (p.TypeName != null && p.TypeName.ToLower().Contains(term))
                    || (p.SubTypeName != null && p.SubTypeName.ToLower().Contains(term))
                    || (p.MainTypeName != null && p.MainTypeName.ToLower().Contains(term)));
            }

            var rows = await query
                .OrderBy(p => p.Name)
                .Take(_options.MaxProductResults)
                .Select(p => new
                {
                    p.Id,
                    p.Name,
                    p.Reference,
                    p.Brand,
                    p.UnitPrice,
                    p.PriceHT,
                    p.MainTypeName,
                    p.TypeName,
                    p.SubTypeName
                })
                .ToListAsync(ct);

            return rows.Select(p => new StoreChatProductSuggestionDto
            {
                ProductId = p.Id.ToString(CultureInfo.InvariantCulture),
                Name = p.Name ?? p.Reference ?? $"Produit {p.Id}",
                Price = p.UnitPrice ?? p.PriceHT,
                Brand = p.Brand,
                Category = string.Join(" / ", new[] { p.MainTypeName, p.TypeName, p.SubTypeName }
                    .Where(x => !string.IsNullOrWhiteSpace(x))),
                SuggestedQuantity = 1
            }).ToList();
        }

        private static string BuildCatalogContext(IReadOnlyList<StoreChatProductSuggestionDto> products)
        {
            if (products.Count == 0)
                return "(aucun produit trouvé)";

            return string.Join("\n", products.Take(12).Select(p =>
                $"- [{p.ProductId}] {p.Name} | {p.Brand} | {p.Category} | {p.Price:N2} €"));
        }

        private static void DetectDomain(StoreChatSession session, string text)
        {
            var lower = text.ToLowerInvariant();
            (string id, string label, string[] keys)[] domains =
            {
                ("painting", "Peinture", new[] { "peinture", "peindre", "mur", "plafond", "rouleau" }),
                ("tiling", "Carrelage", new[] { "carrelage", "carreau", "colle", "joint" }),
                ("plumbing", "Plomberie", new[] { "plomberie", "robinet", "tuyau", "wc", "siphon" }),
                ("electrical", "Électricité", new[] { "électri", "electri", "prise", "interrupteur", "câble", "cable", "led" }),
                ("garden_maintenance", "Entretien jardin", new[] { "jardin", "tondeuse", "haie", "gazon" }),
                ("wall_construction", "Construction de mur", new[] { "parpaing", "ciment", "mortier", "brique" })
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
