using System.Collections.Generic;

namespace Backup.Web.Api.Server.Services.SalesAssistant
{
    /// <summary>Filtre métier explicite (pas une seule string de recherche).</summary>
    public class ProductSearchFilter
    {
        public string? Brand { get; set; }
        public List<string> Categories { get; set; } = new();
        public decimal? WeightKg { get; set; }
        public decimal? MaxUnitPrice { get; set; }
        public string? SkillLevel { get; set; }
        public bool WeightApplied { get; set; }
        public bool IsYesNoBrandQuestion { get; set; }
        public int TotalMatches { get; set; }
        public ProductSearchOutcome Outcome { get; set; } = ProductSearchOutcome.Generic;
        public string? Intent { get; set; }

        /// <summary>Étape parcours mur (structure / liant / treillis / outils).</summary>
        public WallGuideFamily? WallGuideFamily { get; set; }

        /// <summary>Alias historique — TypeHints = Categories.</summary>
        public List<string> TypeHints
        {
            get => Categories;
            set => Categories = value ?? new List<string>();
        }
    }

    public enum ProductSearchOutcome
    {
        Generic,
        Domain,
        BrandOnly,
        BrandAndType,
        BrandWithoutType,
        BrandNotFound,
        WeightNotFound
    }
}
