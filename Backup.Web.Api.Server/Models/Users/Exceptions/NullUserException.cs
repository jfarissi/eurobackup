




using System;
namespace Backup.Web.Api.Server.Models.Users.Exceptions
{
    public class NullUserException : Exception
    {
        public NullUserException() : base("The user is null.") { }
    }
}
