namespace Backup.Web.Api.Server.Models.Users;

using Backup.Web.Api.Server.Models.Entities;

public class AuthenticateResponse
{
    public Guid Id { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Username { get; set; }
    public string Role { get; set; }
    public string Token { get; set; }

    public bool IsAdmin   { get; set; }
    public AuthenticateResponse(User user, string token)
    {
        Id = user.Id;
        FirstName = user.Name;
        LastName = user.FamilyName;
        Username = user.UserName;
        Role = user.Role.Name;
        Token = token;
        IsAdmin = user.IsAdmin;
    }
}