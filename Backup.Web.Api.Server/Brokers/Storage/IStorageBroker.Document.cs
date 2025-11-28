using Backup.Web.Api.Server.Models;
using Backup.Web.Api.Server.Models.WordDocuments;
using Backup.Web.Api.Server.Models;
using Backup.Web.Api.Server.Models.Entities;
using Backup.Web.Api.Server.Models.Rols;
using Backup.Web.Api.Server.Models.Users;
using Backup.Web.Api.Server.Models.WordDocumentContents;
using Backup.Web.Api.Server.Models;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Backup.Web.Api.Server.Brokers.Storage
{
    public partial interface IStorageBroker
    {
        // Documents archive
        ValueTask<Models.Document> InsertDocumentAsync(Models.Document document);
        IQueryable<Models.Document> SelectAllDocuments();
        ValueTask<Models.Document?> SelectDocumentByIdAsync(int id);
        IQueryable<Models.Document> SearchDocuments(string query);

        // Document lines
        ValueTask InsertDocumentLinesAsync(IEnumerable<Models.DocumentLine> lines);
        IQueryable<Models.DocumentLine> SelectLinesByDocumentId(int documentId);
        ValueTask DeleteLinesByDocumentIdAsync(int documentId);

        // Stock
        ValueTask UpsertStockBatchAsync(IEnumerable<(string productKey, decimal quantityDelta)> changes);
        IQueryable<Models.StockItem> SelectAllStock();
    }
}


