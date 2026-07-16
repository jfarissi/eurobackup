namespace Backup.Web.Api.Server.Services.ErpSync
{
    public class ErpSyncOptions
    {
        public const string SectionName = "ErpSync";

        public bool Enabled { get; set; } = true;
        public string BaseUrl { get; set; } = "http://eurobrico.ddns.net:15021/ServiceMM.svc";
        public string CustomerId { get; set; } = "0";
        public int CronHour { get; set; } = 2;
        public int CronMinute { get; set; } = 0;
        public int BatchSize { get; set; } = 50;
        public int TimeoutSeconds { get; set; } = 30;
        public int RetryCount { get; set; } = 3;

        /// <summary>
        /// LocalEnrich = enrichit les produits locaux (Excel) depuis l'ERP.
        /// FullCatalog = scan complet de l'arborescence ERP.
        /// </summary>
        public string SyncMode { get; set; } = "LocalEnrich";

        /// <summary>Dossier des fichiers Excel fournisseurs.</summary>
        public string ExcelProductPath { get; set; } = @"F:\EuroBricoMigration\product";
    }
}
