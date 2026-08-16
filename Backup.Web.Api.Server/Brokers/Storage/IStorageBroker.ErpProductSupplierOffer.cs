using System.Linq;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Models.Catalog;

namespace Backup.Web.Api.Server.Brokers.Storage
{
    public partial interface IStorageBroker
    {
        IQueryable<ErpProductSupplierOffer> SelectAllErpProductSupplierOffers();
        ValueTask<ErpProductSupplierOffer> UpsertErpProductSupplierOfferAsync(ErpProductSupplierOffer row);
    }
}
