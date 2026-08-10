namespace Backup.Web.Api.Server.Services.AutoParts
{
    /// <summary>Config lecture plaque (marché cible : Maroc en premier).</summary>
    public class PlateScanOptions
    {
        public const string SectionName = "PlateScan";

        /// <summary>Code pays ISO par défaut (MA = Maroc).</summary>
        public string DefaultCountry { get; set; } = "MA";

        /// <summary>Clé API fournisseur plaque→véhicule (ex. Afteriize / partenaire MA). Vide = stub démo.</summary>
        public string? ApiKey { get; set; }

        /// <summary>URL de base du fournisseur plaque (à brancher en prod).</summary>
        public string? ProviderBaseUrl { get; set; }

        /// <summary>Autoriser le décodage VIN via NHTSA (fallback gratuit).</summary>
        public bool EnableNhtsaVin { get; set; } = true;

        public string NhtsaVinUrl { get; set; } =
            "https://vpic.nhtsa.dot.gov/api/vehicles/DecodeVinValuesExtended";

        /// <summary>Nombre max de pièces compatibles retournées.</summary>
        public int MaxProducts { get; set; } = 50;
    }
}
