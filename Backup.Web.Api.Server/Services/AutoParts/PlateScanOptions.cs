namespace Backup.Web.Api.Server.Services.AutoParts
{
    /// <summary>Config lecture plaque (marché cible : Maroc en premier).</summary>
    public class PlateScanOptions
    {
        public const string SectionName = "PlateScan";

        /// <summary>Code pays ISO par défaut (MA = Maroc).</summary>
        public string DefaultCountry { get; set; } = "MA";

        /// <summary>
        /// Fournisseur OCR image → texte plaque.
        /// Demo | PlateRecognizer | OpenAlpr | Custom
        /// </summary>
        public string OcrProvider { get; set; } = "Demo";

        /// <summary>Si true et OCR réel configuré : échec OCR = erreur (pas de plaque synthétique).</summary>
        public bool RequireRealOcr { get; set; } = true;

        /// <summary>Token Plate Recognizer (Authorization: Token xxx).</summary>
        public string? PlateRecognizerToken { get; set; }

        public string PlateRecognizerUrl { get; set; } =
            "https://api.platerecognizer.com/v1/plate-reader/";

        /// <summary>Régions ALPR (ex. ma, eu, fr). Vide = auto.</summary>
        public string[] PlateRecognizerRegions { get; set; } = { "ma", "eu", "fr" };

        /// <summary>Clé secrète OpenALPR Cloud (legacy).</summary>
        public string? OpenAlprSecretKey { get; set; }

        public string OpenAlprUrl { get; set; } =
            "https://api.openalpr.com/v2/recognize_bytes";

        /// <summary>Code pays OpenALPR (eu couvre bien les formats proches MA).</summary>
        public string OpenAlprCountry { get; set; } = "eu";

        /// <summary>Clé API webhook custom / Afteriize (header X-Api-Key).</summary>
        public string? ApiKey { get; set; }

        /// <summary>URL de base custom : POST {base}/plate/ocr et GET {base}/plate/{num}.</summary>
        public string? ProviderBaseUrl { get; set; }

        /// <summary>Autoriser le décodage VIN via NHTSA (fallback gratuit).</summary>
        public bool EnableNhtsaVin { get; set; } = true;

        public string NhtsaVinUrl { get; set; } =
            "https://vpic.nhtsa.dot.gov/api/vehicles/DecodeVinValuesExtended";

        /// <summary>Nombre max de pièces compatibles retournées.</summary>
        public int MaxProducts { get; set; } = 50;
    }
}
