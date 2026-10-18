namespace RaceDay.Api.Models;

/// <summary>
/// Junction entity linking a Participant to an Event and the chosen Category.
/// Maps to dbo.Enrolments — unique on (EventId, UserId) so one entry per participant per event.
/// </summary>
public class Enrolment
{
    /// <summary>Surrogate PK for /api/enrolments/{id}.</summary>
    public int EnrolmentId { get; set; }

    /// <summary>FK to the event entered.</summary>
    public int EventId { get; set; }

    /// <summary>Parent event navigation.</summary>
    public Event Event { get; set; } = null!;

    /// <summary>FK to the participant user.</summary>
    public int UserId { get; set; }

    /// <summary>Navigation to the participant.</summary>
    public User User { get; set; } = null!;

    /// <summary>FK to the chosen category — must belong to EventId (validated in API layer).</summary>
    public int CategoryId { get; set; }

    /// <summary>Navigation to the chosen category.</summary>
    public EventCategory Category { get; set; } = null!;

    /// <summary>UTC timestamp when the participant entered (auto-set).</summary>
    public DateTime EnrolmentDate { get; set; } = DateTime.UtcNow;

    /// <summary>Workflow state: Confirmed (active) or Cancelled.</summary>
    public string Status { get; set; } = "Confirmed"; // Confirmed | Cancelled

    /// <summary>Optional result captured later (1:1, may be null before race day).</summary>
    public Result? Result { get; set; }
}
