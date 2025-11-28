

using System;

namespace Backup.Web.Api.Server.Models.WordDocumentContents.Exceptions
{
    public class WordDocumentContentsValidationException : Exception
    {
        public WordDocumentContentsValidationException(Exception innerException)
            : base("Invalid input, contact support.", innerException) { }
    }
}
