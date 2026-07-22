namespace Backup.Web.Api.Server.Services.ErpSync
{
    public class ErpSyncOptions
    {
        public const string SectionName = "ErpSync";

        public bool Enabled { get; set; } = true;
        public string BaseUrl { get; set; } = "http://eurobrico.ddns.net:15021/ServiceMM.svc";

        /// <summary>Base HTTP des images produits (PicName), port 15022.</summary>
        public string ImageBaseUrl { get; set; } = "http://eurobrico.ddns.net:15022";

        public string CustomerId { get; set; } = "0";

        /// <summary>Heure UTC du sync planifié (18 = 20h été Belgique / 19h hiver).</summary>
        public int CronHour { get; set; } = 18;
        public int CronMinute { get; set; } = 0;

        /// <summary>
        /// Jour de la semaine du sync planifié (enum DayOfWeek : 0=Dimanche … 5=Vendredi … 6=Samedi).
        /// Null = tous les jours.
        /// </summary>
        public int? CronDayOfWeek { get; set; } = (int)System.DayOfWeek.Friday;

        public int BatchSize { get; set; } = 50;
        public int TimeoutSeconds { get; set; } = 30;
        public int RetryCount { get; set; } = 3;

        /// <summary>
        /// LocalEnrich = enrichit les produits locaux (Excel) depuis l'ERP.
        /// FullCatalog = scan complet de l'arborescence ERP.
        /// Utilisé par les syncs manuels (UI / API).
        /// </summary>
        public string SyncMode { get; set; } = "LocalEnrich";

        /// <summary>Mode du job hebdomadaire planifié (FullCatalog recommandé).</summary>
        public string ScheduledSyncMode { get; set; } = "FullCatalog";

        /// <summary>Dossier des fichiers Excel fournisseurs.</summary>
        public string ExcelProductPath { get; set; } = @"F:\EuroBricoMigration\product";
    }
}
