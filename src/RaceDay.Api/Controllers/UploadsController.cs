using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.DTOs;
using RaceDay.Api.Helpers;
using RaceDay.Api.Services;

namespace RaceDay.Api.Controllers;

/// <summary>
/// Part 3 file-handling endpoints. Every image uploaded by the MVC web application is
/// received here; the web project never talks to Azure Blob Storage directly.
/// </summary>
[ApiController]
public class UploadsController : ControllerBase
{
    // Only small web images are accepted — protects storage costs and prevents abuse.
    private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".webp", ".gif" };
    private const long MaxBytes = 5 * 1024 * 1024;

    private readonly RaceDayContext _db;
    private readonly AuthService _auth;
    private readonly IImageStorageService _storage;

    public UploadsController(RaceDayContext db, AuthService auth, IImageStorageService storage)
    {
        _db = db;
        _auth = auth;
        _storage = storage;
    }

    /// <summary>
    /// POST /api/events/{id}/banner — uploads an event banner and saves its URL on the event.
    /// The owning Organiser is resolved from the session, so one organiser cannot brand another's event.
    /// </summary>
    /// <returns>200 with the stored URL, or 400/401/403/404.</returns>
    [HttpPost("api/events/{id:int}/banner")]
    public async Task<IActionResult> UploadBanner(int id, [FromForm] IFormFile? file)
    {
        var invalid = Validate(file);
        if (invalid != null) return invalid;

        var current = await SessionHelper.GetCurrentAsync(Request, _db, _auth);
        if (current == null) return SessionHelper.UnauthorizedResult();
        if (current.Value.user.Role.RoleName != "Organiser") return SessionHelper.ForbiddenResult("Only an Organiser may upload a banner.");

        var ev = await _db.Events.FindAsync(id);
        if (ev == null) return NotFound(new { message = "Event does not exist." });
        if (ev.OrganiserId != current.Value.user.UserId) return SessionHelper.ForbiddenResult("Not the owning Organiser.");

        var stored = await _storage.SaveAsync(file!, "events", HttpContext.RequestAborted);
        ev.BannerImageUrl = stored.Url;
        await _db.SaveChangesAsync();

        return Ok(new ImageUploadResponse(stored.Url, Path.GetFileName(file!.FileName), stored.StorageProvider));
    }

    /// <summary>
    /// POST /api/users/me/profile-picture — uploads the signed-in user's profile picture.
    /// Accepted for both roles; the URL is returned through GET /api/users/me.
    /// </summary>
    [HttpPost("api/users/me/profile-picture")]
    public async Task<IActionResult> UploadProfilePicture([FromForm] IFormFile? file)
    {
        var invalid = Validate(file);
        if (invalid != null) return invalid;

        var current = await SessionHelper.GetCurrentAsync(Request, _db, _auth);
        if (current == null) return SessionHelper.UnauthorizedResult();

        var stored = await _storage.SaveAsync(file!, "profiles", HttpContext.RequestAborted);
        var user = await _db.Users.FindAsync(current.Value.user.UserId);
        user!.ProfilePictureUrl = stored.Url;
        await _db.SaveChangesAsync();

        return Ok(new ImageUploadResponse(stored.Url, Path.GetFileName(file!.FileName), stored.StorageProvider));
    }

    /// <summary>
    /// GET /api/files/{path} — public read proxy for locally stored development images.
    /// Blob-backed images are returned directly from Azure, but the MVC app may still use this
    /// route for the fallback; path traversal and absolute URLs are rejected by the storage service.
    /// </summary>
    [HttpGet("api/files/{**path}")]
    public async Task<IActionResult> GetFile(string path)
    {
        var found = await _storage.OpenAsync(path, HttpContext.RequestAborted);
        if (found == null) return NotFound();
        return File(found.Value.Stream, found.Value.ContentType, enableRangeProcessing: true);
    }

    /// <summary>Shared upload validation — type, size and non-empty check.</summary>
    private IActionResult? Validate(IFormFile? file)
    {
        if (file == null || file.Length == 0) return BadRequest(new { message = "Choose an image to upload." });
        if (file.Length > MaxBytes) return BadRequest(new { message = "Image must be 5 MB or smaller." });
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
            return BadRequest(new { message = "Only JPG, PNG, WEBP and GIF images are supported." });
        return null;
    }
}
