using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using BitNest.Models;
using Microsoft.IdentityModel.Tokens;

namespace BitNest.Services;

public class JwtTokenService
{
    private readonly IConfiguration configuration;

    public JwtTokenService(IConfiguration configuration)
    {
        this.configuration = configuration;
    }

    public string CreateAccessToken(User user)
    {
        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken(
            issuer: GetIssuer(),
            audience: GetAudience(),
            claims:
            [
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.NormalizedUsername),
                new Claim("admin", user.IsAdmin ? "true" : "false")
            ],
            notBefore: now,
            expires: now.AddMinutes(GetAccessTokenMinutes()),
            signingCredentials: new SigningCredentials(GetSigningKey(), SecurityAlgorithms.HmacSha256)
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshSecret()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
    }

    public string HashRefreshSecret(string refreshSecret)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(refreshSecret));
        return Convert.ToBase64String(bytes);
    }

    public string GetIssuer() => configuration.GetValue<string>("Auth:Issuer") ?? "bitnest";
    public string GetAudience() => configuration.GetValue<string>("Auth:Audience") ?? "bitnest-client";
    public int GetAccessTokenMinutes() => configuration.GetValue("Auth:AccessTokenMinutes", 15);

    public SymmetricSecurityKey GetSigningKey()
    {
        var key = configuration.GetValue<string>("Auth:SigningKey")
                  ?? "local-dev-signing-key-change-me-please-123456";
        return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
    }
}
