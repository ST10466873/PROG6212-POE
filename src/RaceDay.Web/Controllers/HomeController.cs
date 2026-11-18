using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Models;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

/// <summary>
/// Public landing page / Participant home. Lists upcoming events with API-side filtering
/// by event type and location; organisers are sent to their own dashboard.
/// </summary>
public class HomeController : WebControllerBase
{
    public HomeController(ApiClient api) : base(api) { }

    [HttpGet]
    public async Task<IActionResult> Index(string? eventType = null, string? location = null)
    {
        var user = HttpContext.Session.GetCurrentUser();
        if (user?.Role == "Organiser") return RedirectToAction("Dashboard", "Organiser");

        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(eventType)) query.Add($"eventType={Uri.EscapeDataString(eventType)}");
        if (!string.IsNullOrWhiteSpace(location)) query.Add($"location={Uri.EscapeDataString(location)}");
        var path = "/api/events" + (query.Count > 0 ? "?" + string.Join('&', query) : "");

        var result = await Api.GetAsync<List<EventVm>>(path);
        if (!result.Success) Report(result);

        ViewBag.EventType = eventType;
        ViewBag.Location = location;
        ViewBag.IsParticipant = user?.Role == "Participant";
        return View(result.Data ?? new List<EventVm>());
    }

    /// <summary>Shown when a signed-in user tries to open another role's pages.</summary>
    public IActionResult AccessDenied() => View();

    /// <summary>Friendly fallback for unexpected errors — no raw exception details are shown.</summary>
    public IActionResult Error(int? statusCode = null)
    {
        ViewData["StatusCode"] = statusCode;
        return View();
    }
}
