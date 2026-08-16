using System.Threading;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace Backup.Web.Api.Server.Services.SupplierQuotes
{
    public interface ISupplierQuoteNotifier
    {
        Task NotifyQuotesUpdatedAsync(string companyId, int productId, SupplierQuotesResult result, CancellationToken cancellationToken = default);
    }

    public sealed class SupplierQuoteNotifier : ISupplierQuoteNotifier
    {
        private readonly IHubContext<SupplierQuotesHub> hub;

        public SupplierQuoteNotifier(IHubContext<SupplierQuotesHub> hub)
        {
            this.hub = hub;
        }

        public Task NotifyQuotesUpdatedAsync(
            string companyId, int productId, SupplierQuotesResult result, CancellationToken cancellationToken = default)
        {
            return this.hub.Clients
                .Group(SupplierQuotesHub.ProductGroup(companyId, productId))
                .SendAsync(SupplierQuotesHub.QuotesUpdatedEvent, result, cancellationToken);
        }
    }
}
