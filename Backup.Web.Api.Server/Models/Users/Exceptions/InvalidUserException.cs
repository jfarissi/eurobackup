




using System;
namespace Backup.Web.Api.Server.Models.Users.Exceptions
{
    public class InvalidUserException : Exception
    {
        public InvalidUserException(string parameterName, object parameterValue)
            : base($"Invalid User, " +
                  $"ParameterName: {parameterName}, " +
                  $"ParameterValue: {parameterValue}.")
        { }
    }
}
