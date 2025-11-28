




using System;
namespace Backup.Web.Api.Server.Models.Roles.Exceptions
{
    public class NotFoundRoleException : Exception
    {
        public NotFoundRoleException(Guid RoleId)
            : base($"Couldn't find Role with Id: {RoleId}.") { }
    }
}
