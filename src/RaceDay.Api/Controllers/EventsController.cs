using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.DTOs;
using RaceDay.Api.Helpers;
using RaceDay.Api.Services;

namespace RaceDay.Api.Controllers;

/// <summary>
/// Event CRUD — Organisers manage, both roles view.
/// Implements Section 3 of the endpoint plan (5 routes).
/// Ownership is enforced via Events.OrganiserId (only the creator may edit/delete).
/// </summary>
[ApiController]
[Route("api/events")]
public class EventsController : ControllerBase
{
    private readonly RaceDayContext _db;
    private readonly AuthService _auth;
    public EventsController(RaceDayContext db, AuthService auth) { _db = db; _auth = auth; }

    /// <summary>
    /// GET /api/events — public list, optionally filtered by type or location.
    /// Ordered by EventDate ascending so upcoming races appear first.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? eventType, [FromQuery] string? location)
    {
        // Start with all events including organiser for display name
        var q = _db.Events.Include(e => e.Organiser).ThenInclude(u => u.Role).AsQueryable();
        // Optional filters map directly to query params (?eventType=Run&location=Cape)
        if (!string.IsNullOrWhiteSpace(eventType)) q = q.Where(e => e.EventType == eventType);
        if (!string.IsNullOrWhiteSpace(location)) q = q.Where(e => e.Location.Contains(location));
        var list = await q.OrderBy(e => e.EventDate).ToListAsync();
        // Project to DTO — route omitted in list view for brevity
        var dtos = list.Select(e => new EventDto(e.EventId, e.OrganiserId, e.EventName, e.Description, e.EventDate, e.Location, e.DistanceKm, e.EventType, null, $"{e.Organiser.FirstName} {e.Organiser.LastName}")).ToList();
        return Ok(dtos);
    }

    /// <summary>
    /// GET /api/events/{id} — full detail including 1:1 route information.
    /// </summary>
    /// <returns>200 with EventDto+RouteDto, 404 when missing.</returns>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetOne(int id)
    {
        // Eager-load organiser and route (1:1) for the detail view
        var e = await _db.Events.Include(ev => ev.Organiser).Include(ev => ev.Route).FirstOrDefaultAsync(ev => ev.EventId == id);
        if (e == null) return NotFound(new { message = "Event does not exist." });
        RouteDto? rd = null;
        if (e.Route != null) rd = new RouteDto(e.Route.StartPoint, e.Route.EndPoint, e.Route.ElevationGainM, e.Route.GpxTrackUrl);
        return Ok(new EventDto(e.EventId, e.OrganiserId, e.EventName, e.Description, e.EventDate, e.Location, e.DistanceKm, e.EventType, rd, $"{e.Organiser.FirstName} {e.Organiser.LastName}"));
    }

    /// <summary>
    /// POST /api/events — creates an event owned by the logged-in Organiser.
    /// Validates EventType (Run/Walk/Cycle), DistanceKm &gt; 0 and required fields.
    /// </summary>
    /// <returns>201 Created, 400/401/403/409 on failure.</returns>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateEventRequest req)
    {
        // Auth: must be signed in
        var cur = await SessionHelper.GetCurrentAsync(Request, _db, _auth);
        if (cur == null) return SessionHelper.UnauthorizedResult();
        // Role gate — participants cannot create events
        if (cur.Value.user.Role.RoleName != "Organiser") return SessionHelper.ForbiddenResult("Participant attempted to create an event.");
        // Domain validation matching CK_Events_* constraints
        if (!new[] { "Run", "Walk", "Cycle" }.Contains(req.EventType)) return BadRequest(new { message = "Invalid EventType. Use Run, Walk or Cycle." });
        if (req.DistanceKm <= 0) return BadRequest(new { message = "DistanceKm must be > 0." });
        if (string.IsNullOrWhiteSpace(req.Name) || string.IsNullOrWhiteSpace(req.Location)) return BadRequest(new { message = "Name and Location are required." });

        var ev = new Models.Event
        {
            OrganiserId = cur.Value.user.UserId, // ownership pointer
            EventName = req.Name.Trim(),
            Description = req.Description?.Trim() ?? "",
            EventDate = req.Date.Date,
            Location = req.Location.Trim(),
            DistanceKm = req.DistanceKm,
            EventType = req.EventType
        };
        _db.Events.Add(ev);
        try { await _db.SaveChangesAsync(); }
        // Catch UQ_Events_Name_Date_Location — translate to 409 instead of 500
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("UNIQUE") == true || ex.InnerException?.Message.Contains("UQ_Events") == true)
        { return Conflict(new { message = "Duplicate event (same name, date and location)." }); }

