namespace Backup.Web.Api.Server.Models.AppSettings;

public class AuthSeedOptions
{
    public const string SectionName = "AuthSeed";

    public bool Enabled { get; set; } = true;
    public string Email { get; set; } = "admin@demo.local";
    public string Password { get; set; } = "Admin123!";
    public string Role { get; set; } = "Admin";
    public string? Name { get; set; } = "Admin";
    public string? FamilyName { get; set; } = "Demo";
    /// <summary>When true, always reset the seed user password to <see cref="Password"/> on startup.</summary>
    public bool ForceResetPassword { get; set; }
}
