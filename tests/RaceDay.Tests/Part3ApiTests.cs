using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace RaceDay.Tests;

/// <summary>
/// Integration coverage for the Part 3 API additions: organiser dashboard,
/// personal results, blob-backed uploads and ownership enforcement.
/// </summary>
public class Part3ApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public Part3ApiTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            // Keep development-fallback uploads out of the source tree during tests.
            var testRoot = Path.Combine(Path.GetTempPath(), "RaceDayPart3Tests");
            Directory.CreateDirectory(testRoot);
            builder.UseContentRoot(testRoot);
        });
    }

    [Fact]
    public async Task OrganiserDashboard_ReturnsOwnEventCounts()
    {
        var client = _factory.CreateClient();
        var orgToken = await RegisterAndLoginAsync(client, "Organiser");
        var eventId = await CreateEventAsync(client, orgToken);
        var partToken = await RegisterAndLoginAsync(client, "Participant");
        var categoryId = await CreateCategoryAsync(client, orgToken, eventId);
        UseToken(client, partToken);
        var enrol = await client.PostAsJsonAsync($"/api/events/{eventId}/enrolments", new { categoryId });
        Assert.Equal(HttpStatusCode.Created, enrol.StatusCode);

        UseToken(client, orgToken);
        var response = await client.GetAsync("/api/organisers/dashboard");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(1, doc.RootElement.GetProperty("totalEvents").GetInt32());
        Assert.Equal(1, doc.RootElement.GetProperty("totalEnrolments").GetInt32());
        Assert.Equal("Cape Town", doc.RootElement.GetProperty("events")[0].GetProperty("location").GetString());
    }

    [Fact]
    public async Task Dashboard_IsForbiddenForParticipant()
    {
        var client = _factory.CreateClient();
        var token = await RegisterAndLoginAsync(client, "Participant");
        UseToken(client, token);
        var response = await client.GetAsync("/api/organisers/dashboard");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task MyResults_IncludesPositionAndFieldSize()
    {
        var client = _factory.CreateClient();
        var orgToken = await RegisterAndLoginAsync(client, "Organiser");
        var eventId = await CreateEventAsync(client, orgToken);
        var categoryId = await CreateCategoryAsync(client, orgToken, eventId);
        var partToken = await RegisterAndLoginAsync(client, "Participant");

        UseToken(client, partToken);
        var enrol = await client.PostAsJsonAsync($"/api/events/{eventId}/enrolments", new { categoryId });
        var enrolmentId = (await enrol.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("enrolmentId").GetInt32();

        UseToken(client, orgToken);
        var capture = await client.PostAsJsonAsync($"/api/enrolments/{enrolmentId}/result",
            new { finishTime = "00:42:15.250", position = 1 });
        Assert.Equal(HttpStatusCode.Created, capture.StatusCode);

        UseToken(client, partToken);
        var response = await client.GetAsync("/api/enrolments/me/results");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var first = doc.RootElement[0];
        Assert.Equal("00:42:15.250", first.GetProperty("finishTime").GetString());
        Assert.Equal(1, first.GetProperty("finishingPosition").GetInt32());
        Assert.Equal(1, first.GetProperty("totalFinishers").GetInt32());
    }

    [Fact]
    public async Task BannerUpload_StoresUrlOnEvent()
    {
        var client = _factory.CreateClient();
        var token = await RegisterAndLoginAsync(client, "Organiser");
        var eventId = await CreateEventAsync(client, token);

        UseToken(client, token);
        using var form = new MultipartFormDataContent();
        var bytes = Encoding.UTF8.GetBytes("fake png bytes for upload validation");
        using var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(file, "file", "banner.png");

        var upload = await client.PostAsync($"/api/events/{eventId}/banner", form);
        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        using var uploadDoc = JsonDocument.Parse(await upload.Content.ReadAsStringAsync());
        Assert.False(string.IsNullOrWhiteSpace(uploadDoc.RootElement.GetProperty("url").GetString()));

        UseToken(client, null);
        var detail = await client.GetAsync($"/api/events/{eventId}");
        using var detailDoc = JsonDocument.Parse(await detail.Content.ReadAsStringAsync());
        Assert.False(string.IsNullOrWhiteSpace(detailDoc.RootElement.GetProperty("bannerImageUrl").GetString()));
    }

    [Fact]
    public async Task BannerUpload_IsForbiddenForNonOwner()
    {
        var client = _factory.CreateClient();
        var owner = await RegisterAndLoginAsync(client, "Organiser");
        var eventId = await CreateEventAsync(client, owner);
        var other = await RegisterAndLoginAsync(client, "Organiser");

        UseToken(client, other);
        using var form = new MultipartFormDataContent();
        using var file = new ByteArrayContent(Encoding.UTF8.GetBytes("x"));
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(file, "file", "banner.png");
        var response = await client.PostAsync($"/api/events/{eventId}/banner", form);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ProfilePictureUpload_IsReflectedInProfile()
    {
        var client = _factory.CreateClient();
        var token = await RegisterAndLoginAsync(client, "Participant");
        UseToken(client, token);

        using var form = new MultipartFormDataContent();
        using var file = new ByteArrayContent(Encoding.UTF8.GetBytes("fake jpeg bytes"));
        file.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        form.Add(file, "file", "avatar.jpg");
        var upload = await client.PostAsync("/api/users/me/profile-picture", form);
        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);

        var profile = await client.GetAsync("/api/users/me");
        using var doc = JsonDocument.Parse(await profile.Content.ReadAsStringAsync());
        Assert.False(string.IsNullOrWhiteSpace(doc.RootElement.GetProperty("profilePictureUrl").GetString()));
    }

    [Fact]
    public async Task Upload_RejectsUnsupportedFileType()
    {
        var client = _factory.CreateClient();
        var token = await RegisterAndLoginAsync(client, "Organiser");
        var eventId = await CreateEventAsync(client, token);
        UseToken(client, token);

        using var form = new MultipartFormDataContent();
        using var file = new ByteArrayContent(Encoding.UTF8.GetBytes("not an image"));
        file.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        form.Add(file, "file", "malware.exe.txt");
        var response = await client.PostAsync($"/api/events/{eventId}/banner", form);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static void UseToken(HttpClient client, string? token)
    {
        client.DefaultRequestHeaders.Remove("X-Session-Token");
        if (!string.IsNullOrWhiteSpace(token)) client.DefaultRequestHeaders.Add("X-Session-Token", token);
    }

    private static async Task<string> RegisterAndLoginAsync(HttpClient client, string role)
    {
        var email = $"{role.ToLowerInvariant()}{Guid.NewGuid():N}@test.com";
        var registration = await client.PostAsJsonAsync("/api/auth/register", new
        {
            firstName = "Test",
            lastName = role,
            email,
            password = "Secret123!",
            role,
            dateOfBirth = "1990-01-01"
        });
        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);

        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "Secret123!" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var json = await login.Content.ReadFromJsonAsync<JsonElement>();
        return json.TryGetProperty("sessionToken", out var token)
            ? token.GetString()!
            : json.GetProperty("SessionToken").GetString()!;
    }

    private static async Task<int> CreateEventAsync(HttpClient client, string token)
    {
        UseToken(client, token);
        var response = await client.PostAsJsonAsync("/api/events", new
        {
            name = $"Part 3 Test {Guid.NewGuid():N}",
            description = "Integration test event",
            date = DateTime.UtcNow.AddDays(10),
            location = "Cape Town",
            distanceKm = 10m,
            eventType = "Run"
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("eventId").GetInt32();
    }

    private static async Task<int> CreateCategoryAsync(HttpClient client, string token, int eventId)
    {
        UseToken(client, token);
        var response = await client.PostAsJsonAsync($"/api/events/{eventId}/categories",
            new { categoryName = "Senior", minAge = 20, maxAge = (int?)null });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("categoryId").GetInt32();
    }
}
