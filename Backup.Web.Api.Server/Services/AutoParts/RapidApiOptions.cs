using System.Collections.Generic;

namespace Backup.Web.Api.Server.Services.AutoParts
{
    /// <summary>Options RapidAPI Auto Parts Catalog (sync + VIN).</summary>
    public class RapidApiOptions
    {
        public const string SectionName = "RapidApi";

        public bool Enabled { get; set; } = true;
        public string BaseUrl { get; set; } = "https://auto-parts-catalog.p.rapidapi.com";
        public string Host { get; set; } = "auto-parts-catalog.p.rapidapi.com";
        public string? ApiKey { get; set; }
        public int LangId { get; set; } = 6;
        public int CountryFilterId { get; set; } = 34;
        public int TypeId { get; set; } = 1;

        /// <summary>
        /// Opt-in uniquement. Défaut false : VIN = cache local + NHTSA, sans dépendance RapidAPI.
        /// </summary>
        public bool EnableVinLookup { get; set; } = false;

        /// <summary>
        /// Chemins relatifs avec {vin}. Essayés dans l'ordre jusqu'au premier succès.
        /// Docs Making Data Meaningful : tecdoc-vin-check, decoder-v1, decoder-v2.
        /// </summary>
        public List<string> VinCheckPaths { get; set; } = new()
        {
            "/vin/tecdoc-vin-check/{vin}",
            "/vin/decoder-v1/{vin}",
            "/vin/decoder-v2/{vin}"
        };
    }
}
