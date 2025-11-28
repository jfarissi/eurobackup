using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Backup.Web.Api.Server.Models.WordDocumentContents;

namespace Backup.Web.Api.Server.Services.WordDocumentContents
{
    public interface IWordDocumentContentsService
    {
        ValueTask<WordDocumentContent> RegisterWordDocumentContentAsync(WordDocumentContent WordDocumentContent);
        ValueTask<WordDocumentContent> RetrieveWordDocumentContentByIdAsync(Guid WordDocumentContentId);
        ValueTask<WordDocumentContent> ModifyWordDocumentContentAsync(WordDocumentContent WordDocumentContent);
        ValueTask<WordDocumentContent> DeleteWordDocumentContentAsync(Guid WordDocumentContentId);
        IQueryable<WordDocumentContent> RetrieveAllWordDocumentContents();
    }
}
