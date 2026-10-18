using RaceDay.Api.Services;

namespace RaceDay.Tests;

public class AuthTests
{
    [Fact]
    public void HashAndVerify_Password_Roundtrips()
    {
        using var db = TestHelpers.CreateInMemoryContext();
        var auth = new AuthService(db);
        var hash = auth.HashPassword("Secret123!");
        Assert.True(auth.VerifyPassword("Secret123!", hash));
        Assert.False(auth.VerifyPassword("Wrong", hash));
    }

    [Fact]
    public void GenerateToken_Is64HexChars_AndUnique()
    {
        using var db = TestHelpers.CreateInMemoryContext();
        var auth = new AuthService(db);
        var t1 = auth.GenerateToken();
        var t2 = auth.GenerateToken();
        Assert.Equal(64, t1.Length);
        Assert.Equal(64, t2.Length);
        Assert.NotEqual(t1, t2);
        Assert.Matches("^[0-9a-f]{64}$", t1);
    }

    [Fact]
    public async Task ValidateSession_ReturnsNull_WhenExpired()
    {
        using var db = TestHelpers.CreateInMemoryContext();
        var auth = new AuthService(db);
        var (org, _, _, _) = await TestHelpers.SeedBasicAsync(db, auth);
        var s = await TestHelpers.CreateSessionAsync(db, org);
        s.ExpiresAt = DateTime.UtcNow.AddHours(-1);
        await db.SaveChangesAsync();
        var result = await auth.ValidateSessionAsync(s.SessionToken);
        Assert.Null(result);
    }

    [Fact]
    public async Task ValidateSession_ReturnsUser_WhenActive()
    {
        using var db = TestHelpers.CreateInMemoryContext();
        var auth = new AuthService(db);
        var (org, _, _, _) = await TestHelpers.SeedBasicAsync(db, auth);
        var s = await TestHelpers.CreateSessionAsync(db, org);
        var result = await auth.ValidateSessionAsync(s.SessionToken);
        Assert.NotNull(result);
        Assert.Equal(org.UserId, result.Value.user.UserId);
    }
}
