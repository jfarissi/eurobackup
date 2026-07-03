using System.Threading;
using System.Threading.Tasks;

namespace Backup.Web.Api.Server.Services.Pricing
{
    public interface IErpPricingService
    {
        Task<decimal?> GetProductPriceAsync(string productCode, CancellationToken ct);
    }
}
