

using System;

namespace Backup.Web.Api.Server.Models.WordDocuments.Exceptions
{
    public class AlreadyExistsWordDocumentException : Exception
    {
        public AlreadyExistsWordDocumentException(Exception innerException)
            : base("Document Word with the same id already exists.", innerException) { }
    }
}
