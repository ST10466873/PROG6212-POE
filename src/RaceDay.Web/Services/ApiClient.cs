using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace RaceDay.Web.Services;

/// <summary>
/// Typed HTTP client for the Part 2 API. It is the ONLY component in the MVC project
/// that talks to the API: controllers call this client, which forwards the session token
/// and converts every failure into a friendly message for the UI.
/// </summary>
public class ApiClient
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly HttpClient _http;
    private readonly IHttpContextAccessor _accessor;
    private readonly ILogger<ApiClient> _logger;

    public ApiClient(HttpClient http, IHttpContextAccessor accessor, ILogger<ApiClient> logger)
    {
        _http = http;
        _accessor = accessor;
        _logger = logger;
    }

    /// <summary>GET request returning a deserialised payload.</summary>
    public async Task<ApiResult<T>> GetAsync<T>(string path)
        => await SendAsync<T>(HttpMethod.Get, path, null);

    /// <summary>POST request with an optional JSON body.</summary>
    public async Task<ApiResult<T>> PostAsync<T>(string path, object? payload = null)
        => await SendAsync<T>(HttpMethod.Post, path, payload);

    /// <summary>PUT request with a JSON body.</summary>
    public async Task<ApiResult<T>> PutAsync<T>(string path, object? payload)
        => await SendAsync<T>(HttpMethod.Put, path, payload);

    /// <summary>DELETE request reduced to a success flag.</summary>
    public async Task<ApiResult<bool>> DeleteAsync(string path)
    {
        var result = await SendAsync<string>(HttpMethod.Delete, path, null);
        return result.Success
            ? ApiResult<bool>.Ok(true, result.StatusCode)
            : ApiResult<bool>.Fail(result.Error ?? "The request could not be completed.", result.StatusCode);
    }

    /// <summary>Multipart upload used for event banners and profile pictures.</summary>
    public async Task<ApiResult<T>> PostFileAsync<T>(string path, IFormFile file)
    {
        using var form = new MultipartFormDataContent();
        using var stream = file.OpenReadStream();
        using var fileContent = new StreamContent(stream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType ?? "application/octet-stream");
        form.Add(fileContent, "file", file.FileName);
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = form };
        var reply = await SendRawAsync(request);
        return Deserialize<T>(reply);
    }

    /// <summary>
    /// Downloads an API-hosted image so the browser never needs direct access to the
    /// API container (important inside Docker Compose where "api" is an internal hostname).
    /// </summary>
    public async Task<ApiResult<(byte[] Bytes, string ContentType)>> GetBytesAsync(string path)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        var reply = await SendRawAsync(request);
        if (!reply.Success) return ApiResult<(byte[], string)>.Fail(FriendlyError(reply.Body, reply.Status), reply.Status);
        var bytes = await reply.Response!.Content.ReadAsByteArrayAsync();
        var type = reply.Response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
        return ApiResult<(byte[], string)>.Ok((bytes, type), reply.Status);
    }

    /// <summary>Core JSON request/response pipeline.</summary>
    private async Task<ApiResult<T>> SendAsync<T>(HttpMethod method, string path, object? payload)
    {
        using var request = new HttpRequestMessage(method, path);
        if (payload != null)
        {
            request.Content = new StringContent(JsonSerializer.Serialize(payload, Json), Encoding.UTF8, "application/json");
        }
        var reply = await SendRawAsync(request);
        return Deserialize<T>(reply);
    }

    /// <summary>Sends the request with the current session token and captures transport failures.</summary>
    private async Task<(bool Success, int Status, string Body, HttpResponseMessage? Response)> SendRawAsync(HttpRequestMessage request)
    {
        var token = _accessor.HttpContext?.Session.GetApiToken();
        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Remove("X-Session-Token");
            request.Headers.TryAddWithoutValidation("X-Session-Token", token);
        }

        try
        {
            var response = await _http.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();
            return ((int)response.StatusCode < 400, (int)response.StatusCode, body, response);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "RaceDay API request to {Path} failed", request.RequestUri);
            return (false, 503, "{\"message\":\"The RaceDay API is unavailable. Please try again shortly.\"}", null);
        }
        catch (TaskCanceledException)
        {
            return (false, 504, "{\"message\":\"The RaceDay API took too long to respond. Please try again.\"}", null);
        }
    }

    /// <summary>Turns a raw reply into a typed result, including validation error extraction.</summary>
    private static ApiResult<T> Deserialize<T>((bool Success, int Status, string Body, HttpResponseMessage? Response) reply)
    {
        if (!reply.Success) return ApiResult<T>.Fail(FriendlyError(reply.Body, reply.Status), reply.Status);

        if (reply.Status == (int)HttpStatusCode.NoContent || typeof(T) == typeof(bool) || typeof(T) == typeof(string))
            return ApiResult<T>.Ok(default, reply.Status);

        try
        {
            var data = JsonSerializer.Deserialize<T>(reply.Body, Json);
            return data is null
                ? ApiResult<T>.Fail("The API returned an empty response.", reply.Status)
                : ApiResult<T>.Ok(data, reply.Status);
        }
        catch (JsonException)
        {
            return ApiResult<T>.Fail("The API returned a response the application could not read.", reply.Status);
        }
    }

    /// <summary>Converts API problem JSON (or a status code) into a human-readable message.</summary>
    private static string FriendlyError(string body, int status)
    {
        if (!string.IsNullOrWhiteSpace(body))
        {
            try
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.String)
                    return message.GetString() ?? StatusMessage(status);
                if (doc.RootElement.TryGetProperty("title", out var title) && title.ValueKind == JsonValueKind.String)
                    return title.GetString() ?? StatusMessage(status);
                // ASP.NET ModelState payloads: { errors: { Field: ["message"] } }
                if (doc.RootElement.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
                {
                    foreach (var field in errors.EnumerateObject())
                    {
                        if (field.Value.ValueKind == JsonValueKind.Array && field.Value.GetArrayLength() > 0)
                            return $"{field.Name}: {field.Value[0].GetString()}";
                    }
                }
            }
            catch (JsonException)
            {
                // Non-JSON error page — fall through to the status-based message.
            }
        }
        return StatusMessage(status);
    }

    private static string StatusMessage(int status) => status switch
    {
        400 => "Please check the information you entered and try again.",
        401 => "Your session has expired. Please log in again.",
        403 => "You do not have permission to perform this action.",
        404 => "The item you requested could not be found.",
        409 => "This change conflicts with an existing record.",
        >= 500 => "Something went wrong on the server. Please try again shortly.",
        _ => "The request could not be completed. Please try again."
    };
}
