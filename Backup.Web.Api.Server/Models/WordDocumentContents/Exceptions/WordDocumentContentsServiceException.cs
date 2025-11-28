

using System;

namespace Backup.Web.Api.Server.Models.WordDocumentContents.Exceptions
{
    public class WordDocumentContentsServiceException : Exception
    {
        public WordDocumentContentsServiceException(Exception innerException)
            : base("Service error occurred, contact support.", innerException) { }
    }
}
