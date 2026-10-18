namespace RaceDay.Api.Models;

/// <summary>
/// Represents a system user — either an Organiser or Participant.
/// Maps to dbo.Users. Email is the unique login credential.
/// </summary>
public class User
{
    /// <summary>Surrogate PK used in all API routes (e.g. /api/users/{id}).</summary>
    public int UserId { get; set; }

    /// <summary>FK to Roles — discriminates Organiser vs Participant.</summary>
    public int RoleId { get; set; }

    /// <summary>Navigation to the role lookup row.</summary>
    public Role Role { get; set; } = null!;

    /// <summary>Given name shown on profiles and results.</summary>
    public string FirstName { get; set; } = null!;

    /// <summary>Family name shown on profiles and results.</summary>
    public string LastName { get; set; } = null!;

    /// <summary>Unique login email — validated with format check in DB.</summary>
    public string Email { get; set; } = null!;

    /// <summary>BCrypt hash only — never plain text (see AuthService).</summary>
    public string PasswordHash { get; set; } = null!;

    /// <summary>Optional contact number.</summary>
    public string? PhoneNumber { get; set; }

    /// <summary>Required for age-category validation (Under 20 / Senior).</summary>
    public DateTime DateOfBirth { get; set; }

    /// <summary>Soft-delete flag — keeps enrolment/result history intact.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Audit timestamp when the account was created (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Events owned by this organiser (empty for participants).</summary>
    public ICollection<Event> OrganisedEvents { get; set; } = new List<Event>();

    /// <summary>Entries this user has in events (as a participant).</summary>
    public ICollection<Enrolment> Enrolments { get; set; } = new List<Enrolment>();

    /// <summary>Server-side sessions for this user (auth state).</summary>
    public ICollection<Session> Sessions { get; set; } = new List<Session>();
}
