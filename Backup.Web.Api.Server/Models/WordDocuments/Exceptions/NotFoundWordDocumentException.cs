

using System;

namespace Backup.Web.Api.Server.Models.WordDocuments.Exceptions
{
    public class NotFoundWordDocumentException : Exception
    {
        public NotFoundWordDocumentException(Guid studentId)
            : base($"Couldn't find student with Id: {studentId}.") { }
    }
}
