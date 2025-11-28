

using System;

namespace Backup.Web.Api.Server.Models.WordDocumentContents.Exceptions
{
    public class NullWordDocumentContentsException : Exception
    {
        public NullWordDocumentContentsException() : base("The student is null.") { }
    }
}
