

using System;

namespace Backup.Web.Api.Server.Models.WordDocuments.Exceptions
{
    public class NullWordDocumentException : Exception
    {
        public NullWordDocumentException() : base("The student is null.") { }
    }
}
