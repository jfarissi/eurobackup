using System.Threading.Tasks;
using Backup.Web.Api.Server.Models.Catalog;

namespace Backup.Web.Api.Server.Brokers.Storage
{
    public partial interface IStorageBroker
    {
        ValueTask<ErpRapidApiKTypeCategoryCache?> SelectRapidApiKTypeCategoryCacheAsync(string kType);
        ValueTask UpsertRapidApiKTypeCategoryCacheAsync(string kType, string categoriesJson, int categoryCount);
    }
}
