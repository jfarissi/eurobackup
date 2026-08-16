using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Backup.Web.Api.Server.Models.AppSettings;
using Backup.Web.Api.Server.Models.Users;
using Backup.Web.Api.Server.Services.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Backup.Web.Api.Server.Authorization;

public interface IJwtUtils
{
    string GenerateJwtToken(User user, string? companyId = null, IEnumerable<string>? permissions = null);
    Guid? ValidateJwtToken(string token);
}

public class JwtUtils : IJwtUtils
{
    private readonly IConfiguration _configuration;

    public JwtUtils(IOptions<AppSettings> appSettings, IConfiguration configuration)
    {
        _ = appSettings;
        _configuration = configuration;
    }

    public string GenerateJwtToken(User user, string? companyId = null, IEnumerable<string>? permissions = null)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(GetJwtKey());
        var roleName = user.Role?.Name
            ?? (user.IsAdmin ? "Admin" : "User");

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new("id", user.Id.ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Role, roleName)
        };

        var effectiveCompanyId = !string.IsNullOrWhiteSpace(companyId) ? companyId : user.CompanyId;
        if (!string.IsNullOrWhiteSpace(effectiveCompanyId))
            claims.Add(new Claim("CompanyId", effectiveCompanyId));
        if (user.CustomerId.HasValue)
            claims.Add(new Claim("CustomerId", user.CustomerId.Value.ToString()));

        if (!string.IsNullOrWhiteSpace(user.Email))
            claims.Add(new Claim(JwtRegisteredClaimNames.Email, user.Email));
        if (!string.IsNullOrWhiteSpace(user.UserName))
            claims.Add(new Claim(ClaimTypes.Name, user.UserName!));

        var displayName = $"{user.Name} {user.FamilyName}".Trim();
        if (!string.IsNullOrWhiteSpace(displayName))
            claims.Add(new Claim("display_name", displayName));
        else if (!string.IsNullOrWhiteSpace(user.UserName))
            claims.Add(new Claim("display_name", user.UserName!));

        if (permissions != null)
            claims.AddRange(PermissionResolver.ToPermissionClaims(permissions));

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddDays(7),
            Issuer = _configuration["Jwt:Issuer"],
            Audience = _configuration["Jwt:Audience"],
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(key),
                SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    public Guid? ValidateJwtToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;

        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(GetJwtKey());
        try
        {
            tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = true,
                ValidIssuer = _configuration["Jwt:Issuer"],
                ValidateAudience = true,
                ValidAudience = _configuration["Jwt:Audience"],
                ClockSkew = TimeSpan.Zero
            }, out SecurityToken validatedToken);

            var jwtToken = (JwtSecurityToken)validatedToken;
            var idClaim = jwtToken.Claims.FirstOrDefault(x => x.Type == "id")
                ?? jwtToken.Claims.FirstOrDefault(x => x.Type == JwtRegisteredClaimNames.Sub)
                ?? jwtToken.Claims.FirstOrDefault(x => x.Type == ClaimTypes.NameIdentifier);

            return idClaim != null ? Guid.Parse(idClaim.Value) : null;
        }
        catch
        {
            return null;
        }
    }

    private string GetJwtKey()
    {
        var key = _configuration["Jwt:Key"];
        if (string.IsNullOrWhiteSpace(key))
            throw new InvalidOperationException("Jwt:Key is not configured");
        return key;
    }
}
