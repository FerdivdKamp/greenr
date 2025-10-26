namespace CarbonTracker.API.Services.Interfaces
{
    public interface ITokenService
    {
        string CreateAccessToken(Guid userId, string username);
        string CreateRefreshToken();
        int RefreshDays();
    }
}
