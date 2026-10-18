using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Models;
using RaceDay.Api.Services;

namespace RaceDay.Api.Helpers;

public static class SessionHelper
{
    public static string? GetToken(HttpRequest req)
    {
        if (req.Headers.TryGetValue("X-Session-Token", out var v) && !string.IsNullOrWhiteSpace(v)) return v.ToString();
        if (req.Headers.TryGetValue("Authorization", out var auth))
        {
            var s = auth.ToString();
            if (s.StartsWith("Bearer ")) return s["Bearer ".Length..].Trim();
        }
        // also support query? no
        return null;
    }

    public static async Task<(User user, Session session)?> GetCurrentAsync(HttpRequest req, RaceDayContext db, AuthService auth)
    {
        var token = GetToken(req);
        return await auth.ValidateSessionAsync(token);
    }

    public static IActionResult UnauthorizedResult() => new UnauthorizedObjectResult(new { message = "Not authenticated. Provide X-Session-Token header." });
    public static IActionResult ForbiddenResult(string msg = "Forbidden.") => new ObjectResult(new { message = msg }) { StatusCode = 403 };
}
