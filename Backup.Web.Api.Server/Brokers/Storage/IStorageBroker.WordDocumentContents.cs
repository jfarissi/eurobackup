using Backup.Web.Api.Server.Models.WordDocumentContents;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Backup.Web.Api.Server.Brokers.Storage
{
    public partial interface IStorageBroker
    {
            public ValueTask<WordDocumentContent> InsertWordDocumentContentsAsync(WordDocumentContent WordDocumentContents);
            public IQueryable<WordDocumentContent> SelectAllWordDocumentContents();
            public ValueTask<WordDocumentContent> SelectWordDocumentContentsByIdAsync(Guid WordDocumentContentsId);
            public ValueTask<WordDocumentContent> UpdateWordDocumentContentsAsync(WordDocumentContent WordDocumentContents);
            public ValueTask<WordDocumentContent> DeleteWordDocumentContentsAsync(WordDocumentContent WordDocumentContents);
        
    }
}
