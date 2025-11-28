

using System;

namespace Backup.Web.Api.Server.Models.WordDocumentContents.Exceptions
{
    public class AlreadyExistsWordDocumentContentsException : Exception
    {
        public AlreadyExistsWordDocumentContentsException(Exception innerException)
            : base("Student with the same id already exists.", innerException) { }
    }
}
