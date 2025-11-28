

using System;

namespace Backup.Web.Api.Server.Models.WordDocuments.Exceptions
{
    public class WordDocumentValidationException : Exception
    {
        public WordDocumentValidationException(Exception innerException)
            : base("Invalid input, contact support.", innerException) { }
    }
}
