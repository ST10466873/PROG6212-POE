using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.DTOs;
using RaceDay.Api.Helpers;
using RaceDay.Api.Services;

namespace RaceDay.Api.Controllers;

/// <summary>
/// Results management — Organisers capture/correct times and positions, participants view own.
/// Implements Section 6 of the endpoint plan (4 routes) with 1:1 and position uniqueness.
/// </summary>
[ApiController]
public class ResultsController : ControllerBase
{
    private readonly RaceDayContext _db;
    private readonly AuthService _auth;
    public ResultsController(RaceDayContext db, AuthService auth) { _db = db; _auth = auth; }

    /// <summary>
    /// POST /api/enrolments/{id}/result — Organiser captures a finish time and position.
    /// Enforces 1:1 per enrolment (409 if already captured) and unique position per event.
    /// </summary>
    /// <returns>201 Created, 400/403/404/409 on failure.</returns>
    [HttpPost("api/enrolments/{id:int}/result")]
    public async Task<IActionResult> Capture(int id, [FromBody] CreateResultRequest req)
    {
        // Only organisers capture results (participants view only)
        var cur = await SessionHelper.GetCurrentAsync(Request, _db, _auth);
        if (cur == null) return SessionHelper.UnauthorizedResult();
        if (cur.Value.user.Role.RoleName != "Organiser") return SessionHelper.ForbiddenResult();
        // Enrolment must exist — include Event for ownership check
        var en = await _db.Enrolments.Include(e => e.Event).FirstOrDefaultAsync(e => e.EnrolmentId == id);
        if (en == null) return NotFound(new { message = "Enrolment does not exist." });
        // Ownership — only the event's organiser captures results for that race
        if (en.Event.OrganiserId != cur.Value.user.UserId) return SessionHelper.ForbiddenResult("Not the owning Organiser.");
        // Enforce UQ_Results_EnrolmentId — one result per enrolment (1:1)
        if (await _db.Results.AnyAsync(r => r.EnrolmentId == id)) return Conflict(new { message = "Result already captured." });
        // Enforce UQ_Results_Event_Position — positions 1,2,3… unique within event
        if (await _db.Results.AnyAsync(r => r.EventId == en.EventId && r.FinishingPosition == req.Position))
            return Conflict(new { message = "Position already taken in the event." });
        // Validate time format (HH:MM:SS.fff) and position &gt; 0
        if (!TimeSpan.TryParse(req.FinishTime, out var ts)) return BadRequest(new { message = "Invalid FinishTime format. Use HH:MM:SS.fff" });
        if (req.Position <= 0) return BadRequest(new { message = "Position must be > 0." });

        var res = new Models.Result
        {
            EnrolmentId = id,
            EventId = en.EventId, // denormalised for position uniqueness
            FinishTime = ts,
            FinishingPosition = req.Position,
            CapturedAt = DateTime.UtcNow
        };
        _db.Results.Add(res);
        await _db.SaveChangesAsync();
        var user = await _db.Users.FindAsync(en.UserId);
        return Created($"/api/results/{res.ResultId}", new ResultDto(res.ResultId, res.EnrolmentId, res.EventId, en.Event.EventName, $"{user!.FirstName} {user.LastName}", res.FinishTime.ToString(@"hh\:mm\:ss\.fff"), res.FinishingPosition, res.CapturedAt));
    }

