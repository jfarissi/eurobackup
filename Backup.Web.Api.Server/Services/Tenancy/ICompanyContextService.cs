using System.Threading.Tasks;

namespace Backup.Web.Api.Server.Services.Tenancy
{
    public interface ICompanyContextService
    {
        /// <summary>Société active (JWT CompanyId ou en-tête X-Company-ID).</summary>
        string? GetCurrentCompanyId();
        Guid? GetCurrentUserId();

        /// <summary>True si la société courante a le sync catalogue ERP (Euro Brico).</summary>
        Task<bool> CurrentCompanyHasErpCatalogSyncAsync();
    }
}
