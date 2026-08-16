using System.Threading;
using System.Threading.Tasks;

namespace Backup.Web.Api.Server.Services.SupplierQuotes
{
    public interface ISupplierFeedAdapter
    {
        string FeedCode { get; }
        Task<SupplierQuoteDto?> QuoteAsync(SupplierQuoteRequest request, CancellationToken cancellationToken);
    }
}
