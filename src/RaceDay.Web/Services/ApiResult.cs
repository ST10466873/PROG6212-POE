namespace RaceDay.Web.Services;

/// <summary>Non-generic view of an API call result for shared error handling.</summary>
public interface IApiResult
{
    bool Success { get; }
    int StatusCode { get; }
    string? Error { get; }
}

/// <summary>
/// Result wrapper returned by ApiClient. API failures become friendly messages instead
/// of exceptions so pages never display raw stack traces or JSON error documents.
/// </summary>
public sealed class ApiResult<T> : IApiResult
{
    public bool Success { get; private init; }
    public int StatusCode { get; private init; }
    public T? Data { get; private init; }
    public string? Error { get; private init; }

    public static ApiResult<T> Ok(T? data, int status = 200)
        => new() { Success = true, StatusCode = status, Data = data };

    public static ApiResult<T> Fail(string message, int status = 500)
        => new() { Success = false, StatusCode = status, Error = message };
}
