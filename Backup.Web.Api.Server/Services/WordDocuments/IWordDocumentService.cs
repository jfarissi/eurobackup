using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Models.WordDocuments;
using Backup.Web.Api.Server.Models;

namespace Backup.Web.Api.Server.Services.WordDocuments
{
    public interface IWordDocumentService
    {
        ValueTask<WordDocument> RegisterWordDocumentAsync(WordDocument wordDocument);
        ValueTask<WordDocument> RetrieveWordDocumentByIdAsync(Guid WordDocumentId);
        ValueTask<WordDocument> ModifyWordDocumentAsync(WordDocument WordDocument);
        ValueTask<WordDocument> DeleteWordDocumentAsync(Guid WordDocumentId);
        IQueryable<WordDocument> RetrieveAllWordDocuments();
        IQueryable<WordDocument> RetrieveAllWordDocuments(Pager queryObj);
    }
}
