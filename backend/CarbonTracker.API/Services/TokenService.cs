using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using CarbonTracker.API.Services.Interfaces;

namespace CarbonTracker.API.Services;

public class TokenService : ITokenService
{
    private readonly IConfiguration _cfg;
    private readonly SymmetricSecurityKey _key;

    public TokenService(IConfiguration cfg)
    {
        _cfg = cfg;
        var secret = _cfg["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key missing");
        _key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
    }

    public string CreateAccessToken(Guid userId, string username)
    {
        var creds = new SigningCredentials(_key, SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Name, username)
        };

        var minutes = int.Parse(_cfg["Jwt:AccessTokenMinutes"] ?? "15");
        var token = new JwtSecurityToken(
            issuer: _cfg["Jwt:Issuer"] ?? "greenr",
            audience: _cfg["Jwt:Audience"] ?? "greenr",
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(minutes),
            signingCredentials: creds
        );
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string CreateRefreshToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

    public int RefreshDays() =>
        int.Parse(_cfg["Jwt:RefreshTokenDays"] ?? "14");
}
