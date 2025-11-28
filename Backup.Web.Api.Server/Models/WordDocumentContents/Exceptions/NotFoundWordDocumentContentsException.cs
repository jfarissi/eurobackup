

using System;

namespace Backup.Web.Api.Server.Models.WordDocumentContents.Exceptions
{
    public class NotFoundWordDocumentContentsException : Exception
    {
        public NotFoundWordDocumentContentsException(Guid studentId)
            : base($"Couldn't find student with Id: {studentId}.") { }
    }
}
