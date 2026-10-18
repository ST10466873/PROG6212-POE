using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RaceDay.Api.Data;

namespace RaceDay.Tests;

public class ApiIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    public ApiIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Testing");
        });
    }

    [Fact]
    public async Task Register_And_Login_Flow_Works()
    {
        var client = _factory.CreateClient();
        var email = $"user{Guid.NewGuid()}@test.com";
        var reg = await client.PostAsJsonAsync("/api/auth/register", new
        {
            firstName = "Test", lastName = "User", email, password = "Secret123!", role = "Participant", dateOfBirth = "2000-01-01", phoneNumber = "082 000 0000"
        });
        Assert.Equal(HttpStatusCode.Created, reg.StatusCode);
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "Secret123!" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var body = await login.Content.ReadAsStringAsync();
        Assert.Contains("sessionToken", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetEvents_IsPublic()
    {
        var client = _factory.CreateClient();
        var res = await client.GetAsync("/api/events");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task CreateEvent_RequiresOrganiserRole()
    {
        var client = _factory.CreateClient();
        // register participant and try to create event -> forbidden
        var email = $"part{Guid.NewGuid()}@test.com";
        await client.PostAsJsonAsync("/api/auth/register", new { firstName = "P", lastName = "P", email, password = "Secret123!", role = "Participant", dateOfBirth = "2000-01-01" });
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "Secret123!" });
        var loginJson = await login.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var token = loginJson.GetProperty("sessionToken").GetString() ?? loginJson.GetProperty("SessionToken").GetString();
        client.DefaultRequestHeaders.Remove("X-Session-Token");
        client.DefaultRequestHeaders.Add("X-Session-Token", token);
        var create = await client.PostAsJsonAsync("/api/events", new { name = "Hack", description = "x", date = DateTime.UtcNow.AddDays(10), location = "Test", distanceKm = 10, eventType = "Run" });
        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
    }

    [Fact]
    public async Task Enrol_And_ViewResults_Flow()
    {
        var client = _factory.CreateClient();
        // organiser creates event + category
        var orgEmail = $"org{Guid.NewGuid()}@test.com";
        await client.PostAsJsonAsync("/api/auth/register", new { firstName = "Org", lastName = "One", email = orgEmail, password = "Secret123!", role = "Organiser", dateOfBirth = "1990-01-01" });
        var orgLogin = await client.PostAsJsonAsync("/api/auth/login", new { email = orgEmail, password = "Secret123!" });
        var orgJson = await orgLogin.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var orgToken = orgJson.GetProperty("sessionToken").GetString() ?? orgJson.GetProperty("SessionToken").GetString();
        client.DefaultRequestHeaders.Remove("X-Session-Token");
        client.DefaultRequestHeaders.Add("X-Session-Token", orgToken);
        var evRes = await client.PostAsJsonAsync("/api/events", new { name = $"Event {Guid.NewGuid()}", description = "Desc", date = DateTime.UtcNow.AddDays(15), location = "Cape Town", distanceKm = 10, eventType = "Run" });
        Assert.Equal(HttpStatusCode.Created, evRes.StatusCode);
        var evJson = await evRes.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var eventId = evJson.GetProperty("eventId").GetInt32();

        var catRes = await client.PostAsJsonAsync($"/api/events/{eventId}/categories", new { categoryName = "Senior", minAge = 20, maxAge = (int?)null });
        Assert.Equal(HttpStatusCode.Created, catRes.StatusCode);
        var catJson = await catRes.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var catId = catJson.GetProperty("categoryId").GetInt32();

        // participant enrols
        var partEmail = $"part{Guid.NewGuid()}@test.com";
        await client.PostAsJsonAsync("/api/auth/register", new { firstName = "Part", lastName = "One", email = partEmail, password = "Secret123!", role = "Participant", dateOfBirth = "1995-01-01" });
        var partLogin = await client.PostAsJsonAsync("/api/auth/login", new { email = partEmail, password = "Secret123!" });
        var partJson = await partLogin.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var partToken = partJson.GetProperty("sessionToken").GetString() ?? partJson.GetProperty("SessionToken").GetString();
        client.DefaultRequestHeaders.Remove("X-Session-Token");
        client.DefaultRequestHeaders.Add("X-Session-Token", partToken);
        var enrolRes = await client.PostAsJsonAsync($"/api/events/{eventId}/enrolments", new { categoryId = catId });
        Assert.Equal(HttpStatusCode.Created, enrolRes.StatusCode);
        var enrolJson = await enrolRes.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var enrolId = enrolJson.GetProperty("enrolmentId").GetInt32();

        // organiser captures result
        client.DefaultRequestHeaders.Remove("X-Session-Token");
        client.DefaultRequestHeaders.Add("X-Session-Token", orgToken);
        var resultRes = await client.PostAsJsonAsync($"/api/enrolments/{enrolId}/result", new { finishTime = "00:35:00.000", position = 1 });
        Assert.Equal(HttpStatusCode.Created, resultRes.StatusCode);

        // public can view event results
        client.DefaultRequestHeaders.Remove("X-Session-Token");
        var results = await client.GetAsync($"/api/events/{eventId}/results");
        Assert.Equal(HttpStatusCode.OK, results.StatusCode);
        var resultsBody = await results.Content.ReadAsStringAsync();
        Assert.Contains("1", resultsBody);
    }
}
