using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace CarbonTracker.API.Services;

public static class JwtConfiguration
{
    public static SymmetricSecurityKey SigningKey(IConfiguration configuration)
    {
        var secret = configuration["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key missing. Configure user secrets or Jwt__Key.");
        if (string.IsNullOrWhiteSpace(secret) || Encoding.UTF8.GetByteCount(secret) < 32)
            throw new InvalidOperationException("Jwt:Key must contain at least 32 UTF-8 bytes. Configure user secrets or Jwt__Key.");
        return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
    }
}
