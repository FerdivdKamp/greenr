using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using System.Text.Json;
using CarbonTracker.API.Contracts.Authentication;
using CarbonTracker.API.Contracts.Questionnaires;
using CarbonTracker.API.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.IdentityModel.Tokens;

namespace CarbonTracker.API.Tests;

public sealed class QuestionnaireTests : IClassFixture<AuthenticationFactory>
{
    private readonly AuthenticationFactory _factory;
    public QuestionnaireTests(AuthenticationFactory factory) => _factory = factory;

    private static readonly JsonElement Definition = JsonDocument.Parse("""
        {"pages":[{"elements":[
          {"type":"rating","name":"days","isRequired":true,"rateMin":0,"rateMax":7},
          {"type":"checkbox","name":"transport","choices":["bike",{"value":"train","text":"Train"}]},
          {"type":"text","name":"time","inputType":"time"},
          {"type":"boolean","name":"remote"},
          {"type":"text","name":"distance","inputType":"number","min":0,"max":100}
        ]}]}
        """).RootElement;

    private async Task<(HttpClient Client, Guid Owner)> SignIn(bool admin = false)
    {
        var client = _factory.CreateClient();
        var name = "q" + Guid.NewGuid().ToString("N")[..15];
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/register",
            new { username = name, email = name + "@example.invalid", password = "Test-password-123", role = "QuestionnaireAdmin" })).StatusCode);
        var login = await client.PostAsJsonAsync("/api/auth/login", new { identifier = name, password = "Test-password-123" });
        var tokens = (await login.Content.ReadFromJsonAsync<TokenResponse>())!;
        client.DefaultRequestHeaders.Authorization = new("Bearer", tokens.AccessToken);
        var me = await client.GetFromJsonAsync<JsonElement>("/api/auth/me");
        var owner = me.GetProperty("userId").GetGuid();
        if (admin)
        {
            // Same production issuer with an operator-configured allowlist; no fake auth handler.
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = _factory.Key,
                ["Questionnaires:AdminUserIds:0"] = owner.ToString()
            }).Build();
            client.DefaultRequestHeaders.Authorization = new("Bearer", new TokenService(config).CreateAccessToken(owner, name));
        }
        return (client, owner);
    }

    private static async Task<QuestionnaireDto> Create(HttpClient client, Guid? supersedes = null, Guid? canonical = null)
    {
        var response = await client.PostAsJsonAsync("/api/questionnaires", new
        {
            title = "Commute", definition = Definition, supersedesId = supersedes, canonicalId = canonical
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<QuestionnaireDto>())!;
    }

    [Fact]
    public async Task Authorization_Covers_All_Routes_And_Ordinary_Users_Only_Read_Active()
    {
        var (admin, _) = await SignIn(true);
        using var adminClient = admin;
        var draft = await Create(admin);
        using var anonymous = _factory.CreateClient();
        var (ordinary, _) = await SignIn();
        using var user = ordinary;
        foreach (var client in new[] { anonymous, user })
        {
            var denied = client == anonymous ? HttpStatusCode.Unauthorized : HttpStatusCode.Forbidden;
            Assert.Equal(denied, (await client.PostAsJsonAsync("/api/questionnaires", new { title = "x", definition = Definition })).StatusCode);
            Assert.Equal(denied, (await client.PostAsync($"/api/questionnaires/{draft.Id}/publish", null)).StatusCode);
            Assert.Equal(denied, (await client.PostAsJsonAsync($"/api/questionnaires/families/{draft.CanonicalId}/publish", new { latest = true })).StatusCode);
        }
        foreach (var path in new[] { "", $"/{draft.Id}", $"/{draft.CanonicalId}/latest" })
            Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/questionnaires" + path)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync($"/api/questionnaires/{draft.Id}/responses", new { answers = new { days = 2 } })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await user.GetAsync($"/api/questionnaires/{draft.Id}")).StatusCode);
        Assert.DoesNotContain((await user.GetFromJsonAsync<QuestionnaireDto[]>("/api/questionnaires"))!, q => q.Id == draft.Id);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/api/questionnaires/{draft.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsync($"/api/questionnaires/{draft.Id}/publish", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await user.GetAsync($"/api/questionnaires/{draft.Id}")).StatusCode);
        Assert.Contains((await user.GetFromJsonAsync<QuestionnaireDto[]>("/api/questionnaires"))!, q => q.Id == draft.Id);
    }

    [Fact]
    public async Task Versioning_Keeps_Active_Until_Publish_And_Supports_Family_Selectors_And_Rollback()
    {
        var (client, _) = await SignIn(true);
        using var admin = client;
        var v1 = await Create(admin);
        Assert.Equal(1, v1.Version);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/questionnaires/{v1.CanonicalId}/latest")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsync($"/api/questionnaires/{v1.Id}/publish", null)).StatusCode);
        var v2 = await Create(admin, v1.Id);
        var v3 = await Create(admin, v1.Id);
        Assert.Equal(2, v2.Version);
        Assert.Equal(3, v3.Version);
        Assert.Equal(v1.Id, (await admin.GetFromJsonAsync<QuestionnaireDto>($"/api/questionnaires/{v1.CanonicalId}/latest"))!.Id);
        var family = $"/api/questionnaires/families/{v1.CanonicalId}/publish";
        foreach (var selection in new object[] { new { targetVersion = 2 }, new { latest = true }, new { targetId = v1.Id } })
            Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsJsonAsync(family, selection)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsync($"/api/questionnaires/{v1.Id}/publish", null)).StatusCode);
        using var db = _factory.OpenDatabase();
        using var command = db.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM questionnaire WHERE canonical_id = '{v1.CanonicalId}' AND status = 'active'";
        Assert.Equal(1, Convert.ToInt32(command.ExecuteScalar()));
        command.CommandText = $"SELECT replaced_by_id FROM questionnaire WHERE id = '{v1.Id}'";
        Assert.Equal(DBNull.Value, command.ExecuteScalar());
        foreach (var selection in new object[] { new { }, new { latest = true, targetVersion = 2 }, new { targetVersion = 0 }, new { targetId = Guid.NewGuid() } })
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync(family, selection)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PostAsJsonAsync(family, new { targetVersion = 99 })).StatusCode);
        var unrelated = await Create(admin);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync(family, new { targetId = unrelated.Id })).StatusCode);
    }

    [Fact]
    public async Task Response_Uses_Claim_Owner_And_Persists_Exact_Version_Hash_And_Answer_Items()
    {
        var (admin, _) = await SignIn(true);
        using var adminClient = admin;
        var draft = await Create(admin);
        var (client, owner) = await SignIn();
        using var user = client;
        var body = new { userId = Guid.NewGuid(), answers = new { days = 2, transport = new[] { "train" }, time = "00:30", remote = false, distance = 12.5 } };
        var path = $"/api/questionnaires/{draft.Id}/responses";
        Assert.Equal(HttpStatusCode.NotFound, (await user.PostAsJsonAsync(path, body)).StatusCode);
        await admin.PostAsync($"/api/questionnaires/{draft.Id}/publish", null);
        var response = await user.PostAsJsonAsync(path, body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var id = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        using var db = _factory.OpenDatabase();
        using var command = db.CreateCommand();
        command.CommandText = $"SELECT user_id, questionnaire_id, canonical_id, definition_hash, answers_json FROM response WHERE id = '{id}'";
        using (var reader = command.ExecuteReader())
        {
            Assert.True(reader.Read());
            Assert.Equal(owner, Guid.Parse(reader[0].ToString()!));
            Assert.Equal(draft.Id, Guid.Parse(reader[1].ToString()!));
            Assert.Equal(draft.CanonicalId, Guid.Parse(reader[2].ToString()!));
            Assert.Equal(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(draft.Definition))), reader[3]);
            Assert.Equal(2, JsonDocument.Parse(reader[4].ToString()!).RootElement.GetProperty("days").GetInt32());
        }
        command.CommandText = $"SELECT COUNT(*) FROM response_item WHERE response_id = '{id}'";
        Assert.Equal(5, Convert.ToInt32(command.ExecuteScalar()));
        var next = await Create(admin, draft.Id);
        await admin.PostAsync($"/api/questionnaires/{next.Id}/publish", null);
        Assert.Equal(HttpStatusCode.NotFound, (await user.PostAsJsonAsync(path, body)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await user.GetAsync($"/api/questionnaires/{draft.Id}")).StatusCode);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"days\":8}")]
    [InlineData("{\"days\":1.5}")]
    [InlineData("{\"days\":\"2\"}")]
    [InlineData("{\"days\":2,\"unknown\":1}")]
    [InlineData("{\"days\":2,\"days\":3}")]
    [InlineData("{\"days\":2,\"transport\":[\"plane\"]}")]
    [InlineData("{\"days\":2,\"transport\":[\"bike\",\"bike\"]}")]
    [InlineData("{\"days\":2,\"time\":\"25:00\"}")]
    [InlineData("{\"days\":2,\"remote\":\"false\"}")]
    [InlineData("{\"days\":2,\"distance\":-1}")]
    [InlineData("[]")]
    public async Task Invalid_Answers_Are_Rejected_Without_Writes(string answers)
    {
        var (client, _) = await SignIn(true);
        using var admin = client;
        var questionnaire = await Create(admin);
        await admin.PostAsync($"/api/questionnaires/{questionnaire.Id}/publish", null);
        var result = await admin.PostAsync($"/api/questionnaires/{questionnaire.Id}/responses",
            new StringContent("{\"answers\":" + answers + "}", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
        using var db = _factory.OpenDatabase();
        using var command = db.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM response WHERE questionnaire_id = '{questionnaire.Id}'";
        Assert.Equal(0, Convert.ToInt32(command.ExecuteScalar()));
    }

    [Theory]
    [InlineData("{\"elements\":[]}")]
    [InlineData("\"json string\"")]
    [InlineData("{\"elements\":[{\"type\":\"matrix\",\"name\":\"q\"}]}")]
    [InlineData("{\"elements\":[{\"type\":\"text\",\"name\":\"q\",\"visibleIf\":\"true\"}]}")]
    [InlineData("{\"elements\":[{\"type\":\"text\",\"name\":\"q\"},{\"type\":\"text\",\"name\":\"q\"}]}")]
    [InlineData("{\"elements\":[{\"type\":\"checkbox\",\"name\":\"q\",\"choices\":true}]}")]
    public async Task Malformed_Or_Unsupported_Definitions_Return_400(string definition)
    {
        var (client, _) = await SignIn(true);
        using var admin = client;
        var response = await admin.PostAsync("/api/questionnaires", new StringContent(
            "{\"title\":\"Invalid\",\"definition\":" + definition + "}", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("active")]
    [InlineData("inactive")]
    [InlineData("invalid")]
    public async Task Create_Cannot_Bypass_Publish_With_Status(string status)
    {
        var (client, _) = await SignIn(true);
        using var admin = client;
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/questionnaires", new { title = "x", definition = Definition, status })).StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task Response_Rejects_Missing_Or_Invalid_Owner_Claims(string? owner)
    {
        using var client = _factory.CreateClient();
        var jwt = new JwtSecurityToken(issuer: "greenr", audience: "greenr",
            claims: owner is null ? [] : [new Claim(ClaimTypes.NameIdentifier, owner)],
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_factory.Key)), SecurityAlgorithms.HmacSha256));
        client.DefaultRequestHeaders.Authorization = new("Bearer", new JwtSecurityTokenHandler().WriteToken(jwt));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync($"/api/questionnaires/{Guid.NewGuid()}/responses",
            new { answers = new { days = 2 } })).StatusCode);
    }

    [Fact]
    public async Task Operator_Allowlist_Grants_Role_On_Real_Login_And_Refresh()
    {
        var (ordinary, owner) = await SignIn();
        using var user = ordinary;
        var me = await user.GetFromJsonAsync<JsonElement>("/api/auth/me");
        var username = me.GetProperty("username").GetString();
        using var configured = _factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?> { ["Questionnaires:AdminUserIds:0"] = owner.ToString() })));
        using var client = configured.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new { identifier = username, password = "Test-password-123" });
        var tokens = (await login.Content.ReadFromJsonAsync<TokenResponse>())!;
        client.DefaultRequestHeaders.Authorization = new("Bearer", tokens.AccessToken);
        await Create(client);
        var refresh = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = tokens.RefreshToken });
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        tokens = (await refresh.Content.ReadFromJsonAsync<TokenResponse>())!;
        client.DefaultRequestHeaders.Authorization = new("Bearer", tokens.AccessToken);
        await Create(client);
    }

    [Fact]
    public async Task Legacy_Unsupported_Definitions_Cannot_Be_Published_Or_Accept_Responses()
    {
        var (client, _) = await SignIn(true);
        using var admin = client;
        var questionnaire = await Create(admin);
        using var db = _factory.OpenDatabase();
        using var command = db.CreateCommand();
        command.CommandText = $"UPDATE questionnaire SET definition_json = '{{}}' WHERE id = '{questionnaire.Id}'";
        command.ExecuteNonQuery();
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsync($"/api/questionnaires/{questionnaire.Id}/publish", null)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync($"/api/questionnaires/families/{questionnaire.CanonicalId}/publish", new { latest = true })).StatusCode);
        command.CommandText = $"UPDATE questionnaire SET status = 'active' WHERE id = '{questionnaire.Id}'";
        command.ExecuteNonQuery();
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync($"/api/questionnaires/{questionnaire.Id}/responses", new { answers = new { days = 2 } })).StatusCode);
    }

    [Theory]
    [InlineData("100000000000000")]
    [InlineData("0.00001")]
    public async Task Numeric_Projection_Preserves_Values_Outside_Decimal_Column_Precision(string number)
    {
        var (client, _) = await SignIn(true);
        using var admin = client;
        var created = await admin.PostAsJsonAsync("/api/questionnaires", new
        {
            title = "Precision", definition = new { elements = new[] { new { type = "text", name = "amount", inputType = "number" } } }
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var questionnaire = (await created.Content.ReadFromJsonAsync<QuestionnaireDto>())!;
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsync($"/api/questionnaires/{questionnaire.Id}/publish", null)).StatusCode);
        var response = await admin.PostAsync($"/api/questionnaires/{questionnaire.Id}/responses",
            new StringContent("{\"answers\":{\"amount\":" + number + "}}", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var id = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        using var db = _factory.OpenDatabase();
        using var command = db.CreateCommand();
        command.CommandText = $"SELECT answer_text, answer_numeric FROM response_item WHERE response_id = '{id}'";
        using var reader = command.ExecuteReader();
        Assert.True(reader.Read());
        Assert.Equal(number, reader[0]);
        Assert.Equal(DBNull.Value, reader[1]);
    }
}
