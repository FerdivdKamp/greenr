namespace CarbonTracker.API.Tests.Services;

using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using CarbonTracker.API.Services;

public class TokenServiceTests
{
    private static IConfiguration BuildConfig(Dictionary<string, string?> overrides = null)
    {
        var dict = new Dictionary<string, string?>
        {
            ["Jwt:Key"] = "super-secret-test-key-1234567890",
            ["Jwt:Issuer"] = "greenr-issuer",
            ["Jwt:Audience"] = "greenr-audience",
            ["Jwt:AccessTokenMinutes"] = "5",
            ["Jwt:RefreshTokenDays"] = "30",
        };

        if (overrides != null)
            foreach (var kv in overrides) dict[kv.Key] = kv.Value;

        return new ConfigurationBuilder().AddInMemoryCollection(dict!).Build();
    }

    private static TokenValidationParameters BuildValidationParams(IConfiguration cfg)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(cfg["Jwt:Key"]!));
        return new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = cfg["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = cfg["Jwt:Audience"],
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = key
        };
    }

    [Fact]
    public void Ctor_Throws_When_Key_Missing()
    {
        var cfg = BuildConfig(new() { ["Jwt:Key"] = null });
        Action act = () => new TokenService(cfg);
        act.Should().Throw<InvalidOperationException>()
           .WithMessage("*Jwt:Key missing*");
    }

    [Fact]
    public void CreateAccessToken_ContainsClaims_And_ValidSignature()
    {
        var cfg = BuildConfig();
        var svc = new TokenService(cfg);

        var userId = Guid.NewGuid();
        var tokenString = svc.CreateAccessToken(userId, "alice");

        // Validate token
        var handler = new JwtSecurityTokenHandler();
        var parms = BuildValidationParams(cfg);
        var principal = handler.ValidateToken(tokenString, parms, out var validatedToken);

        // Signature algorithm check
        ((JwtSecurityToken)validatedToken).Header.Alg.Should().Be(SecurityAlgorithms.HmacSha256);

        // Claims check
        principal.FindFirst(ClaimTypes.NameIdentifier)!.Value.Should().Be(userId.ToString());
        principal.FindFirst(ClaimTypes.Name)!.Value.Should().Be("alice");
    }

    [Fact]
    public void CreateAccessToken_HasExpectedIssuerAudienceAndExpiry()
    {
        var cfg = BuildConfig(new() { ["Jwt:AccessTokenMinutes"] = "2" });
        var svc = new TokenService(cfg);

        var before = DateTime.UtcNow;
        var tokenString = svc.CreateAccessToken(Guid.NewGuid(), "bob");
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(tokenString);

        jwt.Issuer.Should().Be("greenr-issuer");
        jwt.Audiences.Should().ContainSingle(a => a == "greenr-audience");

        // Expiry within tolerance (a couple seconds for execution time)
        var expectedMin = before.AddMinutes(2).AddSeconds(-3);
        var expectedMax = before.AddMinutes(2).AddSeconds(3);
        jwt.ValidTo.Should().BeOnOrAfter(expectedMin);
        jwt.ValidTo.Should().BeOnOrBefore(expectedMax);
    }

    [Fact]
    public void CreateAccessToken_UsesDefaultMinutes_WhenMissing()
    {
        var cfg = BuildConfig(new() { ["Jwt:AccessTokenMinutes"] = null }); // default is "15"
        var svc = new TokenService(cfg);

        var before = DateTime.UtcNow;
        var tokenString = svc.CreateAccessToken(Guid.NewGuid(), "carl");
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(tokenString);

        var expectedMin = before.AddMinutes(15).AddSeconds(-3);
        var expectedMax = before.AddMinutes(15).AddSeconds(3);
        jwt.ValidTo.Should().BeOnOrAfter(expectedMin);
        jwt.ValidTo.Should().BeOnOrBefore(expectedMax);
    }

    [Fact]
    public void CreateAccessToken_Throws_When_AccessTokenMinutes_NotAnInt()
    {
        var cfg = BuildConfig(new() { ["Jwt:AccessTokenMinutes"] = "not-an-int" });
        var svc = new TokenService(cfg);

        Action act = () => svc.CreateAccessToken(Guid.NewGuid(), "dora");
        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void CreateRefreshToken_IsBase64_64Bytes_And_Random()
    {
        var cfg = BuildConfig();
        var svc = new TokenService(cfg);

        var t1 = svc.CreateRefreshToken();
        var t2 = svc.CreateRefreshToken();

        // Base64 decodes to 64 bytes
        var b1 = Convert.FromBase64String(t1);
        var b2 = Convert.FromBase64String(t2);
        b1.Length.Should().Be(64);
        b2.Length.Should().Be(64);

        // Extremely likely to differ
        t1.Should().NotBe(t2);
    }

    [Fact]
    public void RefreshDays_UsesValue_WhenPresent_ElseDefault14()
    {
        var cfg30 = BuildConfig(new() { ["Jwt:RefreshTokenDays"] = "30" });
        new TokenService(cfg30).RefreshDays().Should().Be(30);

        var cfgDefault = BuildConfig(new() { ["Jwt:RefreshTokenDays"] = null });
        new TokenService(cfgDefault).RefreshDays().Should().Be(14);
    }
}
