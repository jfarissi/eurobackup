

using System;

namespace Backup.Web.Api.Server.Models.WordDocuments.Exceptions
{
    public class WordDocumentDependencyException : Exception
    {
        public WordDocumentDependencyException(Exception innerException)
            : base("Service dependency error occurred, contact support.", innerException) { }
    }
}
