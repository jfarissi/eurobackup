namespace Backup.Web.Api.Server.Services.Tenancy
{
    public interface IHasCompanyId
    {
        string? CompanyId { get; set; }
    }
}
