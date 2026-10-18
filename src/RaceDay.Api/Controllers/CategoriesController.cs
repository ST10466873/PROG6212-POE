using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.DTOs;
using RaceDay.Api.Helpers;
using RaceDay.Api.Services;

namespace RaceDay.Api.Controllers;

[ApiController]
public class CategoriesController : ControllerBase
{
    private readonly RaceDayContext _db;
    private readonly AuthService _auth;
    public CategoriesController(RaceDayContext db, AuthService auth) { _db = db; _auth = auth; }

    // GET /api/events/{id}/categories
    [HttpGet("api/events/{id:int}/categories")]
    public async Task<IActionResult> GetForEvent(int id)
    {
        var ev = await _db.Events.FindAsync(id);
        if (ev == null) return NotFound(new { message = "Event does not exist." });
        var cats = await _db.EventCategories.Where(c => c.EventId == id).ToListAsync();
        return Ok(cats.Select(c => new CategoryDto(c.CategoryId, c.EventId, c.CategoryName, c.MinAge, c.MaxAge)));
    }

    // POST /api/events/{id}/categories
    [HttpPost("api/events/{id:int}/categories")]
    public async Task<IActionResult> CreateForEvent(int id, [FromBody] CreateCategoryRequest req)
    {
        var cur = await SessionHelper.GetCurrentAsync(Request, _db, _auth);
        if (cur == null) return SessionHelper.UnauthorizedResult();
        if (cur.Value.user.Role.RoleName != "Organiser") return SessionHelper.ForbiddenResult();
        var ev = await _db.Events.FindAsync(id);
        if (ev == null) return NotFound(new { message = "Event does not exist." });
        if (ev.OrganiserId != cur.Value.user.UserId) return SessionHelper.ForbiddenResult("Not the owning Organiser.");
        if (string.IsNullOrWhiteSpace(req.CategoryName)) return BadRequest(new { message = "CategoryName required." });
        if (req.MinAge.HasValue && req.MinAge < 0) return BadRequest(new { message = "MinAge cannot be negative." });
        if (req.MaxAge.HasValue && req.MaxAge < 0) return BadRequest(new { message = "MaxAge cannot be negative." });
        if (req.MinAge.HasValue && req.MaxAge.HasValue && req.MinAge > req.MaxAge) return BadRequest(new { message = "MinAge must be <= MaxAge." });
        if (await _db.EventCategories.AnyAsync(c => c.EventId == id && c.CategoryName == req.CategoryName.Trim()))
            return BadRequest(new { message = "Duplicate category name for this event." });

        var cat = new Models.EventCategory { EventId = id, CategoryName = req.CategoryName.Trim(), MinAge = req.MinAge, MaxAge = req.MaxAge };
        _db.EventCategories.Add(cat);
        await _db.SaveChangesAsync();
        return Created($"/api/categories/{cat.CategoryId}", new CategoryDto(cat.CategoryId, cat.EventId, cat.CategoryName, cat.MinAge, cat.MaxAge));
    }

    // PUT /api/categories/{id}
    [HttpPut("api/categories/{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] CreateCategoryRequest req)
    {
        var cur = await SessionHelper.GetCurrentAsync(Request, _db, _auth);
        if (cur == null) return SessionHelper.UnauthorizedResult();
        if (cur.Value.user.Role.RoleName != "Organiser") return SessionHelper.ForbiddenResult();
        var cat = await _db.EventCategories.Include(c => c.Event).FirstOrDefaultAsync(c => c.CategoryId == id);
        if (cat == null) return NotFound(new { message = "Category does not exist." });
        if (cat.Event.OrganiserId != cur.Value.user.UserId) return SessionHelper.ForbiddenResult("Not the owning Organiser.");
        if (req.MinAge.HasValue && req.MinAge < 0) return BadRequest(new { message = "MinAge cannot be negative." });
        if (req.MaxAge.HasValue && req.MaxAge < 0) return BadRequest(new { message = "MaxAge cannot be negative." });
        if (req.MinAge.HasValue && req.MaxAge.HasValue && req.MinAge > req.MaxAge) return BadRequest(new { message = "MinAge must be <= MaxAge." });
        if (await _db.EventCategories.AnyAsync(c => c.EventId == cat.EventId && c.CategoryName == req.CategoryName.Trim() && c.CategoryId != id))
            return BadRequest(new { message = "Duplicate category name for this event." });

        cat.CategoryName = req.CategoryName.Trim();
        cat.MinAge = req.MinAge;
        cat.MaxAge = req.MaxAge;
        await _db.SaveChangesAsync();
        return Ok(new CategoryDto(cat.CategoryId, cat.EventId, cat.CategoryName, cat.MinAge, cat.MaxAge));
    }

    // DELETE /api/categories/{id}
    [HttpDelete("api/categories/{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var cur = await SessionHelper.GetCurrentAsync(Request, _db, _auth);
        if (cur == null) return SessionHelper.UnauthorizedResult();
        if (cur.Value.user.Role.RoleName != "Organiser") return SessionHelper.ForbiddenResult();
        var cat = await _db.EventCategories.Include(c => c.Event).FirstOrDefaultAsync(c => c.CategoryId == id);
        if (cat == null) return NotFound(new { message = "Category does not exist." });
        if (cat.Event.OrganiserId != cur.Value.user.UserId) return SessionHelper.ForbiddenResult("Not the owning Organiser.");
        if (await _db.Enrolments.AnyAsync(e => e.CategoryId == id))
            return Conflict(new { message = "Category still has enrolments." });
        _db.EventCategories.Remove(cat);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
