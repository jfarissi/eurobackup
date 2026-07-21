namespace Backup.Web.Api.Server.Services.ErpSync
{
    public class ErpCatalogSyncFilter
    {
        public string? MainTypeId { get; set; }
        public string? TypeId { get; set; }
        public string? SubTypeId { get; set; }
        public string? Brand { get; set; }

        public bool HasAnyFilter =>
            !string.IsNullOrWhiteSpace(MainTypeId)
            || !string.IsNullOrWhiteSpace(TypeId)
            || !string.IsNullOrWhiteSpace(SubTypeId)
            || !string.IsNullOrWhiteSpace(Brand);
    }
}
