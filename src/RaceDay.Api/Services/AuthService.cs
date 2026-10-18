using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Models;

namespace RaceDay.Api.Services;

public class AuthService
{
    private readonly RaceDayContext _db;
    public AuthService(RaceDayContext db) => _db = db;

    public string HashPassword(string password) => BCrypt.Net.BCrypt.HashPassword(password, 11);
    public bool VerifyPassword(string password, string hash) => BCrypt.Net.BCrypt.Verify(password, hash);

    public string GenerateToken()
    {
        // 64 hex chars = 32 bytes
        var bytes = new byte[32];
        System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public async Task<(User user, Session session)?> ValidateSessionAsync(string? token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        var session = await _db.Sessions.Include(s => s.User).ThenInclude(u => u.Role)
            .FirstOrDefaultAsync(s => s.SessionToken == token && s.IsActive && s.ExpiresAt > DateTime.UtcNow);
        if (session == null) return null;
        return (session.User, session);
    }
}
