using CarbonTracker.API.Services.Interfaces;

namespace CarbonTracker.API.Services;

public sealed class DevelopmentPasswordResetDelivery(
    IHostEnvironment environment,
    ILogger<DevelopmentPasswordResetDelivery> logger) : IPasswordResetDelivery
{
    public Task DeliverAsync(string email, string token, DateTime expiresAtUtc)
    {
        // This adapter must never leak reset credentials to staging/production logs.
        if (environment.IsDevelopment())
            logger.LogInformation("LOCAL DEVELOPMENT password reset for {Email}: token={Token}; expires UTC={Expires}",
                email, token, expiresAtUtc);
        return Task.CompletedTask;
    }
}
