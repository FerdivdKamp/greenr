using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
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
        _key = JwtConfiguration.SigningKey(cfg);
    }

    public string CreateAccessToken(Guid userId, string username)
    {
        var creds = new SigningCredentials(_key, SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
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
