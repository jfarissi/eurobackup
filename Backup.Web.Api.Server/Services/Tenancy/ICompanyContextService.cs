namespace Backup.Web.Api.Server.Services.Tenancy
{
    public interface ICompanyContextService
    {
        /// <summary>Société active (JWT CompanyId ou en-tête X-Company-ID).</summary>
        string? GetCurrentCompanyId();
        Guid? GetCurrentUserId();
    }
}
