using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Backup.Web.Api.Server.Services.AutoParts
{
    public record KTypeEnrichmentContext(
        string? CompanyId,
        string? Vin,
        string? Make,
        string? Model,
        int? Year,
        string? EngineCode,
        string Source,
        string? FuelType = null);

    public record KTypeEnrichmentResult(
        bool Queued,
        bool CatalogSynced,
        int ProductsImported,
        string? Message = null,
        bool SyncInProgress = false,
        bool NeedsCategorySelection = false);

    public interface IKTypeEnrichmentService
    {
        Task<bool> ExistsInCatalogAsync(string kType, CancellationToken ct = default);

        /// <summary>
        /// Si K-Type absent du catalogue : file d'attente (sans import auto).
        /// L'import RapidAPI se lance après choix des catégories.
        /// </summary>
        Task<KTypeEnrichmentResult> EnrichIfMissingAsync(
            string kType, KTypeEnrichmentContext context, CancellationToken ct = default);

        Task<KTypeEnrichmentResult> StartOnDemandImportAsync(
            string kType,
            KTypeEnrichmentContext context,
            IReadOnlyList<int> categoryIds,
            CancellationToken ct = default);
    }
}
