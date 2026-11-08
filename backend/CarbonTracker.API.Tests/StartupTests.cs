using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CarbonTracker.API.Tests;

public class StartupTests
{
    private static WebApplicationFactory<Program> Factory(string environment) => new WebApplicationFactory<Program>()
        .WithWebHostBuilder(builder => builder
            .UseEnvironment(environment)
            .UseSetting("Jwt:Key", Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)))
            .UseSetting("ConnectionStrings:DuckDb", "Data Source=:memory:"));

    [Fact]
    public async Task Swagger_IsAvailable_WithoutALocalDatabase()
    {
        await using var factory = Factory("Testing");
        using var client = factory.CreateClient();

        using var ui = await client.GetAsync("/swagger/index.html");
        ui.EnsureSuccessStatusCode();
        Assert.Contains("swagger-ui", await ui.Content.ReadAsStringAsync());

        using var response = await client.GetAsync("/swagger/v1/swagger.json");
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("CarbonTracker.API", document.RootElement.GetProperty("info").GetProperty("title").GetString());
        Assert.True(document.RootElement.GetProperty("paths").TryGetProperty("/api/items", out _));
        var paths = document.RootElement.GetProperty("paths");
        var items = paths.GetProperty("/api/items").GetProperty("get");
        Assert.Equal("Bearer", items.GetProperty("security")[0].EnumerateObject().Single().Name);
        Assert.True(items.GetProperty("responses").GetProperty("401").GetProperty("content").TryGetProperty("application/problem+json", out _));
        var login = paths.GetProperty("/api/auth/login").GetProperty("post");
        Assert.False(login.TryGetProperty("security", out _));
        Assert.True(login.GetProperty("requestBody").GetProperty("content").TryGetProperty("application/json", out _));
    }

    [Theory]
    [InlineData("Development", "http://localhost:5173", true)]
    [InlineData("Development", "http://127.0.0.1:5173", true)]
    [InlineData("Development", "http://example.com", false)]
    [InlineData("Production", "http://localhost:5173", false)]
    public async Task Cors_IsLimitedToDevelopmentOrigins(string environment, string origin, bool allowed)
    {
        await using var factory = Factory(environment);
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/items");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "GET");
        request.Headers.Add("Access-Control-Request-Headers", "authorization");
        using var response = await client.SendAsync(request);
        Assert.Equal(allowed, response.Headers.TryGetValues("Access-Control-Allow-Origin", out var values));
        if (allowed) Assert.Equal(origin, values!.Single());
    }

    [Fact]
    public async Task UnauthorizedAndMissingEndpoints_ReturnProblemDetails()
    {
        await using var factory = Factory("Development");
        using var client = factory.CreateClient();
        foreach (var (path, status) in new[] { ("/api/items", 401), ("/missing", 404) })
        {
            using var response = await client.GetAsync(path);
            Assert.Equal(status, (int)response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal(status, body.RootElement.GetProperty("status").GetInt32());
        }
    }
}
