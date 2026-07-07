namespace Backup.Web.Api.Server.Services.Pricing
{
    public class ErpPricingOptions
    {
        public string BaseUrl { get; set; } =
            "http://eurobrico.ddns.net:15021/ServiceMM.svc/getProductStockByReference";

        /// <summary>
        /// Optional override when BaseUrl does not end with getProductStockByReference.
        /// </summary>
        public string? ProductsByReferenceUrl { get; set; }
    }
}
