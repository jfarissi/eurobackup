




using System;
namespace Backup.Web.Api.Server.Models.Roles.Exceptions
{
    public class AlreadyExistsRoleException : Exception
    {
        public AlreadyExistsRoleException(Exception innerException)
            : base("Role with the same id already exists.", innerException) { }
    }
}
