namespace Backup.Web.Api.Server.Services.Pricing
{
    public class ErpPricingOptions
    {
        public string BaseUrl { get; set; } =
            "http://eurobrico.ddns.net:15021/ServiceMM.svc/getProductStockByReference";
    }
}
