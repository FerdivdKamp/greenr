namespace CarbonTracker.API.Services.Interfaces
{
    public interface IUsersService
    {
        Task CreateUserAsync(string username, string email, string password);

        // Returns (UserId, Username) for an identifier (username OR email), or null if not found
        Task<(Guid UserId, string Username)?> FindByIdentifierAsync(string identifier);

        // Verifies the password for the given userId
        Task<bool> VerifyPasswordAsync(Guid userId, string password);

        // Creates & persists refresh token; returns access + refresh
        Task<(string AccessToken, string RefreshToken)> IssueTokensAsync(Guid userId, string username);

        // Creates a reset token (generic even if email not found)
        Task<string> CreatePasswordResetAsync(string email);

        // Resets password using token
        Task ResetPasswordAsync(string token, string newPassword);
    }
}
