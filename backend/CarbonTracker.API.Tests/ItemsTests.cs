using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using CarbonTracker.API.Contracts.Authentication;
using CarbonTracker.API.Contracts.Items;
using CarbonTracker.API.Models;
using CarbonTracker.API.Services;
using Microsoft.Extensions.Configuration;

namespace CarbonTracker.API.Tests;

public sealed class ItemsTests : IClassFixture<AuthenticationFactory>
{
    private readonly AuthenticationFactory _factory;
    public ItemsTests(AuthenticationFactory factory) => _factory = factory;

    private async Task<HttpClient> SignIn()
    {
        var client = _factory.CreateClient();
        var name = "u" + Guid.NewGuid().ToString("N")[..15];
        var register = await client.PostAsJsonAsync("/api/auth/register",
            new { username = name, email = name + "@example.invalid", password = "Test-password-123" });
        Assert.Equal(HttpStatusCode.OK, register.StatusCode);
        var login = await client.PostAsJsonAsync("/api/auth/login", new { identifier = name, password = "Test-password-123" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var tokens = await login.Content.ReadFromJsonAsync<TokenResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);
        return client;
    }

    private static object Request(string name = "Bicycle", string useCase = "Transport", decimal price = 125.50m,
        decimal footprint = 12.345m) => new
        {
            itemName = name, useCase, price, footprintKg = footprint, dateOfPurchase = "2020-06-15"
        };

    private static async Task<Item> Create(HttpClient client, object? body = null)
    {
        var response = await client.PostAsJsonAsync("/api/items", body ?? Request());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var item = (await response.Content.ReadFromJsonAsync<Item>())!;
        Assert.NotEqual(Guid.Empty, item.ItemId);
        Assert.EndsWith("/api/items/" + item.ItemId, response.Headers.Location!.ToString());
        return item;
    }

    [Fact]
    public async Task Crud_Persists_Server_Id_And_Returns_404_After_Delete()
    {
        using var client = await SignIn();
        var created = await Create(client);
        Assert.Equal(new DateOnly(2020, 6, 15), created.DateOfPurchase);
        Assert.Equal(12.345m, created.FootprintKg);
        var read = await client.GetFromJsonAsync<Item>("/api/items/" + created.ItemId);
        Assert.Equal("Bicycle", read!.ItemName);
        var update = await client.PutAsJsonAsync("/api/items/" + created.ItemId,
            new { itemName = " Updated bike ", useCase = " Transport ", price = 0, footprintKg = 0, dateOfPurchase = (string?)null });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = await update.Content.ReadFromJsonAsync<Item>();
        Assert.Equal(created.ItemId, updated!.ItemId);
        Assert.Equal("Updated bike", updated.ItemName);
        Assert.Equal("Transport", updated.UseCase);
        Assert.Null(updated.DateOfPurchase);
        var list = await client.GetFromJsonAsync<Item[]>("/api/items");
        Assert.Equal("Updated bike", Assert.Single(list!).ItemName);
        Assert.Equal(0m, (await client.GetFromJsonAsync<Item>("/api/items/" + created.ItemId))!.Price);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync("/api/items/" + created.ItemId)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/items/" + created.ItemId)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync("/api/items/" + created.ItemId, Request())).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync("/api/items/" + created.ItemId)).StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<Item[]>("/api/items"))!);
    }

    [Fact]
    public async Task Schema_Limits_And_Unknown_Date_Are_Accepted()
    {
        using var client = await SignIn();
        var item = await Create(client, new
        {
            itemName = new string('n', 50), useCase = new string('c', 20),
            price = 99999999.99m, footprintKg = 9999999.999m,
            dateOfPurchase = DateOnly.FromDateTime(DateTime.UtcNow)
        });
        Assert.Equal(99999999.99m, item.Price);
        Assert.Equal(9999999.999m, item.FootprintKg);
        var unknown = await Create(client, new { itemName = "Unknown date", price = 0, footprintKg = 0 });
        Assert.Null(unknown.DateOfPurchase);
        Assert.Equal("", unknown.UseCase);
        var items = (await client.GetFromJsonAsync<Item[]>("/api/items"))!;
        Assert.Equal(item.ItemId, items[0].ItemId);
        Assert.Equal(unknown.ItemId, items[1].ItemId);
        var summary = (await client.GetFromJsonAsync<ItemSummary>("/api/items/summary"))!;
        Assert.Equal(2, summary.ItemCount);
        Assert.Equal(99999999.99m, summary.TotalPrice);
        Assert.Equal(9999999.999m, summary.TotalFootprintKg);
        Assert.Equal(1, Assert.Single(summary.ByUseCase, group => group.UseCase == "").ItemCount);
    }

    [Fact]
    public async Task Other_User_Cannot_Read_Change_Delete_Or_Include_Items_In_Summary()
    {
        using var owner = await SignIn();
        using var other = await SignIn();
        var item = await Create(owner);
        Assert.Empty((await other.GetFromJsonAsync<Item[]>("/api/items"))!);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync("/api/items/" + item.ItemId)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PutAsJsonAsync("/api/items/" + item.ItemId, Request("Stolen"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.DeleteAsync("/api/items/" + item.ItemId)).StatusCode);
        var summary = (await other.GetFromJsonAsync<ItemSummary>("/api/items/summary"))!;
        Assert.Equal(0, summary.ItemCount);
        Assert.Equal(0m, summary.TotalPrice);
        Assert.Equal(0m, summary.TotalFootprintKg);
        Assert.Empty(summary.ByUseCase);
        Assert.Equal("Bicycle", (await owner.GetFromJsonAsync<Item>("/api/items/" + item.ItemId))!.ItemName);
    }

    [Fact]
    public async Task Body_Cannot_Set_Owner_Or_Id_And_Query_Cannot_Select_Another_User()
    {
        using var owner = await SignIn();
        using var other = await SignIn();
        var otherIdentity = await other.GetFromJsonAsync<JsonElement>("/api/auth/me");
        var forgedOwner = otherIdentity.GetProperty("userId").GetString();
        var forgedId = Guid.NewGuid();
        var body = JsonSerializer.SerializeToNode(Request())!.AsObject();
        body["userId"] = forgedOwner;
        body["itemId"] = forgedId.ToString();
        var item = await Create(owner, body);
        Assert.NotEqual(forgedId, item.ItemId);
        var update = await owner.PutAsJsonAsync("/api/items/" + item.ItemId, body);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.Equal(item.ItemId, (await update.Content.ReadFromJsonAsync<Item>())!.ItemId);
        Assert.Empty((await other.GetFromJsonAsync<Item[]>("/api/items"))!);
        var ownerIdentity = await owner.GetFromJsonAsync<JsonElement>("/api/auth/me");
        Assert.Empty((await other.GetFromJsonAsync<Item[]>("/api/items?userId=" + ownerIdentity.GetProperty("userId").GetString()))!);
        Assert.Single((await owner.GetFromJsonAsync<Item[]>("/api/items"))!);
    }

    [Fact]
    public async Task Summary_Sums_Own_Recorded_Values_And_Tracks_Updates_And_Deletes()
    {
        using var client = await SignIn();
        using var other = await SignIn();
        await Create(other, Request(price: 999m, footprint: 999m));
        var first = await Create(client, Request());
        await Create(client, Request("Train", "Transport", 10m, 1.111m));
        await Create(client, Request("Chair", "Furniture", 20m, 2m));
        var summary = (await client.GetFromJsonAsync<ItemSummary>("/api/items/summary"))!;
        Assert.Equal(3, summary.ItemCount);
        Assert.Equal(155.50m, summary.TotalPrice);
        Assert.Equal(15.456m, summary.TotalFootprintKg);
        Assert.Equal(2, summary.ByUseCase.Count);
        var transport = Assert.Single(summary.ByUseCase, g => g.UseCase == "Transport");
        Assert.Equal(2, transport.ItemCount);
        Assert.Equal(135.50m, transport.TotalPrice);
        Assert.Equal(13.456m, transport.TotalFootprintKg);
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync("/api/items/" + first.ItemId, Request(footprint: 1m))).StatusCode);
        Assert.Equal(4.111m, (await client.GetFromJsonAsync<ItemSummary>("/api/items/summary"))!.TotalFootprintKg);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync("/api/items/" + first.ItemId)).StatusCode);
        summary = (await client.GetFromJsonAsync<ItemSummary>("/api/items/summary"))!;
        Assert.Equal(2, summary.ItemCount);
        Assert.Equal(30m, summary.TotalPrice);
        Assert.Equal(3.111m, summary.TotalFootprintKg);
    }

    public static IEnumerable<object[]> InvalidFields()
    {
        yield return ["itemName", ""];
        yield return ["itemName", "   "];
        yield return ["itemName", new string('a', 51)];
        yield return ["itemName", null!];
        yield return ["useCase", new string('a', 21)];
        yield return ["useCase", null!];
        foreach (var value in new object?[] { -1m, 100000000m, 0.001m, null }) yield return ["price", value!];
        foreach (var value in new object?[] { -1m, 10000000m, 0.0001m, null }) yield return ["footprintKg", value!];
        foreach (var value in new[] { "bad-date", "2024-02-30", "0001-01-01", "9999-12-31" }) yield return ["dateOfPurchase", value];
    }

    [Theory]
    [MemberData(nameof(InvalidFields))]
    public async Task Invalid_Create_And_Update_Return_Validation_Problems_Without_Changing_Data(string field, object? value)
    {
        using var client = await SignIn();
        var item = await Create(client);
        var body = JsonSerializer.SerializeToNode(Request())!.AsObject();
        body[field] = JsonSerializer.SerializeToNode(value);
        foreach (var method in new[] { HttpMethod.Post, HttpMethod.Put })
        {
            using var request = new HttpRequestMessage(method, "/api/items" + (method == HttpMethod.Put ? "/" + item.ItemId : ""))
            { Content = JsonContent.Create(body) };
            var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Contains(problem.GetProperty("errors").EnumerateObject(),
                e => e.Name.Contains(field, StringComparison.OrdinalIgnoreCase));
        }
        Assert.Equal("Bicycle", Assert.Single((await client.GetFromJsonAsync<Item[]>("/api/items"))!).ItemName);
        Assert.Equal(12.345m, (await client.GetFromJsonAsync<ItemSummary>("/api/items/summary"))!.TotalFootprintKg);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("null")]
    public async Task Missing_Body_Fields_Are_Rejected(string json)
    {
        using var client = await SignIn();
        var response = await client.PostAsync("/api/items", new StringContent(json, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, (await client.GetFromJsonAsync<ItemSummary>("/api/items/summary"))!.ItemCount);
    }

    [Theory]
    [InlineData("absent")]
    [InlineData("invalid")]
    [InlineData("missing-owner")]
    [InlineData("invalid-owner")]
    [InlineData("empty-owner")]
    public async Task Every_Endpoint_Requires_Valid_Authentication_And_Owner(string kind)
    {
        using var client = _factory.CreateClient();
        if (kind != "absent")
        {
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:Key"] = _factory.Key }).Build();
            var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(
                issuer: "greenr", audience: "greenr", expires: DateTime.UtcNow.AddMinutes(5),
                claims: kind == "missing-owner" ? [] : [new Claim(ClaimTypes.NameIdentifier, kind == "empty-owner" ? Guid.Empty.ToString() : "bad-id")],
                signingCredentials: new Microsoft.IdentityModel.Tokens.SigningCredentials(JwtConfiguration.SigningKey(config), Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
                kind == "invalid" ? "bad-token" : new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().WriteToken(token));
        }
        var id = Guid.NewGuid();
        foreach (var (method, path) in new[]
        {
            (HttpMethod.Get, "/api/items"), (HttpMethod.Get, "/api/items/summary"),
            (HttpMethod.Get, "/api/items/" + id), (HttpMethod.Post, "/api/items"),
            (HttpMethod.Put, "/api/items/" + id), (HttpMethod.Delete, "/api/items/" + id)
        })
        {
            using var request = new HttpRequestMessage(method, path);
            if (method == HttpMethod.Post || method == HttpMethod.Put) request.Content = JsonContent.Create(Request());
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(request)).StatusCode);
        }
    }
}
