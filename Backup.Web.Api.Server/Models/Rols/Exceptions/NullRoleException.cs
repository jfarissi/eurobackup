




using System;
namespace Backup.Web.Api.Server.Models.Roles.Exceptions
{
    public class NullRoleException : Exception
    {
        public NullRoleException() : base("The Role is null.") { }
    }
}
