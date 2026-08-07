namespace Backup.Web.Api.Server.Services.Modules
{
    /// <summary>Codes modules connus (extensibles sans migration).</summary>
    public static class ModuleCodes
    {
        public const string Core = "core";
        /// <summary>Sync catalogue webservice EuroBrico / Changements ERP.</summary>
        public const string ErpCatalogSync = "erp_catalog_sync";
        /// <summary>Fitment véhicule, OEM, import RapidAPI / CarApi.</summary>
        public const string AutoParts = "auto_parts";
        /// <summary>Attributs quincaillerie (filetage, DIN…).</summary>
        public const string Hardware = "hardware";
        /// <summary>Garanties / SAV électroménager.</summary>
        public const string Appliances = "appliances";

        public static string DisplayName(string code) => code switch
        {
            Core => "Core ERP",
            ErpCatalogSync => "Sync catalogue ERP",
            AutoParts => "Pièces auto",
            Hardware => "Quincaillerie",
            Appliances => "Électroménager",
            _ => code
        };
    }
}