        // Create a default route so GET /{id} always returns route info (1:1)
        var route = new Models.Route { EventId = ev.EventId, StartPoint = ev.Location, EndPoint = ev.Location, ElevationGainM = 0 };
        _db.Routes.Add(route);
        await _db.SaveChangesAsync();

        return Created($"/api/events/{ev.EventId}", new EventDto(ev.EventId, ev.OrganiserId, ev.EventName, ev.Description, ev.EventDate, ev.Location, ev.DistanceKm, ev.EventType, new RouteDto(route.StartPoint, route.EndPoint, route.ElevationGainM, route.GpxTrackUrl), $"{cur.Value.user.FirstName} {cur.Value.user.LastName}"));
    }

    /// <summary>
    /// PUT /api/events/{id} — updates an owned event (owner-only).
    /// </summary>
    /// <returns>200, 403 when not owner, 404 when missing.</returns>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] CreateEventRequest req)
    {
        var cur = await SessionHelper.GetCurrentAsync(Request, _db, _auth);
        if (cur == null) return SessionHelper.UnauthorizedResult();
        if (cur.Value.user.Role.RoleName != "Organiser") return SessionHelper.ForbiddenResult();
        var ev = await _db.Events.FindAsync(id);
        if (ev == null) return NotFound(new { message = "Event does not exist." });
        // Ownership check — only creator may mutate
        if (ev.OrganiserId != cur.Value.user.UserId) return SessionHelper.ForbiddenResult("Organiser does not own the event.");
        // Re-validate domain rules on update
        if (!new[] { "Run", "Walk", "Cycle" }.Contains(req.EventType)) return BadRequest(new { message = "Invalid EventType." });
        if (req.DistanceKm <= 0) return BadRequest(new { message = "DistanceKm must be > 0." });

        ev.EventName = req.Name.Trim();
        ev.Description = req.Description?.Trim() ?? "";
        ev.EventDate = req.Date.Date;
        ev.Location = req.Location.Trim();
        ev.DistanceKm = req.DistanceKm;
        ev.EventType = req.EventType;
        await _db.SaveChangesAsync();
        return Ok(new EventDto(ev.EventId, ev.OrganiserId, ev.EventName, ev.Description, ev.EventDate, ev.Location, ev.DistanceKm, ev.EventType, null, ""));
    }

    /// <summary>
    /// DELETE /api/events/{id} — deletes an owned event and cascades enrolments/results.
    /// </summary>
    /// <returns>204 No Content, 403/404 on failure.</returns>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var cur = await SessionHelper.GetCurrentAsync(Request, _db, _auth);
        if (cur == null) return SessionHelper.UnauthorizedResult();
        if (cur.Value.user.Role.RoleName != "Organiser") return SessionHelper.ForbiddenResult();
        var ev = await _db.Events.Include(e => e.Enrolments).Include(e => e.Results).FirstOrDefaultAsync(e => e.EventId == id);
        if (ev == null) return NotFound(new { message = "Event does not exist." });
        if (ev.OrganiserId != cur.Value.user.UserId) return SessionHelper.ForbiddenResult("Organiser does not own the event.");
        _db.Events.Remove(ev); // cascade via FK
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // Categories sub-routes are handled in CategoriesController via attribute routing

    // Enrolments sub-routes for events (view and create) are in EnrolmentsController
    // Results sub-routes for events are in ResultsController
}
