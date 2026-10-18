using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.DTOs;
using RaceDay.Api.Helpers;
using RaceDay.Api.Services;

namespace RaceDay.Api.Controllers;

/// <summary>
/// Category management — Organisers define age/distance categories per event.
/// Implements Section 4 of the endpoint plan (4 routes) with ownership and enrolment checks.
/// </summary>
[ApiController]
public class CategoriesController : ControllerBase
{
    private readonly RaceDayContext _db;
    private readonly AuthService _auth;
    public CategoriesController(RaceDayContext db, AuthService auth) { _db = db; _auth = auth; }

    /// <summary>
    /// GET /api/events/{id}/categories — public list of categories for an event.
    /// </summary>
    /// <returns>200 with list, 404 when event missing.</returns>
    [HttpGet("api/events/{id:int}/categories")]
    public async Task<IActionResult> GetForEvent(int id)
    {
        // Verify event exists before listing its categories
        var ev = await _db.Events.FindAsync(id);
        if (ev == null) return NotFound(new { message = "Event does not exist." });
        var cats = await _db.EventCategories.Where(c => c.EventId == id).ToListAsync();
        return Ok(cats.Select(c => new CategoryDto(c.CategoryId, c.EventId, c.CategoryName, c.MinAge, c.MaxAge)));
    }

    /// <summary>
    /// POST /api/events/{id}/categories — adds a category to an owned event.
    /// Validates age bounds (MinAge &lt;= MaxAge, non-negative) and duplicate name per event.
    /// </summary>
    /// <returns>201 Created, 400/403/404 on failure.</returns>
    [HttpPost("api/events/{id:int}/categories")]
    public async Task<IActionResult> CreateForEvent(int id, [FromBody] CreateCategoryRequest req)
    {
        // Must be logged in as Organiser
        var cur = await SessionHelper.GetCurrentAsync(Request, _db, _auth);
        if (cur == null) return SessionHelper.UnauthorizedResult();
        if (cur.Value.user.Role.RoleName != "Organiser") return SessionHelper.ForbiddenResult();
        // Event must exist and be owned by caller
        var ev = await _db.Events.FindAsync(id);
        if (ev == null) return NotFound(new { message = "Event does not exist." });
        if (ev.OrganiserId != cur.Value.user.UserId) return SessionHelper.ForbiddenResult("Not the owning Organiser.");
        // Domain validation mirrors CK_EventCategories_* constraints
        if (string.IsNullOrWhiteSpace(req.CategoryName)) return BadRequest(new { message = "CategoryName required." });
        if (req.MinAge.HasValue && req.MinAge < 0) return BadRequest(new { message = "MinAge cannot be negative." });
        if (req.MaxAge.HasValue && req.MaxAge < 0) return BadRequest(new { message = "MaxAge cannot be negative." });
        if (req.MinAge.HasValue && req.MaxAge.HasValue && req.MinAge > req.MaxAge) return BadRequest(new { message = "MinAge must be <= MaxAge." });
        // Enforce UQ_EventCategories_Event_Name inside one event
        if (await _db.EventCategories.AnyAsync(c => c.EventId == id && c.CategoryName == req.CategoryName.Trim()))
            return BadRequest(new { message = "Duplicate category name for this event." });

        var cat = new Models.EventCategory { EventId = id, CategoryName = req.CategoryName.Trim(), MinAge = req.MinAge, MaxAge = req.MaxAge };
        _db.EventCategories.Add(cat);
        await _db.SaveChangesAsync();
        return Created($"/api/categories/{cat.CategoryId}", new CategoryDto(cat.CategoryId, cat.EventId, cat.CategoryName, cat.MinAge, cat.MaxAge));
    }

    /// <summary>
    /// PUT /api/categories/{id} — updates name/age range (owner Organiser only).
    /// </summary>
    /// <returns>200, 403 when not owner, 404 when missing.</returns>
    [HttpPut("api/categories/{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] CreateCategoryRequest req)
    {
        var cur = await SessionHelper.GetCurrentAsync(Request, _db, _auth);
        if (cur == null) return SessionHelper.UnauthorizedResult();
        if (cur.Value.user.Role.RoleName != "Organiser") return SessionHelper.ForbiddenResult();
        // Include parent event for ownership check
        var cat = await _db.EventCategories.Include(c => c.Event).FirstOrDefaultAsync(c => c.CategoryId == id);
        if (cat == null) return NotFound(new { message = "Category does not exist." });
        if (cat.Event.OrganiserId != cur.Value.user.UserId) return SessionHelper.ForbiddenResult("Not the owning Organiser.");
        // Re-validate age bounds on update
        if (req.MinAge.HasValue && req.MinAge < 0) return BadRequest(new { message = "MinAge cannot be negative." });
        if (req.MaxAge.HasValue && req.MaxAge < 0) return BadRequest(new { message = "MaxAge cannot be negative." });
        if (req.MinAge.HasValue && req.MaxAge.HasValue && req.MinAge > req.MaxAge) return BadRequest(new { message = "MinAge must be <= MaxAge." });
        // Prevent renaming to a duplicate within same event
        if (await _db.EventCategories.AnyAsync(c => c.EventId == cat.EventId && c.CategoryName == req.CategoryName.Trim() && c.CategoryId != id))
            return BadRequest(new { message = "Duplicate category name for this event." });

        cat.CategoryName = req.CategoryName.Trim();
        cat.MinAge = req.MinAge;
        cat.MaxAge = req.MaxAge;
        await _db.SaveChangesAsync();
        return Ok(new CategoryDto(cat.CategoryId, cat.EventId, cat.CategoryName, cat.MinAge, cat.MaxAge));
    }

    /// <summary>
    /// DELETE /api/categories/{id} — removes a category that has no enrolments yet.
    /// Returns 409 when still referenced by enrolments (protects history).
    /// </summary>
    /// <returns>204 No Content, 403/404/409 on failure.</returns>
    [HttpDelete("api/categories/{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var cur = await SessionHelper.GetCurrentAsync(Request, _db, _auth);
        if (cur == null) return SessionHelper.UnauthorizedResult();
        if (cur.Value.user.Role.RoleName != "Organiser") return SessionHelper.ForbiddenResult();
        var cat = await _db.EventCategories.Include(c => c.Event).FirstOrDefaultAsync(c => c.CategoryId == id);
        if (cat == null) return NotFound(new { message = "Category does not exist." });
        if (cat.Event.OrganiserId != cur.Value.user.UserId) return SessionHelper.ForbiddenResult("Not the owning Organiser.");
        // Guard: cannot delete a category already chosen by participants
        if (await _db.Enrolments.AnyAsync(e => e.CategoryId == id))
            return Conflict(new { message = "Category still has enrolments." });
        _db.EventCategories.Remove(cat);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
