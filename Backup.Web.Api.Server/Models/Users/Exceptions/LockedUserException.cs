




using System;
namespace Backup.Web.Api.Server.Models.Users.Exceptions
{
    public class LockedUserException : Exception
    {
        public LockedUserException(Exception innerException)
            : base("Locked user record exception, please try again later.", innerException) { }
    }
}
