using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CarbonTracker.API.Tests;

public class StartupTests
{
    [Fact]
    public async Task Swagger_IsAvailable_WithoutALocalDatabase()
    {
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder
                .UseEnvironment("Testing")
                .UseSetting("Jwt:Key", Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)))
                .UseSetting("ConnectionStrings:DuckDb", "Data Source=:memory:"));
        using var client = factory.CreateClient();

        using var ui = await client.GetAsync("/swagger/index.html");
        ui.EnsureSuccessStatusCode();
        Assert.Contains("swagger-ui", await ui.Content.ReadAsStringAsync());

        using var response = await client.GetAsync("/swagger/v1/swagger.json");
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("CarbonTracker.API", document.RootElement.GetProperty("info").GetProperty("title").GetString());
        Assert.True(document.RootElement.GetProperty("paths").TryGetProperty("/api/items", out _));
    }
}
