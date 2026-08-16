using System;

namespace Backup.Web.Api.Server.Models.Catalog
{
    /// <summary>Cache persisté des feuilles catégorie RapidAPI pour un K-Type (évite un appel API à chaque VIN).</summary>
    public class ErpRapidApiKTypeCategoryCache
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string KType { get; set; } = string.Empty;
        public string CategoriesJson { get; set; } = "[]";
        public int CategoryCount { get; set; }
        public DateTime FetchedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
