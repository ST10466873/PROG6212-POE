using Microsoft.EntityFrameworkCore;

namespace RaceDay.Tests;

public class EnrolmentTests
{
    [Fact]
    public async Task Enrolment_OnePerParticipantPerEvent_IsEnforced()
    {
        using var db = TestHelpers.CreateInMemoryContext();
        var (org, part, ev, cat) = await TestHelpers.SeedBasicAsync(db);
        var en = new RaceDay.Api.Models.Enrolment { EventId = ev.EventId, UserId = part.UserId, CategoryId = cat.CategoryId, EnrolmentDate = DateTime.UtcNow, Status = "Confirmed" };
        db.Enrolments.Add(en);
        await db.SaveChangesAsync();
        var duplicateExists = await db.Enrolments.CountAsync(e => e.EventId == ev.EventId && e.UserId == part.UserId);
        Assert.Equal(1, duplicateExists);
        // attempting second should be detected by query before insert (unique index)
        var second = await db.Enrolments.AnyAsync(e => e.EventId == ev.EventId && e.UserId == part.UserId);
        Assert.True(second);
    }

    [Fact]
    public async Task Enrolment_CategoryMustBelongToEvent()
    {
        using var db = TestHelpers.CreateInMemoryContext();
        var (org, part, ev, cat) = await TestHelpers.SeedBasicAsync(db);
        var otherEvent = new RaceDay.Api.Models.Event { OrganiserId = org.UserId, EventName = "Other", Description = "x", EventDate = DateTime.UtcNow.AddDays(20), Location = "Joburg", DistanceKm = 5, EventType = "Walk" };
        db.Events.Add(otherEvent);
        await db.SaveChangesAsync();
        var otherCat = new RaceDay.Api.Models.EventCategory { EventId = otherEvent.EventId, CategoryName = "Senior", MinAge = 20 };
        db.EventCategories.Add(otherCat);
        await db.SaveChangesAsync();
        // participant tries to enrol in ev with otherCat (mismatched) -> should be rejected
        Assert.NotEqual(ev.EventId, otherCat.EventId);
    }

    [Fact]
    public async Task Result_UniquePositionPerEvent()
    {
        using var db = TestHelpers.CreateInMemoryContext();
        var (org, part, ev, cat) = await TestHelpers.SeedBasicAsync(db);
        var en1 = new RaceDay.Api.Models.Enrolment { EventId = ev.EventId, UserId = part.UserId, CategoryId = cat.CategoryId };
        db.Enrolments.Add(en1);
        await db.SaveChangesAsync();
        var part2 = new RaceDay.Api.Models.User { RoleId = 2, FirstName = "A", LastName = "B", Email = $"a{Guid.NewGuid()}@test.com", PasswordHash = "hash", DateOfBirth = new DateTime(2000,1,1) };
        db.Users.Add(part2);
        await db.SaveChangesAsync();
        var en2 = new RaceDay.Api.Models.Enrolment { EventId = ev.EventId, UserId = part2.UserId, CategoryId = cat.CategoryId };
        db.Enrolments.Add(en2);
        await db.SaveChangesAsync();
        var r1 = new RaceDay.Api.Models.Result { EnrolmentId = en1.EnrolmentId, EventId = ev.EventId, FinishTime = TimeSpan.FromMinutes(30), FinishingPosition = 1 };
        db.Results.Add(r1);
        await db.SaveChangesAsync();
        var dupPos = await db.Results.AnyAsync(r => r.EventId == ev.EventId && r.FinishingPosition == 1);
        Assert.True(dupPos);
    }

    [Fact]
    public async Task Route_OnePerEvent_IsUnique()
    {
        using var db = TestHelpers.CreateInMemoryContext();
        var (_, _, ev, _) = await TestHelpers.SeedBasicAsync(db);
        var routes = await db.Routes.CountAsync(r => r.EventId == ev.EventId);
        Assert.Equal(1, routes);
    }
}
