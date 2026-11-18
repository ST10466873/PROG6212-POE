using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.DTOs;
using RaceDay.Api.Helpers;
using RaceDay.Api.Services;

namespace RaceDay.Api.Controllers;

/// <summary>
/// Enrolment flows — Participants enter events, Organisers view enrolments.
/// Implements Section 5 of the endpoint plan (4 routes) with UQ and age checks.
/// </summary>
[ApiController]
public class EnrolmentsController : ControllerBase
{
    private readonly RaceDayContext _db;
    private readonly AuthService _auth;
    public EnrolmentsController(RaceDayContext db, AuthService auth) { _db = db; _auth = auth; }

    /// <summary>
    /// POST /api/events/{id}/enrolments — Participant enters an event with a chosen category.
    /// Validates category belongs to the event and one entry per participant per event (409).
    /// </summary>
    /// <returns>201 Created, 400/403/404/409 on failure.</returns>
    [HttpPost("api/events/{id:int}/enrolments")]
    public async Task<IActionResult> Enrol(int id, [FromBody] EnrolRequest req)
    {
        // Only logged-in participants may enrol (organisers manage, not compete)
        var cur = await SessionHelper.GetCurrentAsync(Request, _db, _auth);
        if (cur == null) return SessionHelper.UnauthorizedResult();
        if (cur.Value.user.Role.RoleName != "Participant") return SessionHelper.ForbiddenResult("Organiser attempted to enrol.");
        // Event must exist
        var ev = await _db.Events.FindAsync(id);
        if (ev == null) return NotFound(new { message = "Event does not exist." });
        // Category must exist and belong to this event (cross-table rule enforced in API layer)
        var cat = await _db.EventCategories.FindAsync(req.CategoryId);
        if (cat == null || cat.EventId != id) return BadRequest(new { message = "Invalid category for the event." });
        // Enforce UQ_Enrolments_Event_User — one entry per participant per event
        if (await _db.Enrolments.AnyAsync(e => e.EventId == id && e.UserId == cur.Value.user.UserId))
            return Conflict(new { message = "Already enrolled in this event." });

        // Optional age validation against category MinAge/MaxAge
        var age = DateTime.UtcNow.Year - cur.Value.user.DateOfBirth.Year;
        if (cat.MinAge.HasValue && age < cat.MinAge.Value) return BadRequest(new { message = "Participant does not meet MinAge." });
        if (cat.MaxAge.HasValue && age > cat.MaxAge.Value) return BadRequest(new { message = "Participant exceeds MaxAge." });

        // Create enrolment — Status defaults to Confirmed
        var en = new Models.Enrolment
        {
            EventId = id,
            UserId = cur.Value.user.UserId,
            CategoryId = req.CategoryId,
            EnrolmentDate = DateTime.UtcNow,
            Status = "Confirmed"
        };
        _db.Enrolments.Add(en);
        await _db.SaveChangesAsync();
        return Created($"/api/enrolments/{en.EnrolmentId}", new EnrolmentDto(en.EnrolmentId, en.EventId, ev.EventName, en.UserId, $"{cur.Value.user.FirstName} {cur.Value.user.LastName}", cat.CategoryId, cat.CategoryName, en.EnrolmentDate, en.Status));
    }

    /// <summary>
    /// GET /api/events/{id}/enrolments — Organiser views every enrolment for their owned event.
    /// </summary>
    /// <returns>200 with participant+category details, 403 when not owner.</returns>
    [HttpGet("api/events/{id:int}/enrolments")]
    public async Task<IActionResult> GetForEvent(int id)
    {
        var cur = await SessionHelper.GetCurrentAsync(Request, _db, _auth);
        if (cur == null) return SessionHelper.UnauthorizedResult();
        var ev = await _db.Events.FindAsync(id);
        if (ev == null) return NotFound(new { message = "Event does not exist." });
        // Role gate — only organisers view the full enrolment list
        if (cur.Value.user.Role.RoleName != "Organiser") return SessionHelper.ForbiddenResult();
        // Ownership — only creator sees their race's entrants
        if (ev.OrganiserId != cur.Value.user.UserId) return SessionHelper.ForbiddenResult("Not the owning Organiser.");
        // Eager-load user/category/event for DTO projection
        var list = await _db.Enrolments.Include(e => e.User).Include(e => e.Category).Include(e => e.Event).Where(e => e.EventId == id).ToListAsync();
        return Ok(list.Select(e => new EnrolmentDto(e.EnrolmentId, e.EventId, e.Event.EventName, e.UserId, $"{e.User.FirstName} {e.User.LastName}", e.CategoryId, e.Category.CategoryName, e.EnrolmentDate, e.Status)));
    }

