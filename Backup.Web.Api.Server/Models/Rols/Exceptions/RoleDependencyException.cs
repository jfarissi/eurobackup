




using System;
namespace Backup.Web.Api.Server.Models.Roles.Exceptions
{
    public class RoleDependencyException : Exception
    {
        public RoleDependencyException(Exception innerException)
            : base("Service dependency error occurred, contact support.", innerException) { }
    }
}
