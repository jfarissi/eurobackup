




using System;
namespace Backup.Web.Api.Server.Models.Roles.Exceptions
{
    public class LockedRoleException : Exception
    {
        public LockedRoleException(Exception innerException)
            : base("Locked Role record exception, please try again later.", innerException) { }
    }
}
