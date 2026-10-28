using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using CarbonTracker.API.Contracts.Authentication;
using CarbonTracker.API.Services;
using CarbonTracker.API.Services.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;

namespace CarbonTracker.API.Tests;

public sealed class TokenLifecycleTests : IClassFixture<AuthenticationFactory>
{
    private readonly AuthenticationFactory _factory;
    public TokenLifecycleTests(AuthenticationFactory factory) => _factory = factory;
    private const string Password = "Test-password-123";
    private static string Digest(string token) => "sha256:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static async Task<(string Name, TokenResponse Tokens)> Account(HttpClient client)
    {
        var name = "u" + Guid.NewGuid().ToString("N")[..15];
        (await client.PostAsJsonAsync("/api/auth/register", new { username = name, email = name + "@example.invalid", password = Password })).EnsureSuccessStatusCode();
        var login = await client.PostAsJsonAsync("/api/auth/login", new { identifier = name, password = Password });
        login.EnsureSuccessStatusCode();
        Assert.True(login.Headers.CacheControl!.NoStore);
        return (name, (await login.Content.ReadFromJsonAsync<TokenResponse>())!);
    }

    private void Expire(string table, string token)
    {
        using var db = _factory.OpenDatabase();
        using var cmd = db.CreateCommand();
        cmd.CommandText = $"UPDATE {table} SET expires_at=TIMESTAMP '2000-01-01' WHERE token=$1";
        cmd.Parameters.Add(new DuckDB.NET.Data.DuckDBParameter { Value = Digest(token) });
        cmd.ExecuteNonQuery();
    }

    private void AssertStoredAsHash(string table, string token)
    {
        using var db = _factory.OpenDatabase();
        using var cmd = db.CreateCommand();
        cmd.CommandText = $"SELECT token FROM {table} WHERE token=$1 OR token=$2";
        cmd.Parameters.Add(new DuckDB.NET.Data.DuckDBParameter { Value = token });
        cmd.Parameters.Add(new DuckDB.NET.Data.DuckDBParameter { Value = Digest(token) });
        using var r = cmd.ExecuteReader();
        Assert.True(r.Read());
        Assert.Equal(Digest(token), r.GetString(0));
        Assert.False(r.Read());
    }