    /// <summary>
    /// GET /api/enrolments/me — Participant views only their own enrolments.
    /// </summary>
    /// <returns>200 with own enrolments, 403 when organiser tries.</returns>
    [HttpGet("api/enrolments/me")]
    public async Task<IActionResult> GetMine()
    {
        var cur = await SessionHelper.GetCurrentAsync(Request, _db, _auth);
        if (cur == null) return SessionHelper.UnauthorizedResult();
        if (cur.Value.user.Role.RoleName != "Participant") return SessionHelper.ForbiddenResult();
        var list = await _db.Enrolments.Include(e => e.Category).Include(e => e.Event).Where(e => e.UserId == cur.Value.user.UserId).ToListAsync();
        return Ok(list.Select(e => new EnrolmentDto(e.EnrolmentId, e.EventId, e.Event.EventName, e.UserId, $"{cur.Value.user.FirstName} {cur.Value.user.LastName}", e.CategoryId, e.Category.CategoryName, e.EnrolmentDate, e.Status)));
    }

    /// <summary>
    /// GET /api/enrolments/me/results — personal race history for the current Participant.
    /// Includes the event date, chosen category, finish time, position and the number of
    /// finishers so the MVC page can display "47th out of 312 finishers".
    /// </summary>
    [HttpGet("api/enrolments/me/results")]
    public async Task<IActionResult> GetMyResults()
    {
        var current = await SessionHelper.GetCurrentAsync(Request, _db, _auth);
        if (current == null) return SessionHelper.UnauthorizedResult();
        if (current.Value.user.Role.RoleName != "Participant")
            return SessionHelper.ForbiddenResult("Only a Participant has a personal race history.");

        var totals = await _db.Results.GroupBy(r => r.EventId)
            .Select(g => new { EventId = g.Key, Finishers = g.Count() })
            .ToDictionaryAsync(x => x.EventId, x => x.Finishers);

        var rows = await _db.Results
            .Include(r => r.Enrolment)
            .Include(r => r.Event)
            .Include(r => r.Enrolment!.Category)
            .Where(r => r.Enrolment!.UserId == current.Value.user.UserId)
            .OrderByDescending(r => r.Event.EventDate)
            .ToListAsync();

        var dtos = rows.Select(r => new MyResultDto(
            r.ResultId, r.EnrolmentId, r.EventId, r.Event.EventName, r.Event.EventDate,
            r.Enrolment!.Category.CategoryName, r.FinishTime.ToString(@"hh\:mm\:ss\.fff"),
            r.FinishingPosition, totals.TryGetValue(r.EventId, out var count) ? count : 0, r.CapturedAt));

        return Ok(dtos);
    }

    /// <summary>
    /// DELETE /api/enrolments/{id} — Participant cancels their own enrolment before race day.
    /// </summary>
    /// <returns>204 No Content, 403 when cancelling another user's enrolment.</returns>
    [HttpDelete("api/enrolments/{id:int}")]
    public async Task<IActionResult> Cancel(int id)
    {
        var cur = await SessionHelper.GetCurrentAsync(Request, _db, _auth);
        if (cur == null) return SessionHelper.UnauthorizedResult();
        var en = await _db.Enrolments.FindAsync(id);
        if (en == null) return NotFound(new { message = "Enrolment does not exist." });
        // Ownership — participants may only cancel their own row
        if (en.UserId != cur.Value.user.UserId) return SessionHelper.ForbiddenResult("Cancelling someone else's enrolment.");
        _db.Enrolments.Remove(en);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
