using Backup.Web.Api.Server.Models.WordDocuments;
using Backup.Web.Api.Server.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Backup.Web.Api.Server.Brokers.Storage
{
    public partial interface IStorageBroker
    {
            public ValueTask<WordDocument> InsertWordDocumentAsync(WordDocument WordDocument);
            public IQueryable<WordDocument> SelectAllWordDocuments();
            public IQueryable<WordDocument> SelectAllWordDocuments(Pager movieQuery);
            public ValueTask<WordDocument> SelectWordDocumentByIdAsync(Guid WordDocumentId);
            public ValueTask<WordDocument> UpdateWordDocumentAsync(WordDocument WordDocument);
            public ValueTask<WordDocument> DeleteWordDocumentAsync(WordDocument WordDocument);
        
    }
}