    /// <summary>
    /// PUT /api/results/{id} — corrects an existing result (e.g. timing dispute).
    /// Checks position conflict excluding the current row.
    /// </summary>
    /// <returns>200 OK, 403/404/409 on failure.</returns>
    [HttpPut("api/results/{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] CreateResultRequest req)
    {
        var cur = await SessionHelper.GetCurrentAsync(Request, _db, _auth);
        if (cur == null) return SessionHelper.UnauthorizedResult();
        if (cur.Value.user.Role.RoleName != "Organiser") return SessionHelper.ForbiddenResult();
        var res = await _db.Results.Include(r => r.Event).FirstOrDefaultAsync(r => r.ResultId == id);
        if (res == null) return NotFound(new { message = "Result does not exist." });
        // Only owning organiser may correct
        if (res.Event.OrganiserId != cur.Value.user.UserId) return SessionHelper.ForbiddenResult("Not the owning Organiser.");
        // Position must remain unique within event (exclude self)
        if (await _db.Results.AnyAsync(r => r.EventId == res.EventId && r.FinishingPosition == req.Position && r.ResultId != id))
            return Conflict(new { message = "Position conflicts with another result." });
        if (!TimeSpan.TryParse(req.FinishTime, out var ts)) return BadRequest(new { message = "Invalid FinishTime." });
        res.FinishTime = ts;
        res.FinishingPosition = req.Position;
        await _db.SaveChangesAsync();
        return Ok(new ResultDto(res.ResultId, res.EnrolmentId, res.EventId, res.Event.EventName, "", res.FinishTime.ToString(@"hh\:mm\:ss\.fff"), res.FinishingPosition, res.CapturedAt));
    }

    /// <summary>
    /// GET /api/enrolments/{id}/result — returns the result for one enrolment.
    /// Allowed for the participant who owns it or the event's organiser.
    /// </summary>
    /// <returns>200, 403 when another user's result, 404 when no result yet.</returns>
    [HttpGet("api/enrolments/{id:int}/result")]
    public async Task<IActionResult> GetForEnrolment(int id)
    {
        var cur = await SessionHelper.GetCurrentAsync(Request, _db, _auth);
        if (cur == null) return SessionHelper.UnauthorizedResult();
        var en = await _db.Enrolments.Include(e => e.Event).FirstOrDefaultAsync(e => e.EnrolmentId == id);
        if (en == null) return NotFound(new { message = "Enrolment does not exist." });
        // Authorisation: own enrolment or owning organiser
        var isOwnerOrganiser = en.Event.OrganiserId == cur.Value.user.UserId && cur.Value.user.Role.RoleName == "Organiser";
        var isOwn = en.UserId == cur.Value.user.UserId;
        if (!isOwnerOrganiser && !isOwn) return SessionHelper.ForbiddenResult("Another user's result.");
        var res = await _db.Results.Include(r => r.Enrolment).ThenInclude(en2 => en2.User).FirstOrDefaultAsync(r => r.EnrolmentId == id);
        if (res == null) return NotFound(new { message = "Result does not exist." });
        var participant = res.Enrolment.User;
        return Ok(new ResultDto(res.ResultId, res.EnrolmentId, res.EventId, en.Event.EventName, $"{participant.FirstName} {participant.LastName}", res.FinishTime.ToString(@"hh\:mm\:ss\.fff"), res.FinishingPosition, res.CapturedAt));
    }

    /// <summary>
    /// GET /api/events/{id}/results — public ordered results table for an event.
    /// Ordered by FinishingPosition ascending (podium first).
    /// </summary>
    /// <returns>200 with ordered list, 404 when event missing.</returns>
    [HttpGet("api/events/{id:int}/results")]
    public async Task<IActionResult> GetForEvent(int id)
    {
        var ev = await _db.Events.FindAsync(id);
        if (ev == null) return NotFound(new { message = "Event does not exist." });
        // Include participant for display name, order by position
        var results = await _db.Results.Include(r => r.Enrolment).ThenInclude(en => en.User).Where(r => r.EventId == id).OrderBy(r => r.FinishingPosition).ToListAsync();
        return Ok(results.Select(r => new ResultDto(r.ResultId, r.EnrolmentId, r.EventId, ev.EventName, $"{r.Enrolment.User.FirstName} {r.Enrolment.User.LastName}", r.FinishTime.ToString(@"hh\:mm\:ss\.fff"), r.FinishingPosition, r.CapturedAt)));
    }
}
