using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.DTOs;
using RaceDay.Api.Helpers;
using RaceDay.Api.Services;

namespace RaceDay.Api.Controllers;

[ApiController]
[Route("api/events")]
public class EventsController : ControllerBase
{
    private readonly RaceDayContext _db;
    private readonly AuthService _auth;
    public EventsController(RaceDayContext db, AuthService auth) { _db = db; _auth = auth; }

    // GET /api/events
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? eventType, [FromQuery] string? location)
    {
        var q = _db.Events.Include(e => e.Organiser).ThenInclude(u => u.Role).AsQueryable();
        if (!string.IsNullOrWhiteSpace(eventType)) q = q.Where(e => e.EventType == eventType);
        if (!string.IsNullOrWhiteSpace(location)) q = q.Where(e => e.Location.Contains(location));
        var list = await q.OrderBy(e => e.EventDate).ToListAsync();
        var dtos = list.Select(e => new EventDto(e.EventId, e.OrganiserId, e.EventName, e.Description, e.EventDate, e.Location, e.DistanceKm, e.EventType, null, $"{e.Organiser.FirstName} {e.Organiser.LastName}")).ToList();
        return Ok(dtos);
    }

    // GET /api/events/{id}
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetOne(int id)
    {
        var e = await _db.Events.Include(ev => ev.Organiser).Include(ev => ev.Route).FirstOrDefaultAsync(ev => ev.EventId == id);
        if (e == null) return NotFound(new { message = "Event does not exist." });
        RouteDto? rd = null;
        if (e.Route != null) rd = new RouteDto(e.Route.StartPoint, e.Route.EndPoint, e.Route.ElevationGainM, e.Route.GpxTrackUrl);
        return Ok(new EventDto(e.EventId, e.OrganiserId, e.EventName, e.Description, e.EventDate, e.Location, e.DistanceKm, e.EventType, rd, $"{e.Organiser.FirstName} {e.Organiser.LastName}"));
    }

    // POST /api/events
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateEventRequest req)
    {
        var cur = await SessionHelper.GetCurrentAsync(Request, _db, _auth);
        if (cur == null) return SessionHelper.UnauthorizedResult();
        if (cur.Value.user.Role.RoleName != "Organiser") return SessionHelper.ForbiddenResult("Participant attempted to create an event.");
        if (!new[] { "Run", "Walk", "Cycle" }.Contains(req.EventType)) return BadRequest(new { message = "Invalid EventType. Use Run, Walk or Cycle." });
        if (req.DistanceKm <= 0) return BadRequest(new { message = "DistanceKm must be > 0." });
        if (string.IsNullOrWhiteSpace(req.Name) || string.IsNullOrWhiteSpace(req.Location)) return BadRequest(new { message = "Name and Location are required." });

        var ev = new Models.Event
        {
            OrganiserId = cur.Value.user.UserId,
            EventName = req.Name.Trim(),
            Description = req.Description?.Trim() ?? "",
            EventDate = req.Date.Date,
            Location = req.Location.Trim(),
            DistanceKm = req.DistanceKm,
            EventType = req.EventType
        };
        _db.Events.Add(ev);
        try { await _db.SaveChangesAsync(); }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("UNIQUE") == true || ex.InnerException?.Message.Contains("UQ_Events") == true)
        { return Conflict(new { message = "Duplicate event (same name, date and location)." }); }

        // create a default route so GET includes route
        var route = new Models.Route { EventId = ev.EventId, StartPoint = ev.Location, EndPoint = ev.Location, ElevationGainM = 0 };
        _db.Routes.Add(route);
        await _db.SaveChangesAsync();

        return Created($"/api/events/{ev.EventId}", new EventDto(ev.EventId, ev.OrganiserId, ev.EventName, ev.Description, ev.EventDate, ev.Location, ev.DistanceKm, ev.EventType, new RouteDto(route.StartPoint, route.EndPoint, route.ElevationGainM, route.GpxTrackUrl), $"{cur.Value.user.FirstName} {cur.Value.user.LastName}"));
    }

    // PUT /api/events/{id}
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] CreateEventRequest req)
    {
        var cur = await SessionHelper.GetCurrentAsync(Request, _db, _auth);
        if (cur == null) return SessionHelper.UnauthorizedResult();
        if (cur.Value.user.Role.RoleName != "Organiser") return SessionHelper.ForbiddenResult();
        var ev = await _db.Events.FindAsync(id);
        if (ev == null) return NotFound(new { message = "Event does not exist." });
        if (ev.OrganiserId != cur.Value.user.UserId) return SessionHelper.ForbiddenResult("Organiser does not own the event.");
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

    // DELETE /api/events/{id}
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var cur = await SessionHelper.GetCurrentAsync(Request, _db, _auth);
        if (cur == null) return SessionHelper.UnauthorizedResult();
        if (cur.Value.user.Role.RoleName != "Organiser") return SessionHelper.ForbiddenResult();
        var ev = await _db.Events.Include(e => e.Enrolments).Include(e => e.Results).FirstOrDefaultAsync(e => e.EventId == id);
        if (ev == null) return NotFound(new { message = "Event does not exist." });
        if (ev.OrganiserId != cur.Value.user.UserId) return SessionHelper.ForbiddenResult("Organiser does not own the event.");
        _db.Events.Remove(ev);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // Categories sub-routes are handled in CategoriesController via attribute routing

    // Enrolments sub-routes for events (view and create) are in EnrolmentsController
    // Results sub-routes for events are in ResultsController
}
