using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Models;
using RaceDay.Api.Services;

namespace RaceDay.Tests;

public static class TestHelpers
{
    public static RaceDayContext CreateInMemoryContext(string? dbName = null)
    {
        var opts = new DbContextOptionsBuilder<RaceDayContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
            .Options;
        var ctx = new RaceDayContext(opts);
        ctx.Database.EnsureCreated();
        return ctx;
    }

    public static async Task<(User organiser, User participant, Event ev, EventCategory cat)> SeedBasicAsync(RaceDayContext db, AuthService? auth = null)
    {
        auth ??= new AuthService(db);
        // roles seeded by EnsureCreated
        var org = new User { RoleId = 1, FirstName = "Org", LastName = "One", Email = $"org{Guid.NewGuid()}@test.com", PasswordHash = auth.HashPassword("Pass123!"), DateOfBirth = new DateTime(1990, 1, 1), IsActive = true, CreatedAt = DateTime.UtcNow };
        var part = new User { RoleId = 2, FirstName = "Part", LastName = "One", Email = $"part{Guid.NewGuid()}@test.com", PasswordHash = auth.HashPassword("Pass123!"), DateOfBirth = new DateTime(2000, 1, 1), IsActive = true, CreatedAt = DateTime.UtcNow };
        db.Users.AddRange(org, part);
        await db.SaveChangesAsync();
        var ev = new Event { OrganiserId = org.UserId, EventName = "Test 10K", Description = "Desc", EventDate = DateTime.UtcNow.AddDays(10), Location = "Cape Town", DistanceKm = 10, EventType = "Run" };
        db.Events.Add(ev);
        await db.SaveChangesAsync();
        var route = new Route { EventId = ev.EventId, StartPoint = "Start", EndPoint = "End", ElevationGainM = 10 };
        db.Routes.Add(route);
        var cat = new EventCategory { EventId = ev.EventId, CategoryName = "Senior", MinAge = 20, MaxAge = null };
        db.EventCategories.Add(cat);
        await db.SaveChangesAsync();
        return (org, part, ev, cat);
    }

    public static async Task<Session> CreateSessionAsync(RaceDayContext db, User user)
    {
        var auth = new AuthService(db);
        var token = auth.GenerateToken();
        var s = new Session { UserId = user.UserId, SessionToken = token, CreatedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow.AddHours(24), IsActive = true };
        db.Sessions.Add(s);
        await db.SaveChangesAsync();
        return s;
    }
}
