using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Brokers.Storage;
using Backup.Web.Api.Server.Models;
using Backup.Web.Api.Server.Services.StoreChat;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Backup.Web.Api.Server.Services.SalesAssistant
{
    public interface ISalesCommerceTool
    {
        Task AddToCartAsync(StoreChatSession session, string? productId, decimal qty, CancellationToken ct = default);
        void RemoveFromCart(StoreChatSession session, string? productId);
        Task ReplaceCartFromTableAsync(
            StoreChatSession session,
            List<StoreChatTableCartLineDto>? lines,
            CancellationToken ct = default);

        Task<StoreChatResponseDto> CreateQuoteAsync(StoreChatSession session, CancellationToken ct = default);

        /// <summary>Crée la commande + Stripe, ou confirme en mode démo (facture).</summary>
        Task<StoreChatResponseDto> CreateOrderAsync(StoreChatSession session, CancellationToken ct = default);

        Task<StoreChatPaymentResultDto?> GetPaymentResultAsync(Guid orderId, CancellationToken ct = default);

        Task<StoreChatPaymentResultDto?> ConfirmPaymentAsync(
            Guid orderId,
            string? stripeSessionId,
            CancellationToken ct = default);
    }

    /// <summary>
    /// Outil C# panier / devis / commande — jamais invoqué par le LLM.
    /// </summary>
    public sealed class SalesCommerceTool : ISalesCommerceTool
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private readonly IStorageBroker _storage;
        private readonly IStoreChatSessionStore _sessions;
        private readonly IStoreChatPdfService _pdf;
        private readonly IStoreChatStripeService _stripe;
        private readonly ISalesWorkflowGuard _workflow;
        private readonly ISalesLogisticsEngine _logistics;
        private readonly ILogger<SalesCommerceTool> _logger;

        public SalesCommerceTool(
            IStorageBroker storage,
            IStoreChatSessionStore sessions,
            IStoreChatPdfService pdf,
            IStoreChatStripeService stripe,
            ISalesWorkflowGuard workflow,
            ISalesLogisticsEngine logistics,
            ILogger<SalesCommerceTool> logger)
        {
            _storage = storage;
            _sessions = sessions;
            _pdf = pdf;
            _stripe = stripe;
            _workflow = workflow;
            _logistics = logistics;
            _logger = logger;
        }

        public async Task AddToCartAsync(
            StoreChatSession session,
            string? productId,
            decimal qty,
            CancellationToken ct = default)
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
                Name = FormatProductDisplayName(product.Name, product.Name2, product.Reference, product.Id),
                Reference = product.Reference,
                Quantity = qty,
                UnitPrice = product.UnitPrice ?? product.PriceHT ?? 0
            });
        }

        public void RemoveFromCart(StoreChatSession session, string? productId)
        {
            if (!int.TryParse(productId, out var id))
                return;
            session.Cart.RemoveAll(c => c.ErpProductId == id);
        }

        public async Task ReplaceCartFromTableAsync(
            StoreChatSession session,
            List<StoreChatTableCartLineDto>? lines,
            CancellationToken ct = default)
        {
            if (lines == null || lines.Count == 0)
                return;

            session.Cart.Clear();
            foreach (var line in lines)
                await AddToCartAsync(session, line.ProductId, line.Quantity <= 0 ? 1 : line.Quantity, ct);
        }

        public async Task<StoreChatResponseDto> CreateQuoteAsync(
            StoreChatSession session,
            CancellationToken ct = default)
        {
            if (session.Cart.Count == 0)
                return Ok(session, "Panier vide. Ajoutez des produits avant de demander un devis.", "NONE");

            if (!_workflow.CanExecute(WorkflowActions.CreateQuote, session.WorkflowState))
                return DenyWorkflow(session, WorkflowActions.CreateQuote);

            var pdf = _pdf.GenerateQuote(session.Cart, "Client magasin");
            var quote = new StoreChatQuote
            {
                SessionId = session.SessionId,
                Number = $"DEV-{DateTime.UtcNow:yyyyMMddHHmmss}",
                TotalAmount = pdf.Total ?? 0,
                PdfBase64 = pdf.PdfBase64,
                FileName = pdf.FileName,
                LinesJson = JsonSerializer.Serialize(session.Cart, JsonOptions),
                SalesProjectId = session.ActiveSalesProjectId
            };
            await _storage.InsertStoreChatQuoteAsync(quote);
            _workflow.ApplyTransition(session, WorkflowActions.CreateQuote);
            _sessions.Save(session);

            var logistics = _logistics.Evaluate(session);
            var quoteReply = $"Devis prêt ({pdf.Total:N2} €). Vous pouvez le télécharger.";
            if (session.ActiveSalesProjectId is Guid pid)
                quoteReply += $"\nRattaché au projet {pid:D}.";
            quoteReply += "\n" + logistics.Summary;

            return new StoreChatResponseDto
            {
                SessionId = session.SessionId,
                ReplyText = quoteReply,
                HasAction = true,
                ActionType = "QUOTE_PDF",
                QuotePdf = pdf,
                ActiveProjectDomainId = session.ActiveProjectDomainId,
                ActiveProjectDomainLabel = session.ActiveProjectDomainLabel,
                SalesProjectId = session.ActiveSalesProjectId,
                Logistics = logistics,
                WorkflowState = session.WorkflowState.ToString(),
                ProjectSummary = session.Project.SummaryLine(),
                ProjectBaseComplete = SalesComplementRules.IsBaseComplete(session)
            };
        }

        public async Task<StoreChatResponseDto> CreateOrderAsync(
            StoreChatSession session,
            CancellationToken ct = default)
        {
            if (session.Cart.Count == 0)
                return Ok(session, "Panier vide. Ajoutez des produits avant de commander.", "NONE");

            if (!_workflow.CanExecute(WorkflowActions.CreateOrder, session.WorkflowState))
                return DenyWorkflow(session, WorkflowActions.CreateOrder);

            var order = new StoreChatOrder
            {
                SessionId = session.SessionId,
                Status = "pending",
                TotalAmount = session.Cart.Sum(c => c.TotalPrice),
                LinesJson = JsonSerializer.Serialize(session.Cart, JsonOptions),
                SalesProjectId = session.ActiveSalesProjectId
            };
            await _storage.InsertStoreChatOrderAsync(order);
            session.LastOrderId = order.Id;
            _workflow.ApplyTransition(session, WorkflowActions.CreateOrder);

            if (_stripe.IsEnabled)
            {
                var link = await _stripe.CreateCheckoutAsync(
                    order.Id,
                    session.Cart,
                    session.SessionId,
                    session.ReturnBaseUrl,
                    ct);
                if (link != null)
                {
                    order.StripeSessionId = link.Source;
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
                        ActiveProjectDomainLabel = session.ActiveProjectDomainLabel,
                        WorkflowState = session.WorkflowState.ToString(),
                        ProjectSummary = session.Project.SummaryLine(),
                        ProjectBaseComplete = SalesComplementRules.IsBaseComplete(session)
                    };
                }
            }

            // Mode démo sans Stripe : confirme directement et génère facture.
            _logger.LogInformation("Order {OrderId} created without Stripe — demo confirm", order.Id);
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
                ActiveProjectDomainLabel = session.ActiveProjectDomainLabel,
                WorkflowState = session.WorkflowState.ToString(),
                ProjectSummary = session.Project.SummaryLine(),
                ProjectBaseComplete = SalesComplementRules.IsBaseComplete(session)
            };
        }

        public async Task<StoreChatPaymentResultDto?> GetPaymentResultAsync(Guid orderId, CancellationToken ct = default)
        {
            var order = await _storage.SelectAllStoreChatOrders()
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.Id == orderId, ct);
            return order == null ? null : ToPaymentResult(order);
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
                var already = ToPaymentResult(order);
                already.CartCleared = ClearSessionCartAfterPayment(order.SessionId);
                return already;
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

            var chatSession = _sessions.GetOrCreate(order.SessionId);
            chatSession.LastOrderId = order.Id;
            ClearSessionCart(chatSession);
            _workflow.ApplyTransition(chatSession, WorkflowActions.ConfirmPayment);
            // Nouveau cycle (ex. peinture après mur) : panier vide + état Idle.
            chatSession.WorkflowState = SalesWorkflowState.Idle;
            _sessions.Save(chatSession);

            var result = ToPaymentResult(order);
            result.CartCleared = true;
            return result;
        }

        private bool ClearSessionCartAfterPayment(string sessionId)
        {
            var chatSession = _sessions.Get(sessionId);
            if (chatSession == null)
                return false;
            ClearSessionCart(chatSession);
            // Nouveau cycle (ex. peinture après mur) : panier vide + état Idle.
            chatSession.WorkflowState = SalesWorkflowState.Idle;
            _sessions.Save(chatSession);
            return true;
        }

        private static void ClearSessionCart(StoreChatSession chatSession)
        {
            chatSession.Cart.Clear();
            chatSession.LastSuggestedProducts.Clear();
            chatSession.PendingComplementHints.Clear();
            chatSession.AwaitingComplementConfirm = false;
        }

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

        private StoreChatResponseDto DenyWorkflow(StoreChatSession session, string action)
        {
            var msg = _workflow.DenyMessage(action, session.WorkflowState);
            _logger.LogInformation(
                "Workflow deny action={Action} state={State} session={Session}",
                action, session.WorkflowState, session.SessionId);
            return Ok(session, msg, "WORKFLOW_DENIED");
        }

        private static StoreChatResponseDto Ok(StoreChatSession session, string text, string actionType) => new()
        {
            SessionId = session.SessionId,
            ReplyText = text,
            HasAction = !string.Equals(actionType, "NONE", StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(actionType, "WORKFLOW_DENIED", StringComparison.OrdinalIgnoreCase),
            ActionType = actionType,
            ActiveProjectDomainId = session.ActiveProjectDomainId,
            ActiveProjectDomainLabel = session.ActiveProjectDomainLabel,
            WorkflowState = session.WorkflowState.ToString(),
            ProjectSummary = session.Project.SummaryLine(),
            ProjectBaseComplete = SalesComplementRules.IsBaseComplete(session)
        };

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
    }
}
