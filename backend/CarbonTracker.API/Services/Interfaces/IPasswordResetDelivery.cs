namespace CarbonTracker.API.Services.Interfaces;

// Implementations must deliver only to the stored account email, keep secrets out
// of telemetry, and enqueue promptly. A real provider can replace this adapter.
public interface IPasswordResetDelivery
{
    Task DeliverAsync(string email, string token, DateTime expiresAtUtc);
}
