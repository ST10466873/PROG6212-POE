using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Models;
using RaceDay.Api.Services;

namespace RaceDay.Api.Helpers;

/// <summary>
/// Thin helper so every controller can extract and validate the session token
/// without duplicating header-parsing logic.
/// </summary>
public static class SessionHelper
{
    /// <summary>
    /// Reads the token from either X-Session-Token (preferred) or Authorization: Bearer.
    /// Checks X-Session-Token first to match the API plan's examples.
    /// </summary>
    public static string? GetToken(HttpRequest req)
    {
        // Primary header used in docs and tests — simple opaque token
        if (req.Headers.TryGetValue("X-Session-Token", out var v) && !string.IsNullOrWhiteSpace(v)) return v.ToString();
        // Fallback for Swagger Bearer UI — also accepted
        if (req.Headers.TryGetValue("Authorization", out var auth))
        {
            var s = auth.ToString();
            if (s.StartsWith("Bearer ")) return s["Bearer ".Length..].Trim();
        }
        // Intentionally does not accept query-string tokens (security)
        return null;
    }

    /// <summary>
    /// Resolves the current authenticated user+session or returns null.
    /// Centralises the 401 path so controllers stay concise.
    /// </summary>
    public static async Task<(User user, Session session)?> GetCurrentAsync(HttpRequest req, RaceDayContext db, AuthService auth)
    {
        var token = GetToken(req);
        // Delegate to AuthService which checks IsActive + ExpiresAt
        return await auth.ValidateSessionAsync(token);
    }

    /// <summary>Standard 401 payload — tells the client which header to send.</summary>
    public static IActionResult UnauthorizedResult() => new UnauthorizedObjectResult(new { message = "Not authenticated. Provide X-Session-Token header." });

    /// <summary>Standard 403 payload — role or ownership failure.</summary>
    public static IActionResult ForbiddenResult(string msg = "Forbidden.") => new ObjectResult(new { message = msg }) { StatusCode = 403 };
}
