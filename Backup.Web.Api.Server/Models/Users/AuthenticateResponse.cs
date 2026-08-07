using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Backup.Web.Api.Server.Models.Users;

namespace Backup.Web.Api.Server.Models.Users;

public class CompanySummary
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool EnableErpCatalogSync { get; set; }
}

public class AuthenticateResponse
{
    public Guid Id { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Username { get; set; }
    public string Role { get; set; } = "User";
    public string Token { get; set; } = string.Empty;
    public bool IsAdmin { get; set; }
    public string? CompanyId { get; set; }
    public string? CompanyName { get; set; }
    public List<CompanySummary> Companies { get; set; } = new();
    public List<string> Permissions { get; set; } = new();

    [JsonConstructor]
    public AuthenticateResponse()
    {
    }

    public AuthenticateResponse(
        User user,
        string token,
        string? companyId = null,
        string? companyName = null,
        List<CompanySummary>? companies = null,
        List<string>? permissions = null)
    {
        Id = user.Id;
        FirstName = user.Name;
        LastName = user.FamilyName;
        Username = user.Email ?? user.UserName;
        Role = user.Role?.Name ?? (user.IsAdmin ? "Admin" : "User");
        Token = token;
        IsAdmin = user.IsAdmin || string.Equals(Role, "Admin", StringComparison.OrdinalIgnoreCase);
        CompanyId = companyId ?? user.CompanyId;
        CompanyName = companyName;
        Companies = companies ?? new List<CompanySummary>();
        Permissions = permissions ?? new List<string>();
    }
}
