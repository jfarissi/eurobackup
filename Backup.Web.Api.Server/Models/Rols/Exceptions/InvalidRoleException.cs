




using System;
namespace Backup.Web.Api.Server.Models.Roles.Exceptions
{
    public class InvalidRoleException : Exception
    {
        public InvalidRoleException(string parameterName, object parameterValue)
            : base($"Invalid Role, " +
                  $"ParameterName: {parameterName}, " +
                  $"ParameterValue: {parameterValue}.")
        { }
    }
}
