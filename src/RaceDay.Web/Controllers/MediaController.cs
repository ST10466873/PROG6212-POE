using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

/// <summary>
/// Streams API-hosted images to the browser. This keeps all file handling on the API
/// side (including Azure Blob Storage) and works when the API is an internal Docker host.
/// </summary>
public class MediaController : WebControllerBase
{
    public MediaController(ApiClient api) : base(api) { }

    [HttpGet]
    public async Task<IActionResult> Get(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return NotFound();

        // Escape each segment separately so sub-folders such as "events/abc.png" survive.
        var safePath = string.Join('/', path.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(Uri.EscapeDataString));
        var result = await Api.GetBytesAsync($"/api/files/{safePath}");
        if (!result.Success || result.Data.Bytes == null) return NotFound();

        return File(result.Data.Bytes, result.Data.ContentType);
    }
}
