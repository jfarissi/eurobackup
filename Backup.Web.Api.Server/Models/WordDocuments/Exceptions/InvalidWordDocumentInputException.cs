

using System;

namespace Backup.Web.Api.Server.Models.WordDocuments.Exceptions
{
    public class InvalidWordDocumentInputException : Exception
    {
        public InvalidWordDocumentInputException(string parameterName, object parameterValue)
            : base($"Invalid Document, " +
                  $"ParameterName: {parameterName}, " +
                  $"ParameterValue: {parameterValue}.")
        { }
    }
}
