namespace CarbonTracker.API.Contracts.Authentication
{
    public record RegisterRequest(string Username, string Email, string Password);
    public record LoginRequest(string Identifier, string Password);
    public record TokenResponse(string AccessToken, string RefreshToken);
    public record RequestPasswordResetRequest(string Email);
    public record ResetPasswordRequest(string Token, string NewPassword);
}
