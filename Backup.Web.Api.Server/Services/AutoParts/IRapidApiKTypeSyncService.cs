using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Backup.Web.Api.Server.Services.AutoParts
{
    public record RapidApiCategoryDto(
        int Id,
        string Name,
        string Family,
        string FamilyLabel,
        [property: JsonPropertyName("parent")] string? ParentName);

    public record RapidApiCategoryListDto(
        string KType,
        List<RapidApiCategoryDto> Categories);

    public interface IRapidApiKTypeSyncService
    {
        Task<RapidApiCategoryListDto> ListCategoriesAsync(string kType, CancellationToken ct = default);

        /// <summary>
        /// Importe les articles RapidAPI pour un K-Type via le script Python.
        /// Si <paramref name="categoryIds"/> est fourni, n'importe que ces catégories.
        /// </summary>
        Task<int> SyncKTypeAsync(
            string kType,
            string make,
            string model,
            int? year = null,
            IReadOnlyList<int>? categoryIds = null,
            string? fuelType = null,
            CancellationToken ct = default);
    }
}
