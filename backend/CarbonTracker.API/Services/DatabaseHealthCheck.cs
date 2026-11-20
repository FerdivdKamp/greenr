using System.Data;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CarbonTracker.API.Services;

public sealed class DatabaseHealthCheck(IServiceScopeFactory scopeFactory, ILogger<DatabaseHealthCheck> logger) : IHealthCheck
{
    public const string EndpointPath = "/health";

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IDbConnection>();
            using var command = db.CreateCommand();
            command.CommandText = "SELECT user_id FROM users LIMIT 0";
            using var reader = command.ExecuteReader();
            return Task.FromResult(HealthCheckResult.Healthy());
        }
        catch (Exception)
        {
            logger.LogWarning("Database health check failed");
            return Task.FromResult(HealthCheckResult.Unhealthy());
        }
    }
}
