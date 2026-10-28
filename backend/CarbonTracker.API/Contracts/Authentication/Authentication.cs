using System.ComponentModel.DataAnnotations;
using System.Text;

namespace CarbonTracker.API.Contracts.Authentication;

public record RegisterRequest(
    [Required, StringLength(20), RegularExpression(@"[a-zA-Z0-9_.-]+") ] string Username,
    [Required, EmailAddress, StringLength(254)] string Email,
    [Required, StringLength(72, MinimumLength = 8), PasswordByteLength] string Password);
public record LoginRequest(
    [Required, StringLength(254)] string Identifier,
    [Required, PasswordByteLength] string Password);
public record TokenResponse(string AccessToken, string RefreshToken);
public record RefreshTokenRequest([Required, StringLength(256)] string RefreshToken);
public record RequestPasswordResetRequest([Required, EmailAddress, StringLength(254)] string Email);
public record ResetPasswordRequest(
    [Required, StringLength(256)] string Token,
    [Required, StringLength(72, MinimumLength = 8), PasswordByteLength] string NewPassword);

// BCrypt only considers the first 72 bytes; reject longer UTF-8 input.
public sealed class PasswordByteLengthAttribute : ValidationAttribute
{
    public PasswordByteLengthAttribute() : base("Password must not exceed 72 UTF-8 bytes.") { }
    public override bool IsValid(object? value) =>
        value is null || value is string password && Encoding.UTF8.GetByteCount(password) <= 72;
}
