namespace RaceDay.Api.Models;

/// <summary>
/// Server-side session row — stores the opaque bearer token returned by login.
/// Maps to dbo.Sessions (UQ on SessionToken, IsActive for logout audit).
/// </summary>
public class Session
{
    /// <summary>Surrogate PK.</summary>
    public int SessionId { get; set; }

    /// <summary>FK to the authenticated user.</summary>
    public int UserId { get; set; }

    /// <summary>Navigation to the user (includes Role).</summary>
    public User User { get; set; } = null!;

    /// <summary>64-hex opaque token sent as X-Session-Token / Bearer.</summary>
    public string SessionToken { get; set; } = null!;

    /// <summary>UTC creation time (login moment).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Hard expiry — checked on every authenticated request.</summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>False after logout — preserves row for audit instead of deleting.</summary>
    public bool IsActive { get; set; } = true;
}
