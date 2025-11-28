




using System;
namespace Backup.Web.Api.Server.Models.Roles.Exceptions
{
    public class RoleServiceException : Exception
    {
        public RoleServiceException(Exception innerException)
            : base("Roles Service error occurred, contact support.", innerException) { }
    }
}
