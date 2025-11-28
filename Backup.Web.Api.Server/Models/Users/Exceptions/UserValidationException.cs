




using System;
namespace Backup.Web.Api.Server.Models.Users.Exceptions
{
    public class UserValidationException : Exception
    {
        public UserValidationException(Exception innerException)
            : base("Invalid input, contact support.", innerException) { }
    }
}
