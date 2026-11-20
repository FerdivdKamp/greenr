using System.Collections.Concurrent;
using System.Data;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace CarbonTracker.API.Tests;

public class OperationsTests
{
    [Fact]
    public async Task Health_ReturnsHealthyForMigratedDatabase()
    {
        await using var factory = new AuthenticationFactory();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Equal("{\"status\":\"healthy\",\"database\":\"healthy\"}", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task UnavailableDatabase_IsReportedWithoutSensitiveDetails()
    {
        var logs = new RecordingLoggerProvider();
        await using var factory = FailedDatabaseFactory(logs);
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/health?secret=query-secret");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("{\"status\":\"unhealthy\",\"database\":\"unhealthy\"}", await response.Content.ReadAsStringAsync());
        Assert.Contains(logs.Messages, message => message.Contains("Database health check failed"));
        Assert.DoesNotContain(logs.Messages, message => message.Contains("sensitive-database-error") || message.Contains("query-secret"));
    }

    [Fact]
    public async Task Health_RejectsUninitializedDatabase()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("Jwt:Key", Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)));
            builder.UseSetting("ConnectionStrings:DuckDb", "Data Source=:memory:");
        });
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task UnexpectedError_AndRequestLog_DoNotExposeInputOrException()
    {
        var logs = new RecordingLoggerProvider();
        await using var factory = FailedDatabaseFactory(logs);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Example", "header-secret");
        using var response = await client.PostAsJsonAsync("/api/auth/login?secret=query-secret",
            new { identifier = "body-secret", password = "password-secret" });
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.DoesNotContain("sensitive-database-error", await response.Content.ReadAsStringAsync());
        Assert.Contains(logs.Messages, message => message.Contains("Unhandled request error"));
        Assert.Contains(logs.Messages, message => message.Contains("api/auth/login") && message.Contains("500"));
        Assert.DoesNotContain(logs.Messages, message => new[] { "sensitive-database-error", "query-secret", "body-secret", "password-secret", "header-secret" }.Any(message.Contains));
    }

    private static WebApplicationFactory<Program> FailedDatabaseFactory(RecordingLoggerProvider logs) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("Jwt:Key", Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IDbConnection>();
                services.AddScoped<IDbConnection>(_ => throw new InvalidOperationException("sensitive-database-error"));
            });
            builder.ConfigureLogging(logging => logging.AddProvider(logs));
        });

    private sealed class RecordingLoggerProvider : ILoggerProvider
    {
        public ConcurrentBag<string> Messages { get; } = new();
        public ILogger CreateLogger(string categoryName) => new Recorder(Messages);
        public void Dispose() { }

        private sealed class Recorder(ConcurrentBag<string> messages) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
                messages.Add(formatter(state, exception) + exception);
        }
    }
}
