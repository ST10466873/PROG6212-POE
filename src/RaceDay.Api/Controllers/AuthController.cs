using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.DTOs;
using RaceDay.Api.Helpers;
using RaceDay.Api.Services;

namespace RaceDay.Api.Controllers;

/// <summary>
/// Public authentication endpoints — register, login and logout.
/// Implements Section 1 of the endpoint plan (3 routes) with BCrypt and server-side sessions.
/// </summary>
[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly RaceDayContext _db;
    private readonly AuthService _auth;
    public AuthController(RaceDayContext db, AuthService auth) { _db = db; _auth = auth; }

    /// <summary>
    /// POST /api/auth/register — creates a new account and assigns the selected role.
    /// Validates role, email format, duplicate email and past DateOfBirth.
    /// Password is BCrypt-hashed before storage (never plain text).
    /// </summary>
    /// <returns>201 Created on success, 400 on validation, 409 when email exists.</returns>
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest req)
    {
        // Data-annotation validation (Required, EmailAddress, etc.)
        if (!ModelState.IsValid) return BadRequest(ModelState);
        // Defence-in-depth: only the two brief roles are permitted
        if (req.Role != "Organiser" && req.Role != "Participant")
            return BadRequest(new { message = "Role must be Organiser or Participant." });
        // Age sanity — birth date must be in the past
        if (req.DateOfBirth >= DateTime.UtcNow.Date)
            return BadRequest(new { message = "DateOfBirth must be in the past." });
        // Cheap format check before DB round-trip
        if (string.IsNullOrWhiteSpace(req.Email) || !req.Email.Contains("@"))
            return BadRequest(new { message = "Invalid email." });

        // Enforce UQ_Users_Email — return 409 instead of bubbling a DB exception
        var existing = await _db.Users.AnyAsync(u => u.Email == req.Email);
        if (existing) return Conflict(new { message = "Email already registered." });

        // Resolve RoleId via lookup table (needed for FK)
        var role = await _db.Roles.FirstAsync(r => r.RoleName == req.Role);
        var user = new Models.User
        {
            RoleId = role.RoleId,
            FirstName = req.FirstName.Trim(),
            LastName = req.LastName.Trim(),
            Email = req.Email.Trim().ToLowerInvariant(), // normalise for login
            PasswordHash = _auth.HashPassword(req.Password), // BCrypt cost 11
            PhoneNumber = req.PhoneNumber,
            DateOfBirth = req.DateOfBirth.Date,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        // Return Location header pointing to the new user resource
        return Created($"/api/users/{user.UserId}", new { user.UserId, user.Email, Role = req.Role });
    }

    /// <summary>
    /// POST /api/auth/login — validates credentials and creates a 24h server-side session.
    /// Returns the opaque session token the client must send as X-Session-Token thereafter.
    /// </summary>
    /// <returns>200 with AuthResponse, 401 on wrong credentials or inactive account.</returns>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        // Email lookup is case-insensitive (stored lower-cased)
        var user = await _db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Email == req.Email.ToLowerInvariant());
        // Verify BCrypt hash — constant-time comparison inside library
        if (user == null || !_auth.VerifyPassword(req.Password, user.PasswordHash))
            return Unauthorized(new { message = "Wrong email or password." });
        // Deactivated accounts cannot sign in (soft-delete)
        if (!user.IsActive) return Unauthorized(new { message = "Account is deactivated." });

        // Create session row — token is 64 hex chars, expires in 24h
        var token = _auth.GenerateToken();
        var session = new Models.Session
        {
            UserId = user.UserId,
            SessionToken = token,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(24),
            IsActive = true
        };
        _db.Sessions.Add(session);
        await _db.SaveChangesAsync();
        return Ok(new AuthResponse(user.UserId, user.Email, user.Role.RoleName, token, session.ExpiresAt));
    }

    /// <summary>
    /// POST /api/auth/logout — invalidates the current session (IsActive = false).
    /// Preserves the row for audit instead of deleting it.
    /// </summary>
    /// <returns>200 when ended, 401 when no active session.</returns>
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        // Resolve current session from X-Session-Token / Bearer header
        var cur = await SessionHelper.GetCurrentAsync(Request, _db, _auth);
        if (cur == null) return Unauthorized(new { message = "No active session." });
        // Soft-invalidate
        cur.Value.session.IsActive = false;
        await _db.SaveChangesAsync();
        return Ok(new { message = "Session ended." });
    }
}
