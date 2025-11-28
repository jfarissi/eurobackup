

using System;

namespace Backup.Web.Api.Server.Models.WordDocumentContents.Exceptions
{
    public class InvalidWordDocumentContentsInputException : Exception
    {
        public InvalidWordDocumentContentsInputException(string parameterName, object parameterValue)
            : base($"Invalid Student, " +
                  $"ParameterName: {parameterName}, " +
                  $"ParameterValue: {parameterValue}.")
        { }
    }
}
