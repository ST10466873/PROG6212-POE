using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.DTOs;
using RaceDay.Api.Helpers;
using RaceDay.Api.Services;

namespace RaceDay.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly RaceDayContext _db;
    private readonly AuthService _auth;
    public AuthController(RaceDayContext db, AuthService auth) { _db = db; _auth = auth; }

    // POST /api/auth/register
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest req)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (req.Role != "Organiser" && req.Role != "Participant")
            return BadRequest(new { message = "Role must be Organiser or Participant." });
        if (req.DateOfBirth >= DateTime.UtcNow.Date)
            return BadRequest(new { message = "DateOfBirth must be in the past." });
        if (string.IsNullOrWhiteSpace(req.Email) || !req.Email.Contains("@"))
            return BadRequest(new { message = "Invalid email." });

        var existing = await _db.Users.AnyAsync(u => u.Email == req.Email);
        if (existing) return Conflict(new { message = "Email already registered." });

        var role = await _db.Roles.FirstAsync(r => r.RoleName == req.Role);
        var user = new Models.User
        {
            RoleId = role.RoleId,
            FirstName = req.FirstName.Trim(),
            LastName = req.LastName.Trim(),
            Email = req.Email.Trim().ToLowerInvariant(),
            PasswordHash = _auth.HashPassword(req.Password),
            PhoneNumber = req.PhoneNumber,
            DateOfBirth = req.DateOfBirth.Date,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return Created($"/api/users/{user.UserId}", new { user.UserId, user.Email, Role = req.Role });
    }

    // POST /api/auth/login
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        var user = await _db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Email == req.Email.ToLowerInvariant());
        if (user == null || !_auth.VerifyPassword(req.Password, user.PasswordHash))
            return Unauthorized(new { message = "Wrong email or password." });
        if (!user.IsActive) return Unauthorized(new { message = "Account is deactivated." });

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

    // POST /api/auth/logout
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        var cur = await SessionHelper.GetCurrentAsync(Request, _db, _auth);
        if (cur == null) return Unauthorized(new { message = "No active session." });
        cur.Value.session.IsActive = false;
        await _db.SaveChangesAsync();
        return Ok(new { message = "Session ended." });
    }
}
