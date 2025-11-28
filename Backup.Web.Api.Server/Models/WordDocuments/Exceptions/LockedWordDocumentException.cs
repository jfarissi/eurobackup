

using System;

namespace Backup.Web.Api.Server.Models.WordDocuments.Exceptions
{
    public class LockedWordDocumentException : Exception
    {
        public LockedWordDocumentException(Exception innerException)
            : base("Locked student record exception, please try again later.", innerException) { }
    }
}
