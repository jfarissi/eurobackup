

using System;

namespace Backup.Web.Api.Server.Models.WordDocumentContents.Exceptions
{
    public class LockedWordDocumentContentsException : Exception
    {
        public LockedWordDocumentContentsException(Exception innerException)
            : base("Locked student record exception, please try again later.", innerException) { }
    }
}