    [Fact]
    public async Task Rotation_Rejects_Replay_And_Logout_Only_Revokes_Supplied_Session()
    {
        using var client = _factory.CreateClient();
        var (name, original) = await Account(client);
        AssertStoredAsHash("refresh_tokens", original.RefreshToken);
        var secondLogin = await client.PostAsJsonAsync("/api/auth/login", new { identifier = name, password = Password });
        var second = (await secondLogin.Content.ReadFromJsonAsync<TokenResponse>())!;
        var refresh = await client.PostAsJsonAsync("/api/auth/refresh", new { original.RefreshToken });
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        Assert.True(refresh.Headers.CacheControl!.NoStore);
        var rotated = (await refresh.Content.ReadFromJsonAsync<TokenResponse>())!;
        Assert.NotEqual(original.RefreshToken, rotated.RefreshToken);
        Assert.NotEqual(original.AccessToken, rotated.AccessToken);
        AssertStoredAsHash("refresh_tokens", rotated.RefreshToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", rotated.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/refresh", new { original.RefreshToken })).StatusCode);
        client.DefaultRequestHeaders.Authorization = null;
        foreach (var token in new[] { rotated.RefreshToken, rotated.RefreshToken, "unknown" })
            Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/auth/logout", new { refreshToken = token })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/refresh", new { rotated.RefreshToken })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/refresh", new { second.RefreshToken })).StatusCode);
    }

    [Fact]
    public async Task Expired_And_Unknown_Refresh_Tokens_Have_Identical_Errors()
    {
        using var client = _factory.CreateClient();
        var (_, tokens) = await Account(client);
        Expire("refresh_tokens", tokens.RefreshToken);
        string? first = null;
        foreach (var token in new[] { tokens.RefreshToken, "unknown" })
        {
            var response = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = token });
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            var problem = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
            var title = problem.GetProperty("title").GetString();
            first ??= title;
            Assert.Equal(first, title);
        }
    }

    [Fact]
    public async Task Concurrent_Refresh_Has_Exactly_One_Winner()
    {
        using var client = _factory.CreateClient();
        var (_, tokens) = await Account(client);
        var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => Task.Run(() =>
            client.PostAsJsonAsync("/api/auth/refresh", new { tokens.RefreshToken }))));
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Equal(4, responses.Count(r => r.StatusCode == HttpStatusCode.Unauthorized));
        using var db = _factory.OpenDatabase();
        using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT count(*) FROM refresh_tokens WHERE revoked_at IS NULL AND user_id=(SELECT user_id FROM refresh_tokens WHERE token=$1)";
        cmd.Parameters.Add(new DuckDB.NET.Data.DuckDBParameter { Value = Digest(tokens.RefreshToken) });
        Assert.Equal(1L, Convert.ToInt64(cmd.ExecuteScalar()));
    }

    [Theory]
    [InlineData("refresh", "{}")]
    [InlineData("logout", "{}")]
    [InlineData("refresh", "{\"refreshToken\":\" \"}")]
    [InlineData("request-reset", "{\"email\":\"invalid\"}")]
    [InlineData("reset", "{\"token\":\"abc\",\"newPassword\":\"short\"}")]
    public async Task Lifecycle_Inputs_Are_Validated(string endpoint, string json)
    {
        using var client = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/auth/" + endpoint,
            new StringContent(json, Encoding.UTF8, "application/json"))).StatusCode);
    }

    [Fact]
    public async Task Failed_Replacement_Rolls_Back_Consumption_Of_Original_Refresh_Token()
    {
        using var client = _factory.CreateClient();
        var (_, tokens) = await Account(client);
        var tokenService = new Mock<ITokenService>();
        tokenService.Setup(t => t.CreateAccessToken(It.IsAny<Guid>(), It.IsAny<string>())).Returns("access");
        tokenService.Setup(t => t.CreateRefreshToken()).Returns("replacement");
        tokenService.Setup(t => t.RefreshDays()).Throws(new InvalidOperationException("injected failure"));
        using (var db = _factory.OpenDatabase())
        {
            var users = new UsersService(db, tokenService.Object, new RecordingDelivery(),
                Microsoft.Extensions.Logging.Abstractions.NullLogger<UsersService>.Instance);
            await Assert.ThrowsAsync<InvalidOperationException>(() => users.RefreshAsync(tokens.RefreshToken));
        }
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/refresh", new { tokens.RefreshToken })).StatusCode);
    }

    [Fact]
    public async Task Concurrent_Reset_Has_Exactly_One_Winner()
    {
        var delivery = new RecordingDelivery();
        using var factory = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddSingleton<IPasswordResetDelivery>(delivery)));
        using var client = factory.CreateClient();
        var (name, _) = await Account(client);
        await client.PostAsJsonAsync("/api/auth/request-reset", new { email = name + "@example.invalid" });
        var token = Assert.Single(delivery.Messages).Token;
        var responses = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => Task.Run(() =>
            client.PostAsJsonAsync("/api/auth/reset", new { token, newPassword = "Changed-password-456" }))));
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Equal(2, responses.Count(r => r.StatusCode == HttpStatusCode.BadRequest));
    }

    [Fact]
    public async Task Reset_Delivers_Out_Of_Band_And_Revokes_All_Recovery_And_Refresh_Tokens()
    {
        var delivery = new RecordingDelivery();
        using var factory = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddSingleton<IPasswordResetDelivery>(delivery)));
        using var client = factory.CreateClient();
        var (name, tokens) = await Account(client);
        var email = name + "@example.invalid";
        var known = await client.PostAsJsonAsync("/api/auth/request-reset", new { email = email.ToUpperInvariant() });
        var unknown = await client.PostAsJsonAsync("/api/auth/request-reset", new { email = "unknown@example.invalid" });
        Assert.Equal(HttpStatusCode.OK, known.StatusCode);
        Assert.Equal(known.StatusCode, unknown.StatusCode);
        Assert.Equal(await known.Content.ReadAsStringAsync(), await unknown.Content.ReadAsStringAsync());
        var first = Assert.Single(delivery.Messages);
        Assert.Equal(email, first.Email);
        Assert.InRange(first.Expires, DateTime.UtcNow.AddMinutes(29), DateTime.UtcNow.AddMinutes(31));
        AssertStoredAsHash("password_reset_tokens", first.Token);
        Assert.DoesNotContain(first.Token, await known.Content.ReadAsStringAsync());
        await client.PostAsJsonAsync("/api/auth/request-reset", new { email });
        var second = delivery.Messages.Last();
        const string newPassword = "Changed-password-456";
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/reset", new { token = first.Token, newPassword })).StatusCode);
        foreach (var token in new[] { first.Token, second.Token, "unknown" })
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/reset", new { token, newPassword })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/refresh", new { tokens.RefreshToken })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login", new { identifier = name, password = Password })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/login", new { identifier = name, password = newPassword })).StatusCode);
    }

    [Fact]
    public async Task Expired_Reset_And_Delivery_Failure_Do_Not_Expose_Account_Details()
    {
        var delivery = new RecordingDelivery();
        using var factory = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddSingleton<IPasswordResetDelivery>(delivery)));
        using var client = factory.CreateClient();
        var (name, _) = await Account(client);
        await client.PostAsJsonAsync("/api/auth/request-reset", new { email = name + "@example.invalid" });
        var token = Assert.Single(delivery.Messages).Token;
        Expire("password_reset_tokens", token);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/reset", new { token, newPassword = "Changed-password-456" })).StatusCode);
        delivery.Fail = true;
        var known = await client.PostAsJsonAsync("/api/auth/request-reset", new { email = name + "@example.invalid" });
        var unknown = await client.PostAsJsonAsync("/api/auth/request-reset", new { email = "unknown@example.invalid" });
        Assert.Equal(HttpStatusCode.OK, known.StatusCode);
        Assert.Equal(await unknown.Content.ReadAsStringAsync(), await known.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("Production", false)]
    [InlineData("Staging", false)]
    [InlineData("Development", true)]
    public async Task Default_Delivery_Logs_Credentials_Only_In_Development(string environment, bool shouldLog)
    {
        var host = new Mock<IHostEnvironment>();
        host.SetupGet(h => h.EnvironmentName).Returns(environment);
        var logger = new Mock<ILogger<DevelopmentPasswordResetDelivery>>();
        var adapter = new DevelopmentPasswordResetDelivery(host.Object, logger.Object);
        await adapter.DeliverAsync("account@example.invalid", "secret-token", DateTime.UtcNow);
        Assert.Equal(shouldLog, logger.Invocations.Any(i => i.Method.Name == "Log"));
    }

    private sealed class RecordingDelivery : IPasswordResetDelivery
    {
        public ConcurrentQueue<(string Email, string Token, DateTime Expires)> Messages { get; } = new();
        public bool Fail { get; set; }
        public Task DeliverAsync(string email, string token, DateTime expiresAtUtc)
        {
            if (Fail) throw new InvalidOperationException("provider secret must not leak");
            Messages.Enqueue((email, token, expiresAtUtc));
            return Task.CompletedTask;
        }
    }
}
