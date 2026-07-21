using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;

namespace Backup.Web.Api.Server.Services.StoreChat
{
    public interface IStoreChatStripeService
    {
        bool IsEnabled { get; }
        Task<StoreChatPaymentLinkDto?> CreateCheckoutAsync(
            Guid orderId,
            IReadOnlyList<StoreChatCartItem> items,
            string sessionId,
            CancellationToken ct = default);
        Task<string?> GetCheckoutSessionStatusAsync(string stripeSessionId, CancellationToken ct = default);
    }

    public class StoreChatStripeService : IStoreChatStripeService
    {
        private readonly StripeOptions _stripe;
        private readonly StoreChatOptions _store;
        private readonly ILogger<StoreChatStripeService> _logger;

        public StoreChatStripeService(
            IOptions<StripeOptions> stripe,
            IOptions<StoreChatOptions> store,
            ILogger<StoreChatStripeService> logger)
        {
            _stripe = stripe.Value ?? new StripeOptions();
            _store = store.Value ?? new StoreChatOptions();
            _logger = logger;
            if (_stripe.Enabled)
                StripeConfiguration.ApiKey = _stripe.SecretKey;
        }

        public bool IsEnabled => _stripe.Enabled;

        public async Task<StoreChatPaymentLinkDto?> CreateCheckoutAsync(
            Guid orderId,
            IReadOnlyList<StoreChatCartItem> items,
            string sessionId,
            CancellationToken ct = default)
        {
            if (!_stripe.Enabled || items.Count == 0)
                return null;

            try
            {
                var baseUrl = _store.ReturnBaseUrl.TrimEnd('/');
                var successUrl = $"{baseUrl}/assistant?payment=success&orderId={orderId:D}&session_id={{CHECKOUT_SESSION_ID}}";
                var cancelUrl = $"{baseUrl}/assistant?payment=cancel&orderId={orderId:D}";

                var lineItems = items.Select(i => new SessionLineItemOptions
                {
                    Quantity = (long)Math.Max(1, Math.Ceiling(i.Quantity)),
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = "eur",
                        UnitAmount = (long)Math.Round(i.UnitPrice * 100m, MidpointRounding.AwayFromZero),
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = string.IsNullOrWhiteSpace(i.Name) ? $"Produit {i.ErpProductId}" : i.Name
                        }
                    }
                }).ToList();

                var options = new SessionCreateOptions
                {
                    Mode = "payment",
                    SuccessUrl = successUrl,
                    CancelUrl = cancelUrl,
                    LineItems = lineItems,
                    Metadata = new Dictionary<string, string>
                    {
                        ["orderId"] = orderId.ToString("D"),
                        ["chatSessionId"] = sessionId
                    }
                };

                var service = new SessionService();
                var session = await service.CreateAsync(options, cancellationToken: ct);
                var amount = items.Sum(i => i.TotalPrice);

                return new StoreChatPaymentLinkDto
                {
                    Url = session.Url,
                    Amount = amount,
                    Description = $"Commande {_store.BrandName}",
                    OrderId = orderId.ToString("D"),
                    Source = session.Id,
                    SourceLabel = "Carte bancaire"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Stripe checkout failed for order {OrderId}", orderId);
                return null;
            }
        }

        public async Task<string?> GetCheckoutSessionStatusAsync(string stripeSessionId, CancellationToken ct = default)
        {
            if (!_stripe.Enabled || string.IsNullOrWhiteSpace(stripeSessionId))
                return null;

            try
            {
                var service = new SessionService();
                var session = await service.GetAsync(stripeSessionId, cancellationToken: ct);
                return session.PaymentStatus;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Stripe session status failed for {SessionId}", stripeSessionId);
                return null;
            }
        }
    }
}
