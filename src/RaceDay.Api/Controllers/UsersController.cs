using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.DTOs;
using RaceDay.Api.Helpers;
using RaceDay.Api.Services;

namespace RaceDay.Api.Controllers;

/// <summary>
/// User profile endpoints — view and update the currently authenticated user's own profile.
/// Implements Section 2 of the endpoint plan (2 routes).
/// </summary>
[ApiController]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private readonly RaceDayContext _db;
    private readonly AuthService _auth;
    public UsersController(RaceDayContext db, AuthService auth) { _db = db; _auth = auth; }

    /// <summary>
    /// GET /api/users/me — returns the profile of the caller.
    /// Requires a valid X-Session-Token / Bearer header.
    /// </summary>
    /// <returns>200 with UserProfileDto, 401 when unauthenticated.</returns>
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        // Resolve session — helper checks IsActive + ExpiresAt
        var cur = await SessionHelper.GetCurrentAsync(Request, _db, _auth);
        if (cur == null) return SessionHelper.UnauthorizedResult();
        var u = cur.Value.user;
        // Map to DTO including RoleName via navigation
        return Ok(new UserProfileDto(u.UserId, u.FirstName, u.LastName, u.Email, u.Role.RoleName, u.PhoneNumber, u.DateOfBirth, u.IsActive, u.CreatedAt));
    }

    /// <summary>
    /// PUT /api/users/me — updates first/last name, email, phone and date of birth.
    /// Enforces unique email (409) and past DateOfBirth (400).
    /// </summary>
    /// <returns>200 with updated profile, 400/401/409 on validation/auth failures.</returns>
    [HttpPut("me")]
    public async Task<IActionResult> UpdateMe([FromBody] UpdateProfileRequest req)
    {
        // Must be logged in
        var cur = await SessionHelper.GetCurrentAsync(Request, _db, _auth);
        if (cur == null) return SessionHelper.UnauthorizedResult();
        // Basic required-field validation
        if (string.IsNullOrWhiteSpace(req.FirstName) || string.IsNullOrWhiteSpace(req.LastName) || string.IsNullOrWhiteSpace(req.Email))
            return BadRequest(new { message = "FirstName, LastName and Email are required." });
        if (!req.Email.Contains("@")) return BadRequest(new { message = "Invalid email." });
        // Enforce UQ_Users_Email across other accounts
        if (await _db.Users.AnyAsync(x => x.Email == req.Email.ToLowerInvariant() && x.UserId != cur.Value.user.UserId))
            return Conflict(new { message = "Email used by another account." });
        // Age sanity
        if (req.DateOfBirth >= DateTime.UtcNow.Date) return BadRequest(new { message = "DateOfBirth must be in the past." });

        // Load tracked entity and apply changes
        var user = await _db.Users.FindAsync(cur.Value.user.UserId);
        user!.FirstName = req.FirstName.Trim();
        user.LastName = req.LastName.Trim();
        user.Email = req.Email.Trim().ToLowerInvariant();
        user.PhoneNumber = req.PhoneNumber;
        user.DateOfBirth = req.DateOfBirth.Date;
        await _db.SaveChangesAsync();
        // Resolve role name for response
        var role = await _db.Roles.FindAsync(user.RoleId);
        return Ok(new UserProfileDto(user.UserId, user.FirstName, user.LastName, user.Email, role!.RoleName, user.PhoneNumber, user.DateOfBirth, user.IsActive, user.CreatedAt));
    }
}
