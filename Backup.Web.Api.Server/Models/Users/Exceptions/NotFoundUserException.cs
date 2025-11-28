




using System;
namespace Backup.Web.Api.Server.Models.Users.Exceptions
{
    public class NotFoundUserException : Exception
    {
        public NotFoundUserException(Guid userId)
            : base($"Couldn't find user with Id: {userId}.") { }
    }
}
