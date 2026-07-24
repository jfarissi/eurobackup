namespace Backup.Web.Api.Server.Models.AppSettings;

public class AuthSeedOptions
{
    public const string SectionName = "AuthSeed";

    public bool Enabled { get; set; } = true;
    public string Email { get; set; } = "admin@eurobrico.local";
    public string Password { get; set; } = "Admin123!";
    public string Role { get; set; } = "Admin";
    public string? Name { get; set; } = "Admin";
    public string? FamilyName { get; set; } = "EuroBrico";
}
