using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ExpenseManager.Api.Domain;
using Microsoft.IdentityModel.Tokens;

namespace ExpenseManager.Api.Infrastructure;

public interface IJwtTokenService
{
    string Create(User user);
}

public static class JwtClaimNames
{
    public const string TokenVersion = "token_version";
}

public sealed class JwtTokenService(IConfiguration configuration) : IJwtTokenService
{
    public string Create(User user)
    {
        var secret = configuration["Jwt:Secret"]
            ?? throw new InvalidOperationException("Jwt__Secret is required.");
        if (Encoding.UTF8.GetByteCount(secret) < 32)
            throw new InvalidOperationException("Jwt__Secret must contain at least 32 bytes.");

        var expiresMinutes = configuration.GetValue("Jwt:ExpiresMinutes", 15);
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Name),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(JwtClaimNames.TokenVersion, user.TokenVersion.ToString())
        };
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: configuration["Jwt:Issuer"] ?? "ExpenseManager",
            audience: configuration["Jwt:Audience"] ?? "ExpenseManager.App",
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expiresMinutes),
            signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
