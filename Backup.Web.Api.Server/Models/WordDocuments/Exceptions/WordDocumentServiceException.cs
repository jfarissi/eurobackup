

using System;

namespace Backup.Web.Api.Server.Models.WordDocuments.Exceptions
{
    public class WordDocumentServiceException : Exception
    {
        public WordDocumentServiceException(Exception innerException)
            : base("Service error occurred, contact support.", innerException) { }
    }
}
