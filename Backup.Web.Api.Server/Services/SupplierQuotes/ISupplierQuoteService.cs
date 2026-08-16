using System.Threading;
using System.Threading.Tasks;

namespace Backup.Web.Api.Server.Services.SupplierQuotes
{
    public interface ISupplierQuoteService
    {
        Task<SupplierQuotesResult> GetQuotesAsync(int productId, string companyId, bool forceRefresh, CancellationToken cancellationToken);
    }
}
