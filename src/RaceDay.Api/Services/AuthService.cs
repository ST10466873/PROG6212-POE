using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Models;

namespace RaceDay.Api.Services;

/// <summary>
/// Centralises password hashing (BCrypt) and server-side session handling.
/// All controllers delegate to this service so hashing policy lives in one place.
/// </summary>
public class AuthService
{
    private readonly RaceDayContext _db;
    public AuthService(RaceDayContext db) => _db = db;

    /// <summary>
    /// Hashes a plain-text password with BCrypt cost 11.
    /// Cost 11 balances security and latency (~80ms) as required by the brief.
    /// </summary>
    public string HashPassword(string password) => BCrypt.Net.BCrypt.HashPassword(password, 11);

    /// <summary>
    /// Verifies a candidate password against a stored BCrypt hash.
    /// </summary>
    public bool VerifyPassword(string password, string hash) => BCrypt.Net.BCrypt.Verify(password, hash);

    /// <summary>
    /// Generates a cryptographically random 64-hex-char token (32 bytes).
    /// Returned by POST /api/auth/login and stored as Sessions.SessionToken.
    /// </summary>
    public string GenerateToken()
    {
        // 64 hex chars = 32 random bytes -> 256 bits of entropy
        var bytes = new byte[32];
        System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    /// <summary>
    /// Validates an incoming token: must exist, be IsActive and not expired.
    /// Includes User+Role so callers can check RoleName without extra queries.
    /// </summary>
    /// <returns>Tuple of (user, session) or null when token is missing/invalid/expired.</returns>
    public async Task<(User user, Session session)?> ValidateSessionAsync(string? token)
    {
        // Guard: empty header means unauthenticated — fast exit
        if (string.IsNullOrWhiteSpace(token)) return null;
        // Query sessions table — IsActive + ExpiresAt implement logout + expiry
        var session = await _db.Sessions.Include(s => s.User).ThenInclude(u => u.Role)
            .FirstOrDefaultAsync(s => s.SessionToken == token && s.IsActive && s.ExpiresAt > DateTime.UtcNow);
        if (session == null) return null;
        return (session.User, session);
    }
}
