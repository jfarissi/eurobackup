using System.Linq;

namespace Backup.Web.Api.Server.Services.Tenancy
{
    public static class CompanyQueryExtensions
    {
        /// <summary>Filtre par société ; inclut les lignes legacy sans CompanyId.</summary>
        public static IQueryable<T> ForCompany<T>(this IQueryable<T> query, string? companyId)
            where T : class, IHasCompanyId
        {
            if (string.IsNullOrWhiteSpace(companyId)) return query;
            return query.Where(e => e.CompanyId == null || e.CompanyId == companyId);
        }

        public static void EnsureCompanyId(this IHasCompanyId entity, string? companyId)
        {
            if (string.IsNullOrWhiteSpace(entity.CompanyId) && !string.IsNullOrWhiteSpace(companyId))
                entity.CompanyId = companyId;
        }

        public static bool BelongsToCompany(this IHasCompanyId entity, string? companyId)
        {
            if (string.IsNullOrWhiteSpace(companyId)) return true;
            if (string.IsNullOrWhiteSpace(entity.CompanyId)) return true;
            return entity.CompanyId == companyId;
        }
    }
}
