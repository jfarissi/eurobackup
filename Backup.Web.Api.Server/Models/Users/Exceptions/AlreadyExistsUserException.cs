




using System;
namespace Backup.Web.Api.Server.Models.Users.Exceptions
{
    public class AlreadyExistsUserException : Exception
    {
        public AlreadyExistsUserException(Exception innerException)
            : base("User with the same id already exists.", innerException) { }
    }
}
