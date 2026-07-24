using System.Text.Json.Serialization;
using Backup.Web.Api.Server.Models.Users;

namespace Backup.Web.Api.Server.Models.Users;

public class AuthenticateResponse
{
    public Guid Id { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Username { get; set; }
    public string Role { get; set; } = "User";
    public string Token { get; set; } = string.Empty;
    public bool IsAdmin { get; set; }

    [JsonConstructor]
    public AuthenticateResponse()
    {
    }

    public AuthenticateResponse(User user, string token)
    {
        Id = user.Id;
        FirstName = user.Name;
        LastName = user.FamilyName;
        Username = user.Email ?? user.UserName;
        Role = user.Role?.Name ?? (user.IsAdmin ? "Admin" : "User");
        Token = token;
        IsAdmin = user.IsAdmin || string.Equals(Role, "Admin", StringComparison.OrdinalIgnoreCase);
    }
}
