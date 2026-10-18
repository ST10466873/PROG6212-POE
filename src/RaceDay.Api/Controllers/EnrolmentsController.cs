using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.DTOs;
using RaceDay.Api.Helpers;
using RaceDay.Api.Services;

namespace RaceDay.Api.Controllers;

[ApiController]
public class EnrolmentsController : ControllerBase
{
    private readonly RaceDayContext _db;
    private readonly AuthService _auth;
    public EnrolmentsController(RaceDayContext db, AuthService auth) { _db = db; _auth = auth; }

    // POST /api/events/{id}/enrolments
    [HttpPost("api/events/{id:int}/enrolments")]
    public async Task<IActionResult> Enrol(int id, [FromBody] EnrolRequest req)
    {
        var cur = await SessionHelper.GetCurrentAsync(Request, _db, _auth);
        if (cur == null) return SessionHelper.UnauthorizedResult();
        if (cur.Value.user.Role.RoleName != "Participant") return SessionHelper.ForbiddenResult("Organiser attempted to enrol.");
        var ev = await _db.Events.FindAsync(id);
        if (ev == null) return NotFound(new { message = "Event does not exist." });
        var cat = await _db.EventCategories.FindAsync(req.CategoryId);
        if (cat == null || cat.EventId != id) return BadRequest(new { message = "Invalid category for the event." });
        if (await _db.Enrolments.AnyAsync(e => e.EventId == id && e.UserId == cur.Value.user.UserId))
            return Conflict(new { message = "Already enrolled in this event." });

        // optional age validation
        var age = DateTime.UtcNow.Year - cur.Value.user.DateOfBirth.Year;
        if (cat.MinAge.HasValue && age < cat.MinAge.Value) return BadRequest(new { message = "Participant does not meet MinAge." });
        if (cat.MaxAge.HasValue && age > cat.MaxAge.Value) return BadRequest(new { message = "Participant exceeds MaxAge." });

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

    // GET /api/events/{id}/enrolments  (Organiser)
    [HttpGet("api/events/{id:int}/enrolments")]
    public async Task<IActionResult> GetForEvent(int id)
    {
        var cur = await SessionHelper.GetCurrentAsync(Request, _db, _auth);
        if (cur == null) return SessionHelper.UnauthorizedResult();
        var ev = await _db.Events.FindAsync(id);
        if (ev == null) return NotFound(new { message = "Event does not exist." });
        if (cur.Value.user.Role.RoleName != "Organiser") return SessionHelper.ForbiddenResult();
        if (ev.OrganiserId != cur.Value.user.UserId) return SessionHelper.ForbiddenResult("Not the owning Organiser.");
        var list = await _db.Enrolments.Include(e => e.User).Include(e => e.Category).Include(e => e.Event).Where(e => e.EventId == id).ToListAsync();
        return Ok(list.Select(e => new EnrolmentDto(e.EnrolmentId, e.EventId, e.Event.EventName, e.UserId, $"{e.User.FirstName} {e.User.LastName}", e.CategoryId, e.Category.CategoryName, e.EnrolmentDate, e.Status)));
    }

    // GET /api/enrolments/me
    [HttpGet("api/enrolments/me")]
    public async Task<IActionResult> GetMine()
    {
        var cur = await SessionHelper.GetCurrentAsync(Request, _db, _auth);
        if (cur == null) return SessionHelper.UnauthorizedResult();
        if (cur.Value.user.Role.RoleName != "Participant") return SessionHelper.ForbiddenResult();
        var list = await _db.Enrolments.Include(e => e.Category).Include(e => e.Event).Where(e => e.UserId == cur.Value.user.UserId).ToListAsync();
        return Ok(list.Select(e => new EnrolmentDto(e.EnrolmentId, e.EventId, e.Event.EventName, e.UserId, $"{cur.Value.user.FirstName} {cur.Value.user.LastName}", e.CategoryId, e.Category.CategoryName, e.EnrolmentDate, e.Status)));
    }

    // DELETE /api/enrolments/{id}
    [HttpDelete("api/enrolments/{id:int}")]
    public async Task<IActionResult> Cancel(int id)
    {
        var cur = await SessionHelper.GetCurrentAsync(Request, _db, _auth);
        if (cur == null) return SessionHelper.UnauthorizedResult();
        var en = await _db.Enrolments.FindAsync(id);
        if (en == null) return NotFound(new { message = "Enrolment does not exist." });
        if (en.UserId != cur.Value.user.UserId) return SessionHelper.ForbiddenResult("Cancelling someone else's enrolment.");
        _db.Enrolments.Remove(en);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
