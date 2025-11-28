




using System;
namespace Backup.Web.Api.Server.Models.Roles.Exceptions
{
    public class RoleValidationException : Exception
    {
        public RoleValidationException(Exception innerException)
            : base("Invalid input, contact support.", innerException) { }
    }
}
