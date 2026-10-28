using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CarbonTracker.API.Contracts.Authentication;
using DuckDB.NET.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace CarbonTracker.API.Tests;

public sealed class AuthenticationTests : IClassFixture<AuthenticationFactory>
{
    private readonly AuthenticationFactory _factory;
    public AuthenticationTests(AuthenticationFactory factory) => _factory = factory;

    private async Task<string> Register(HttpClient client)
    {
        var name = "u" + Guid.NewGuid().ToString("N")[..15];
        var response = await client.PostAsJsonAsync("/api/auth/register",
            new { username = name, email = name + "@example.invalid", password = "Test-password-123" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return name;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Register_Login_And_Me_Return_Authenticated_Identity(bool useEmail)
    {
        using var client = _factory.CreateClient();
        var name = await Register(client);
        var identifier = useEmail ? name.ToUpperInvariant() + "@EXAMPLE.INVALID" : name;
        var login = await client.PostAsJsonAsync("/api/auth/login",
            new { identifier, password = "Test-password-123" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var tokens = await login.Content.ReadFromJsonAsync<TokenResponse>();
        Assert.False(string.IsNullOrWhiteSpace(tokens!.RefreshToken));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        var me = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        var identity = await me.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(name, identity.GetProperty("username").GetString());
        Assert.True(Guid.TryParse(identity.GetProperty("userId").GetString(), out _));
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/users")).StatusCode);
    }

    [Theory]
    [InlineData("username")]
    [InlineData("email")]
    public async Task Duplicate_Registration_Returns_Safe_Conflict(string duplicateField)
    {
        using var client = _factory.CreateClient();
        var name = await Register(client);
        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            username = duplicateField == "username" ? name.ToUpperInvariant() : "another_user",
            email = duplicateField == "email" ? name.ToUpperInvariant() + "@EXAMPLE.INVALID" : "other@example.invalid",
            password = "Test-password-123"
        });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Username or email already exists.", problem.GetProperty("title").GetString());
        Assert.False(problem.TryGetProperty("exception", out _));
    }

    [Fact]
    public async Task Unknown_User_And_Wrong_Password_Return_Same_401()
    {
        using var client = _factory.CreateClient();
        var name = await Register(client);
        foreach (var identifier in new[] { name, "missing@example.invalid" })
        {
            var response = await client.PostAsJsonAsync("/api/auth/login", new { identifier, password = "Wrong-password" });
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("Invalid username or password.", problem.GetProperty("title").GetString());
        }
    }

    [Theory]
    [InlineData("register", "{}")]
    [InlineData("register", "{\"username\":\"a\",\"email\":\"bad\",\"password\":\"long-enough\"}")]
    [InlineData("register", "{\"username\":\"a\",\"email\":\"a@example.invalid\",\"password\":\"short\"}")]
    [InlineData("register", "{\"username\":\"abcdefghijklmnopqrstu\",\"email\":\"a@example.invalid\",\"password\":\"long-enough\"}")]
    [InlineData("register", "{\"username\":\"a@b\",\"email\":\"a@example.invalid\",\"password\":\"long-enough\"}")]
    [InlineData("login", "{}")]
    [InlineData("login", "{\"identifier\":\" \",\"password\":\"password\"}")]
    [InlineData("login", "{\"identifier\":\"a\",\"password\":null}")]
    public async Task Invalid_Input_Returns_Validation_Problem(string endpoint, string json)
    {
        using var client = _factory.CreateClient();
        var response = await client.PostAsync("/api/auth/" + endpoint, new StringContent(json, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(problem.TryGetProperty("errors", out _));
    }

    [Fact]
    public async Task BCrypt_Byte_Limit_Is_Validated()
    {
        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            username = "byte_limit", email = "bytes@example.invalid", password = new string('é', 37)
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("absent")]
    [InlineData("malformed")]
    [InlineData("expired")]
    [InlineData("signature")]
    [InlineData("issuer")]
    [InlineData("audience")]
    public async Task Me_Rejects_Missing_Or_Invalid_Tokens(string kind)
    {
        using var client = _factory.CreateClient();
        if (kind != "absent")
        {
            var key = kind == "signature" ? Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)) : _factory.Key;
            var token = new JwtSecurityToken(
                issuer: kind == "issuer" ? "wrong" : "greenr",
                audience: kind == "audience" ? "wrong" : "greenr",
                expires: kind == "expired" ? DateTime.UtcNow.AddMinutes(-1) : DateTime.UtcNow.AddMinutes(5),
                signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
                kind == "malformed" ? "not-a-jwt" : new JwtSecurityTokenHandler().WriteToken(token));
        }
        var response = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains(response.Headers.WwwAuthenticate, value => value.Scheme == "Bearer");
    }

    [Fact]
    public async Task User_List_Is_Removed_And_Invalid_Reset_Is_Safe()
    {
        using var client = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/users")).StatusCode);
        var response = await client.PostAsJsonAsync("/api/auth/reset", new { token = "missing", newPassword = "Test-password-123" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Invalid or expired token.", problem.GetProperty("title").GetString());
    }
}

public sealed class AuthenticationFactory : WebApplicationFactory<Program>
{
    private readonly string _database = Path.Combine(Path.GetTempPath(), "greenr-auth-" + Guid.NewGuid() + ".duckdb");
    public string Key { get; } = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    public AuthenticationFactory()
    {
        using var connection = new DuckDBConnection("Data Source=" + _database);
        connection.Open();
        // Migration 002 is only a legacy rename; the Python runner skips it
        // on fresh databases. Apply the remaining schema migrations in order.
        foreach (var migration in Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "migrations"), "*.sql")
                     .Order().Where(path => !Path.GetFileName(path).StartsWith("002_")))
        {
            using var transaction = connection.BeginTransaction();
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = File.ReadAllText(migration);
            command.ExecuteNonQuery();
            transaction.Commit();
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Key"] = Key,
            ["Jwt:Issuer"] = "greenr",
            ["Jwt:Audience"] = "greenr",
            ["ConnectionStrings:DuckDb"] = "Data Source=" + _database
        }));
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        // TestServer can finish a response before its request scope has released
        // the native DuckDB handle. Allow a short, bounded cleanup grace period.
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                File.Delete(_database);
                break;
            }
            catch (IOException) when (attempt < 20)
            {
                await Task.Delay(50);
            }
        }
    }
}
