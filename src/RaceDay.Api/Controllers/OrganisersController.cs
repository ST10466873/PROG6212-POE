using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.DTOs;
using RaceDay.Api.Helpers;
using RaceDay.Api.Services;

namespace RaceDay.Api.Controllers;

/// <summary>
/// Part 3 aggregate endpoint used by the Organiser MVC dashboard. Aggregation lives in the
/// API so the web application remains a thin client with no database access or business logic.
/// </summary>
[ApiController]
[Route("api/organisers")]
public class OrganisersController : ControllerBase
{
    private readonly RaceDayContext _db;
    private readonly AuthService _auth;

    public OrganisersController(RaceDayContext db, AuthService auth)
    {
        _db = db;
        _auth = auth;
    }

    /// <summary>
    /// GET /api/organisers/dashboard — event count, enrolment count, upcoming count,
    /// captured result count and a per-event summary for the signed-in Organiser.
    /// </summary>
    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard()
    {
        var current = await SessionHelper.GetCurrentAsync(Request, _db, _auth);
        if (current == null) return SessionHelper.UnauthorizedResult();
        if (current.Value.user.Role.RoleName != "Organiser")
            return SessionHelper.ForbiddenResult("Only an Organiser may view this dashboard.");

        var today = DateTime.UtcNow.Date;
        var events = await _db.Events
            .Where(e => e.OrganiserId == current.Value.user.UserId)
            .Include(e => e.Enrolments)
            .Include(e => e.Results)
            .OrderBy(e => e.EventDate)
            .ToListAsync();

        var summary = events.Select(e => new OrganiserEventSummaryDto(
            e.EventId, e.EventName, e.EventDate, e.Location, e.EventType, e.BannerImageUrl,
            e.Enrolments.Count, e.Results.Count, e.EventDate.Date >= today)).ToList();

        return Ok(new OrganiserDashboardDto(
            events.Count,
            events.Sum(e => e.Enrolments.Count),
            summary.Count(e => e.IsUpcoming),
            events.Sum(e => e.Results.Count),
            summary));
    }
}
