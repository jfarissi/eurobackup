




using System;
namespace Backup.Web.Api.Server.Models.Users.Exceptions
{
    public class UserServiceException : Exception
    {
        public UserServiceException(Exception innerException)
            : base("User Service error occurred, contact support.", innerException) { }
    }
}
